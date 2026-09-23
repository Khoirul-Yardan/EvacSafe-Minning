using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SafeMining
{
    // Optional transport. The offline simulation has no broker/server dependency.
    public class MiningTelemetry : MonoBehaviour
    {
        public MiningSimulation Simulation;
        public bool connectOnStart;
        public string endpoint = "ws://127.0.0.1:8765";
        public string Status { get; private set; } = "Offline / local simulation";
        ClientWebSocket socket;
        CancellationTokenSource cancellation;
        Task pendingSend;
        readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        float nextSend;

        [Serializable] public class Snapshot
        {
            public string type = "telemetry";
            public string mode, navigation, state, scenario;
            public int seed;
            public Vector2Int[] detectorCells;
            public float simulationTime, x, z, exposureSeconds, planningMs;
            public int reroutes;
            public int[] hazardLevels;
        }
        [Serializable] public class Command { public string type; public int index; public int level; }
        void Start() { if (connectOnStart) Connect(); }
        [ContextMenu("Connect WebSocket")]
        public async void Connect()
        {
            if (socket != null) return;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri uri) || (uri.Scheme != "ws" && uri.Scheme != "wss")) { Status = "Invalid WebSocket endpoint"; return; }
            cancellation = new CancellationTokenSource(); socket = new ClientWebSocket();
            try
            {
                Status = "Connecting"; await socket.ConnectAsync(uri, cancellation.Token);
                Status = "Connected"; await ReceiveLoop(cancellation.Token);
            }
            catch (Exception e) { Status = "Disconnected: " + e.Message; }
            finally { socket?.Dispose(); socket = null; cancellation?.Dispose(); cancellation = null; }
        }
        async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[4096]; var text = new StringBuilder();
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close) { Status = "Server closed connection"; break; }
                if (result.MessageType != WebSocketMessageType.Text) continue;
                text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (text.Length > 8192) throw new InvalidOperationException("Message exceeds 8 KB");
                if (result.EndOfMessage) { messages.Enqueue(text.ToString()); text.Clear(); }
            }
        }
        void Update()
        {
            while (messages.TryDequeue(out string json))
            {
                try { var command = JsonUtility.FromJson<Command>(json); if (command != null && command.type == "hazard" && Simulation.State == SessionState.Running) Simulation.SetHazard(command.index, command.level); }
                catch (Exception e) { Debug.LogWarning("Invalid telemetry command: " + e.Message); }
            }
            if (pendingSend != null && pendingSend.IsFaulted) { Status = "Send failed"; _ = pendingSend.Exception; pendingSend = null; }
            if (socket == null || socket.State != WebSocketState.Open || Simulation == null || Simulation.Actor == null || Time.unscaledTime < nextSend || (pendingSend != null && !pendingSend.IsCompleted)) return;
            nextSend = Time.unscaledTime + .5f;
            var s = Simulation;
            var snapshot = new Snapshot { seed = s.ActiveSeed, scenario = s.ActiveScenario.ToString(), detectorCells = s.HazardSites.ToArray(), mode = s.Mode.ToString(), navigation = s.Adaptive ? "adaptive" : "static", state = s.State.ToString(), simulationTime = s.Elapsed,
                x = s.Actor.position.x, z = s.Actor.position.z, exposureSeconds = s.Exposure, planningMs = s.ResponseMs, reroutes = s.Reroutes, hazardLevels = s.HazardLevels };
            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));
            pendingSend = socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cancellation.Token);
        }
        void OnDestroy() { cancellation?.Cancel(); socket?.Abort(); }
    }
}
