using System;
using System.Collections.Generic;

namespace SafeMining
{
    public enum HazardSource { LegacyTimeline, LocalEdgeSimulation, MqttEdgeSimulation }

    [Serializable]
    public class MiningEdgeSettings
    {
        public float sampleInterval = .05f;
        public float warningThreshold = .45f;
        public float dangerThreshold = .75f;
        public float clearThreshold = .30f;
        public float minimumDuration = .5f;
        public float clearDuration = 1f;
        public float normalIntensity = .12f;
        public float warningIntensity = .58f;
        public float dangerIntensity = .90f;
        public float noiseAmplitude = .04f;
        public float dangerPulseSeconds = 2f;

        public MiningEdgeSettings Copy() => (MiningEdgeSettings)MemberwiseClone();
        public void Validate()
        {
            foreach (float v in new[] { sampleInterval, warningThreshold, dangerThreshold, clearThreshold,
                minimumDuration, clearDuration, normalIntensity, warningIntensity, dangerIntensity, noiseAmplitude, dangerPulseSeconds })
                if (float.IsNaN(v) || float.IsInfinity(v)) throw new ArgumentException("Parameter edge harus finite.");
            if (sampleInterval < .01f || sampleInterval > .5f || clearThreshold < 0 ||
                clearThreshold >= warningThreshold || warningThreshold >= dangerThreshold || dangerThreshold > 1 ||
                minimumDuration < sampleInterval || clearDuration < sampleInterval || dangerPulseSeconds < minimumDuration ||
                noiseAmplitude < 0 || normalIntensity - noiseAmplitude < 0 || normalIntensity + noiseAmplitude >= clearThreshold ||
                warningIntensity - noiseAmplitude < warningThreshold || warningIntensity + noiseAmplitude >= dangerThreshold ||
                dangerIntensity - noiseAmplitude < dangerThreshold || dangerIntensity + noiseAmplitude > 1)
                throw new ArgumentException("Parameter getaran/edge tidak valid; periksa ambang, profil, noise, dan durasi.");
        }
    }

    [Serializable]
    public class EdgeStatusMessage
    {
        // Sentinels also detect missing fields when using JsonUtility.FromJsonOverwrite.
        public int schemaVersion = -1;
        public string sessionId, layoutId, eventId, deviceId, source;
        public long sequence = -1;
        public float simulationTimeS = -1;
        public float vibrationNormalized = -1;
        public int level = -1;
        public string Topic => "safe-mining/v1/" + sessionId + "/edge/" + deviceId + "/status";
    }

    public sealed class MiningEdgeProcessor
    {
        readonly MiningEdgeSettings settings;
        float warningHeld, dangerHeld, clearHeld;
        public int Level { get; private set; }
        public MiningEdgeProcessor(MiningEdgeSettings settings) { this.settings = settings.Copy(); }
        public bool Sample(float value, float dt)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1 || dt <= 0)
                throw new ArgumentException("Invalid edge sample");
            if (Level == 2) return false; // Debris stays closed until the session is reset.
            warningHeld = value >= settings.warningThreshold ? warningHeld + dt : 0;
            dangerHeld = value >= settings.dangerThreshold ? dangerHeld + dt : 0;
            clearHeld = value <= settings.clearThreshold ? clearHeld + dt : 0;
            int previous = Level;
            if (dangerHeld + .000001f >= settings.minimumDuration) Level = 2;
            else if (warningHeld + .000001f >= settings.minimumDuration) Level = 1;
            else if (clearHeld + .000001f >= settings.clearDuration) Level = 0;
            return previous != Level;
        }
    }

    // Fixed sample index and integer hash: no Unity random state, player position, collider, or spawn trigger.
    public sealed class MiningVibrationGenerator
    {
        readonly MiningEdgeSettings settings;
        readonly List<ScheduledRockfall> profiles;
        readonly int seed;
        public MiningVibrationGenerator(int seed, IEnumerable<ScheduledRockfall> schedule, MiningEdgeSettings settings)
        {
            this.seed = seed; this.settings = settings.Copy(); profiles = new List<ScheduledRockfall>();
            foreach (var p in schedule) profiles.Add(new ScheduledRockfall(p.detectorIndex, p.warningTime, p.collapseTime));
        }
        public float Sample(int device, long tick)
        {
            double time = tick * (double)settings.sampleInterval;
            float intensity = settings.normalIntensity;
            foreach (var p in profiles)
                if (p.detectorIndex == device && time >= p.warningTime && time < p.collapseTime + settings.dangerPulseSeconds)
                    intensity = Math.Max(intensity, time >= p.collapseTime ? settings.dangerIntensity : settings.warningIntensity);
            uint hash;
            unchecked
            {
                hash = (uint)seed ^ ((uint)(device + 1) * 747796405u) ^ ((uint)tick * 2891336453u);
                hash = (hash ^ (hash >> 16)) * 2246822519u;
                hash = (hash ^ (hash >> 13)) * 3266489917u; hash ^= hash >> 16;
            }
            return intensity + ((hash & 65535) / 65535f * 2 - 1) * settings.noiseAmplitude;
        }
    }

    public sealed class MiningEdgeValidator
    {
        readonly string session, layout;
        readonly long[] sequences;
        readonly float[] times;
        readonly int[] levels;
        public MiningEdgeValidator(string session, string layout, int count)
        { this.session = session; this.layout = layout; sequences = new long[count]; times = new float[count]; levels = new int[count]; }
        public bool Accept(string topic, EdgeStatusMessage m, float now, out int device, out string reason)
        {
            device = -1; reason = "invalid_contract";
            if (m == null || m.schemaVersion != 1 || m.source != "virtual-edge" || m.level < 0 || m.level > 2 ||
                float.IsNaN(m.vibrationNormalized) || float.IsInfinity(m.vibrationNormalized) || m.vibrationNormalized < 0 || m.vibrationNormalized > 1 ||
                float.IsNaN(m.simulationTimeS) || float.IsInfinity(m.simulationTimeS) || m.simulationTimeS < 0 || m.simulationTimeS > now + .001f ||
                m.sequence <= 0 || string.IsNullOrEmpty(m.deviceId)) return false;
            if (m.sessionId != session || m.layoutId != layout) { reason = "foreign_session_or_layout"; return false; }
            if (!m.deviceId.StartsWith("D", StringComparison.Ordinal) || !int.TryParse(m.deviceId.Substring(1), out int n) ||
                n < 1 || n > sequences.Length || m.deviceId != "D" + n.ToString("00")) return false;
            device = n - 1;
            if (topic != m.Topic || m.eventId != session + "-" + m.deviceId + "-" + m.sequence) { reason = "topic_or_event_mismatch"; return false; }
            if (m.sequence <= sequences[device] || m.simulationTimeS < times[device]) { reason = "old_or_duplicate"; return false; }
            if (levels[device] == 2 && m.level != 2) { reason = "closed_latched"; return false; }
            sequences[device] = m.sequence; times[device] = m.simulationTimeS; levels[device] = m.level;
            reason = ""; return true;
        }
    }
}
