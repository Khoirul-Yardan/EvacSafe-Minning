using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace SafeMining
{
    // Async edge routing; main-thread ownership, immutable snapshots, no local planner fallback.
    public sealed class MiningEdgeRouteSession : IDisposable
    {
        [Serializable] sealed class RouteTrace
        {
            public string stage, detail;
            public double monotonicMs;
            public long sequence, stateRevision;
            public EdgeRouteRequest request;
            public EdgeRouteResponse response;
        }
        readonly MiningMqttClient mqtt;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly string session, layout;
        readonly Action<string, string> traceNavigation;
        readonly List<string> trace = new List<string>();
        EdgeRouteRequest pending;
        long sequence;
        int generation;
        bool published, stopped;
        double requestedAt, publishedAt, pausedAt = -1;
        public bool Pending => pending != null;
        public bool HasRoute { get; private set; }
        public bool Failed { get; private set; }
        public bool DataLoss { get; private set; }
        public bool HadDisconnect { get; private set; }
        public bool NeedsRequest { get; private set; }
        public bool HadPause { get; private set; }
        public string FailureReason { get; private set; }
        public float LastPlanningMs { get; private set; }
        public float LastRoundTripMs { get; private set; }
        public float MaxPlanningMs { get; private set; }
        public float MaxRoundTripMs { get; private set; }
        public int AppliedRoutes { get; private set; }
        public string Status => Failed ? "NAVIGASI EDGE / " + FailureReason :
            Pending ? "NAVIGASI EDGE / menunggu rute" : HasRoute ? "NAVIGASI EDGE / rute diterapkan" : "NAVIGASI EDGE / menghubungkan";
        public const float TimeoutSeconds = 5f;

        public MiningEdgeRouteSession(MiningMqttSettings broker, string session, string layout, Action<string, string> traceNavigation)
        {
            this.session = session; this.layout = layout; this.traceNavigation = traceNavigation;
            mqtt = new MiningMqttClient(broker, session, clock, navigation: true);
        }
        public void Request(Vector2Int start, HashSet<Vector2Int> cells, IList<Vector2Int> devices,
            HashSet<Vector2Int> blocked, Dictionary<Vector2Int, float> risk, float penalty,
            bool adaptive, long revision, float simulationTime)
        {
            if (stopped || Failed) return;
            if (pending != null && pending.startX == start.x && pending.startZ == start.y && pending.stateRevision == revision) return;
            if (pending != null) Record("superseded", pending, null, "new_position_or_hazard_revision");
            pending = MiningRouteContract.Create(session, layout, ++sequence, revision, simulationTime,
                start, cells, devices, blocked, risk, penalty, adaptive);
            NeedsRequest = false; published = false; requestedAt = clock.Elapsed.TotalMilliseconds;
            Record("request", pending, null, "");
            Publish();
        }
        void Publish()
        {
            if (pending == null || !mqtt.Connected) return;
            if (mqtt.Publish(pending.Topic, JsonUtility.ToJson(pending)))
            {
                published = true; publishedAt = clock.Elapsed.TotalMilliseconds;
                Record("request_queued", pending, null, "");
            }
        }
        public void Pump(bool running, bool paused, Func<EdgeRouteResponse, bool> apply)
        {
            if (stopped) return;
            while (mqtt.TryNotice(out var notice))
            {
                if (notice.stage == "disconnect") HadDisconnect = true;
                Record(notice.stage, pending, null, notice.detail, notice.monotonicMs);
            }
            if (!running && !paused) { Dispose(); return; }
            if (mqtt.Overflow) { DataLoss = true; Fail("navigation_data_loss"); }
            if (paused)
            {
                if (pausedAt < 0) { pausedAt = clock.Elapsed.TotalMilliseconds; HadPause = true; Record("pause", pending, null, ""); }
                return;
            }
            if (pausedAt >= 0)
            {
                requestedAt += clock.Elapsed.TotalMilliseconds - pausedAt; pausedAt = -1;
                Record("resume", pending, null, "");
            }
            if (Failed) return;
            // After reconnect, replace correlation id to reject deliveries queued on the old connection.
            if (mqtt.Connected && generation != mqtt.Generation)
            {
                generation = mqtt.Generation;
                if (pending != null)
                {
                    pending.sequence = ++sequence; published = false;
                    Record("request_reconnect", pending, null, "");
                }
            }
            if (pending != null && !published) Publish();
            while (mqtt.TryReceive(out var delivery))
            {
                var result = MiningRouteContract.Parse(delivery.payload);
                Record("response_receive", pending, result, "", delivery.monotonicMs);
                string reason = "retained_response";
                if (delivery.retained || !MiningRouteContract.Validate(delivery.topic, result, pending, out reason))
                { Record("response_reject", pending, result, reason); continue; }
                if (!apply(result))
                {
                    Record("response_stale", pending, result, "position_or_hazard_revision_changed");
                    pending = null; NeedsRequest = true; continue;
                }
                LastPlanningMs = result.planningMs;
                LastRoundTripMs = (float)(clock.Elapsed.TotalMilliseconds - publishedAt);
                MaxPlanningMs = Mathf.Max(MaxPlanningMs, LastPlanningMs);
                MaxRoundTripMs = Mathf.Max(MaxRoundTripMs, LastRoundTripMs);
                HasRoute = result.outcome == "route";
                if (HasRoute) AppliedRoutes++;
                Record("response_applied", pending, result, ""); pending = null;
            }
            if (pending != null && clock.Elapsed.TotalMilliseconds - requestedAt > TimeoutSeconds * 1000)
                Fail("navigation_timeout");
        }
        void Fail(string reason)
        {
            if (Failed) return;
            Failed = true; FailureReason = reason; Record("navigation_failure", pending, null, reason);
        }
        void Record(string stage, EdgeRouteRequest request, EdgeRouteResponse response, string detail, double monotonicMs = -1)
        {
            if (trace.Count >= 10000) { DataLoss = true; Failed = true; FailureReason = "navigation_trace_limit"; return; }
            trace.Add(JsonUtility.ToJson(new RouteTrace { stage = stage, detail = detail,
                monotonicMs = monotonicMs >= 0 ? monotonicMs : clock.Elapsed.TotalMilliseconds,
                sequence = response?.sequence ?? request?.sequence ?? 0,
                stateRevision = response?.stateRevision ?? request?.stateRevision ?? 0,
                request = stage == "request" || stage == "request_reconnect" ? request : null, response = response }));
            traceNavigation?.Invoke(stage, "request=" + (response?.sequence ?? request?.sequence ?? 0) +
                ";revision=" + (response?.stateRevision ?? request?.stateRevision ?? 0) + ";" + detail);
        }
        public void Export(string prefix) => File.WriteAllLines(prefix + "_navigation.jsonl", trace);
        public void Dispose()
        {
            if (stopped) return;
            stopped = true; Record("navigation_stop", pending, null, ""); mqtt.Dispose();
        }
    }
}
