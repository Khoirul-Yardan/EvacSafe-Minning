using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SafeMining
{
    // Owned and ticked by MiningSimulation, exclusively on Unity's main thread.
    public sealed class MiningEdgeSession : IDisposable
    {
        [Serializable] sealed class TraceEntry
        {
            public string stage, detail;
            public double monotonicMs;
            public float simulationTimeS;
            public EdgeStatusMessage message;
        }
        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public string LayoutId { get; }
        public HazardSource Source { get; }
        public MiningEdgeSettings Settings => settings.Copy();
        public MiningMqttSettings Broker => broker.Copy();
        public bool DataLoss { get; private set; }
        public bool HadDisconnect { get; private set; }
        public bool DataStale { get; private set; }
        public string TransportStatus { get; private set; }
        public event Action<EdgeStatusMessage> VibrationSampled;
        public event Action<EdgeStatusMessage> EdgeStateChanged;
        public event Action<string> TransportStateChanged;
        public event Action<EdgeStatusMessage> HazardApplied;
        readonly MiningEdgeSettings settings;
        readonly MiningMqttSettings broker;
        readonly MiningVibrationGenerator generator;
        readonly MiningEdgeProcessor[] processors;
        readonly MiningEdgeValidator validator;
        readonly Action<int, int> apply;
        readonly MiningMqttClient mqtt;
        readonly long[] sequences;
        readonly float[] values;
        readonly bool[] received;
        readonly float[] receivedAt;
        readonly List<string> trace = new List<string>();
        readonly Stopwatch clock = Stopwatch.StartNew();
        long tick;
        int generation;
        float time;
        string pendingRoute;
        bool stopped;
        const int MaxTraceEntries = 100000;

        public MiningEdgeSession(HazardSource source, int seed, IEnumerable<ScheduledRockfall> profiles,
            HashSet<Vector2Int> cells, IList<Vector2Int> devices, MiningEdgeSettings settings,
            MiningMqttSettings broker, Action<int, int> apply)
        {
            if (source != HazardSource.LocalEdgeSimulation && source != HazardSource.MqttEdgeSimulation)
                throw new ArgumentException("Sumber sesi edge tidak valid.");
            settings.Validate(); this.settings = settings.Copy(); this.broker = broker.Copy();
            Source = source; this.apply = apply; LayoutId = LayoutHash(cells, devices);
            generator = new MiningVibrationGenerator(seed, profiles, settings);
            processors = new MiningEdgeProcessor[devices.Count]; sequences = new long[devices.Count]; values = new float[devices.Count]; received = new bool[devices.Count];
            receivedAt = new float[devices.Count];
            for (int i = 0; i < processors.Length; i++) processors[i] = new MiningEdgeProcessor(settings);
            validator = new MiningEdgeValidator(SessionId, LayoutId, devices.Count);
            if (source == HazardSource.MqttEdgeSimulation) mqtt = new MiningMqttClient(broker, SessionId, clock);
            TransportStatus = mqtt == null ? "EDGE LOKAL / tanpa MQTT" : "MQTT menghubungkan / menunggu data";
            DataStale = mqtt != null;
        }
        static string LayoutHash(HashSet<Vector2Int> cells, IList<Vector2Int> devices)
        {
            var sorted = new List<Vector2Int>(cells); sorted.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var text = new StringBuilder();
            foreach (var cell in sorted) text.Append(cell.x).Append(':').Append(cell.y).Append(';');
            text.Append('|'); foreach (var cell in devices) text.Append(cell.x).Append(':').Append(cell.y).Append(';');
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }
        public void Pump(bool running, bool paused, float simulationTime, Func<bool> stillRunning)
        {
            if (stopped) return;
            time = simulationTime;
            if (DataLoss && mqtt == null) TransportStatus = "EDGE kehilangan data / ulang sesi";
            if (mqtt != null)
            {
                while (mqtt.TryNotice(out var notice))
                {
                    if (notice.stage == "disconnect") HadDisconnect = true;
                    EdgeStatusMessage message = null;
                    if (notice.stage == "publish_wire" || notice.stage == "puback") message = Parse(notice.detail);
                    Record(notice.stage, message, message == null ? notice.detail : "", notice.monotonicMs);
                }
                if (mqtt.Overflow) DataLoss = true;
                if (!mqtt.Connected) { DataStale = true; Array.Clear(received, 0, received.Length); }
                string status = DataLoss ? "MQTT kehilangan data / ulang sesi" : mqtt.Status + (DataStale ? " / data belum mutakhir" : "");
                if (status != TransportStatus) { TransportStatus = status; TransportStateChanged?.Invoke(status); }
                if (!running && !paused) { Dispose(); return; }
                if (paused || DataLoss) return;
                // Drain only while running. If applying a hazard ends the session, do not apply the rest.
                while (stillRunning() && mqtt.TryReceive(out var delivery))
                {
                    var message = Parse(delivery.payload); Record("receive", message, "", delivery.monotonicMs);
                    if (delivery.retained) { Record("reject", message, "retained_event"); continue; }
                    Apply(delivery.topic, message);
                }
                for (int i = 0; i < received.Length; i++) if (!received[i] || time - receivedAt[i] > 3) DataStale = true;
            }
            if (!running || DataLoss || !stillRunning()) return;
            // Tick indices, rather than frame delta or player movement, determine every sample.
            long target = (long)Math.Floor((simulationTime + .000001) / settings.sampleInterval);
            for (; tick < target && stillRunning(); )
            {
                tick++;
                for (int i = 0; i < processors.Length && stillRunning(); i++)
                {
                    values[i] = generator.Sample(i, tick);
                    bool changed = processors[i].Sample(values[i], settings.sampleInterval);
                    var sample = Message(i, changed);
                    Record("sample", sample, ""); VibrationSampled?.Invoke(sample);
                    if (changed)
                    {
                        var decision = sample;
                        Record("decision", decision, ""); EdgeStateChanged?.Invoke(decision); Send(decision);
                    }
                }
                if (tick % Math.Max(1, (int)Math.Round(1 / settings.sampleInterval)) == 0) Snapshot(stillRunning);
            }
            if (mqtt != null && mqtt.Connected && generation != mqtt.Generation && stillRunning())
            {
                generation = mqtt.Generation; Array.Clear(received, 0, received.Length); DataStale = true;
                Snapshot(stillRunning);
            }
        }
        EdgeStatusMessage Message(int device, bool advance)
        {
            if (advance) sequences[device]++;
            string id = "D" + (device + 1).ToString("00");
            return new EdgeStatusMessage { schemaVersion = 1, sessionId = SessionId, layoutId = LayoutId,
                deviceId = id, sequence = sequences[device], eventId = SessionId + "-" + id + "-" + sequences[device],
                simulationTimeS = (float)(tick * (double)settings.sampleInterval), vibrationNormalized = values[device],
                level = processors[device].Level, source = "virtual-edge" };
        }
        void Snapshot(Func<bool> stillRunning)
        { for (int i = 0; i < processors.Length && stillRunning(); i++) Send(Message(i, true)); }
        void Send(EdgeStatusMessage message)
        {
            if (mqtt == null) { Record("local_delivery", message, ""); Apply(message.Topic, message); }
            else Record(mqtt.Publish(message.Topic, JsonUtility.ToJson(message)) ? "publish_queued" : "publish_unavailable", message, "");
        }
        static EdgeStatusMessage Parse(string json)
            => MiningEdgeJson.Parse(json);
        void Apply(string topic, EdgeStatusMessage message)
        {
            if (!validator.Accept(topic, message, time, out int index, out string reason)) { Record("reject", message, reason); return; }
            pendingRoute = null;
            apply(index, message.level);
            received[index] = true; receivedAt[index] = message.simulationTimeS;
            DataStale = Array.IndexOf(received, false) >= 0;
            for (int i = 0; i < received.Length; i++) if (time - receivedAt[i] > 3) DataStale = true;
            Record("apply", message, "");
            if (pendingRoute != null) Record("route", message, pendingRoute);
            HazardApplied?.Invoke(message);
        }
        // Called synchronously by the simulation's application callback after status + route update.
        public void TraceRoute(int exit, float planningMs)
        { pendingRoute = "exit=" + exit + ";planningMs=" + planningMs.ToString("F4", CultureInfo.InvariantCulture); }
        void Record(string stage, EdgeStatusMessage message, string detail, double monotonicMs = -1)
        {
            if (trace.Count >= MaxTraceEntries) { DataLoss = true; return; }
            trace.Add(JsonUtility.ToJson(new TraceEntry { stage = stage, detail = detail, message = message,
                simulationTimeS = time, monotonicMs = monotonicMs >= 0 ? monotonicMs : clock.Elapsed.TotalMilliseconds }));
        }
        public void Export(string prefix) { File.WriteAllLines(prefix + "_edge.jsonl", trace); }
        public void Dispose()
        {
            if (stopped) return;
            stopped = true; mqtt?.Dispose(); Record("session_stop", null, "");
            TransportStatus = Source == HazardSource.MqttEdgeSimulation ? "MQTT / sesi selesai" : "EDGE LOKAL / sesi selesai";
            TransportStateChanged?.Invoke(TransportStatus);
        }
    }
}
