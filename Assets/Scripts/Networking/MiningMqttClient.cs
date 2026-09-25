using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SafeMining
{
    [Serializable]
    public class MiningMqttSettings
    {
        public string host = "127.0.0.1";
        public int port = 1883;
        public MiningMqttSettings Copy() => (MiningMqttSettings)MemberwiseClone();
    }

    // Deliberately limited MQTT 3.1.1 adapter for the local TCP demo broker.
    // Clean sessions, QoS 1 publish/subscribe, no retained events, bounded queues, reconnect snapshots.
    // All socket IO lives on one worker. No Unity API or user callbacks execute there.
    public sealed class MiningMqttClient : IDisposable
    {
        public sealed class Delivery
        {
            public string topic, payload;
            public bool retained;
            public double monotonicMs;
        }
        public sealed class Notice
        {
            public string stage, detail;
            public double monotonicMs;
        }
        sealed class Pending { public byte[] body; public string payload; public double sent; public int attempts; }
        const int Capacity = 256;
        readonly ConcurrentQueue<Delivery> incoming = new ConcurrentQueue<Delivery>();
        readonly ConcurrentQueue<Delivery> outgoing = new ConcurrentQueue<Delivery>();
        readonly ConcurrentQueue<Notice> notices = new ConcurrentQueue<Notice>();
        readonly MiningMqttSettings settings;
        readonly string subscription, clientId;
        readonly Thread worker;
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly Stopwatch clock;
        volatile bool stopping, connected, overflow;
        volatile string status = "MQTT menghubungkan";
        TcpClient socket;
        int generation;
        public bool Connected => connected;
        public bool Overflow => overflow;
        public int Generation => Volatile.Read(ref generation);
        public string Status => status;
        public bool TryReceive(out Delivery delivery) => incoming.TryDequeue(out delivery);
        public bool TryNotice(out Notice notice) => notices.TryDequeue(out notice);

        public MiningMqttClient(MiningMqttSettings settings, string session, Stopwatch clock = null)
        {
            if (string.IsNullOrWhiteSpace(settings.host) || settings.port < 1 || settings.port > 65535)
                throw new ArgumentException("Broker MQTT tidak valid.");
            this.clock = clock ?? Stopwatch.StartNew();
            this.settings = settings.Copy(); subscription = "safe-mining/v1/" + session + "/edge/+/status";
            clientId = "sm" + Guid.NewGuid().ToString("N").Substring(0, 20);
            worker = new Thread(Run) { IsBackground = true, Name = "SafeMining MQTT" }; worker.Start();
        }
        public bool Publish(string topic, string payload)
        {
            if (!connected || stopping) return false;
            if (outgoing.Count >= Capacity) { overflow = true; return false; }
            outgoing.Enqueue(new Delivery { topic = topic, payload = payload }); return true;
        }
        void NoticeEvent(string stage, string detail)
        {
            if (notices.Count >= Capacity * 4) { overflow = true; return; }
            notices.Enqueue(new Notice { stage = stage, detail = detail, monotonicMs = clock.Elapsed.TotalMilliseconds });
        }
        void Run()
        {
            try
            {
                while (!stopping)
                {
                    try { ConnectAndPump(); }
                    catch (Exception e) { if (!stopping) { status = "MQTT terputus / data basi"; NoticeEvent("disconnect", e.GetType().Name + ": " + e.Message); } }
                    finally
                    {
                        connected = false; socket?.Close();
                        while (outgoing.TryDequeue(out _)) { }
                    }
                    if (stop.WaitOne(1000)) break;
                }
            }
            finally { stop.Dispose(); }
        }
        void ConnectAndPump()
        {
            socket = new TcpClient(); socket.NoDelay = true;
            var connect = socket.ConnectAsync(settings.host, settings.port);
            // Bound cancellation and connection time without blocking the Unity main thread.
            double started = clock.Elapsed.TotalSeconds;
            while (!connect.IsCompleted)
            {
                if (stopping || clock.Elapsed.TotalSeconds - started > 3) { socket.Close(); throw new IOException("Connect timeout"); }
                stop.WaitOne(20);
            }
            connect.GetAwaiter().GetResult();
            var stream = socket.GetStream(); stream.ReadTimeout = 2000; stream.WriteTimeout = 2000;
            var body = new List<byte>(); Utf8(body, "MQTT"); body.AddRange(new byte[] { 4, 2, 0, 10 }); Utf8(body, clientId);
            Write(stream, 0x10, body.ToArray());
            var reply = Read(stream, out int header);
            if (header != 0x20 || reply.Length != 2 || reply[0] != 0 || reply[1] != 0) throw new IOException("Broker rejected CONNECT");
            body.Clear(); U16(body, 1); Utf8(body, subscription); body.Add(1); Write(stream, 0x82, body.ToArray());
            reply = Read(stream, out header);
            if (header != 0x90 || reply.Length != 3 || reply[0] != 0 || reply[1] != 1 || reply[2] != 1)
                throw new IOException("Broker must grant QoS 1 subscription");
            Interlocked.Increment(ref generation); connected = true; status = "MQTT terhubung"; NoticeEvent("connected", subscription);
            var pending = new Dictionary<int, Pending>(); int packetId = 1;
            double lastPing = clock.Elapsed.TotalSeconds, pingSent = -1;
            while (!stopping)
            {
                double now = clock.Elapsed.TotalSeconds;
                if (socket.Client.Poll(0, SelectMode.SelectRead) && socket.Available == 0) throw new IOException("Broker closed socket");
                for (int readCount = 0; readCount < 64 && stream.DataAvailable; readCount++)
                {
                    reply = Read(stream, out header); int type = header >> 4;
                    if (type == 3)
                    {
                        int qos = (header >> 1) & 3;
                        if (qos > 1 || reply.Length < 2) throw new IOException("Unsupported PUBLISH");
                        int length = reply[0] * 256 + reply[1], offset = 2 + length;
                        if (length == 0 || offset + (qos == 1 ? 2 : 0) > reply.Length) throw new IOException("Malformed PUBLISH");
                        string topic = StrictUtf8.GetString(reply, 2, length);
                        int id = 0;
                        if (qos == 1) { id = reply[offset] * 256 + reply[offset + 1]; offset += 2; if (id == 0) throw new IOException("Invalid packet ID"); }
                        string payload = StrictUtf8.GetString(reply, offset, reply.Length - offset);
                        if (incoming.Count >= Capacity) overflow = true;
                        else incoming.Enqueue(new Delivery { topic = topic, payload = payload, retained = (header & 1) != 0, monotonicMs = clock.Elapsed.TotalMilliseconds });
                        if (qos == 1) Write(stream, 0x40, new byte[] { (byte)(id >> 8), (byte)id });
                    }
                    else if (header == 0x40 && reply.Length == 2)
                    {
                        int id = reply[0] * 256 + reply[1];
                        if (pending.TryGetValue(id, out var acknowledged)) { pending.Remove(id); NoticeEvent("puback", acknowledged.payload); }
                    }
                    else if (header == 0xD0 && reply.Length == 0) pingSent = -1;
                    else throw new IOException("Unexpected MQTT packet");
                }
                for (int sent = 0; sent < 16 && pending.Count < 64 && outgoing.TryDequeue(out var delivery); sent++)
                {
                    do { packetId = packetId % 65535 + 1; } while (pending.ContainsKey(packetId));
                    body.Clear(); Utf8(body, delivery.topic); U16(body, packetId); body.AddRange(StrictUtf8.GetBytes(delivery.payload));
                    var bytes = body.ToArray(); Write(stream, 0x32, bytes);
                    pending[packetId] = new Pending { body = bytes, payload = delivery.payload, sent = now, attempts = 1 };
                    NoticeEvent("publish_wire", delivery.payload);
                }
                foreach (var item in pending.Values)
                    if (now - item.sent > 2)
                    {
                        if (item.attempts >= 3) throw new IOException("PUBACK timeout");
                        Write(stream, 0x3A, item.body); item.sent = now; item.attempts++;
                    }
                if (pingSent >= 0 && now - pingSent > 5) throw new IOException("PINGRESP timeout");
                if (now - lastPing >= 3 && pingSent < 0)
                { Write(stream, 0xC0, new byte[0]); lastPing = pingSent = now; }
                stop.WaitOne(5);
            }
        }
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        static void U16(List<byte> bytes, int n) { bytes.Add((byte)(n >> 8)); bytes.Add((byte)n); }
        static void Utf8(List<byte> bytes, string s) { var data = StrictUtf8.GetBytes(s); U16(bytes, data.Length); bytes.AddRange(data); }
        static void Write(Stream stream, int header, byte[] body)
        {
            var packet = new List<byte> { (byte)header }; int n = body.Length;
            do { int digit = n % 128; n /= 128; packet.Add((byte)(digit | (n > 0 ? 128 : 0))); } while (n > 0);
            packet.AddRange(body); var bytes = packet.ToArray(); stream.Write(bytes, 0, bytes.Length);
        }
        static byte[] Read(Stream stream, out int header)
        {
            header = stream.ReadByte(); if (header < 0) throw new EndOfStreamException();
            int size = 0, multiplier = 1, count = 0, digit;
            do
            {
                digit = stream.ReadByte(); if (digit < 0) throw new EndOfStreamException();
                size += (digit & 127) * multiplier; multiplier *= 128; count++;
                if (size > 16384 || (count == 4 && digit >= 128)) throw new IOException("MQTT packet exceeds demo limit");
            } while (digit >= 128);
            var body = new byte[size]; int offset = 0;
            while (offset < size) { int read = stream.Read(body, offset, size - offset); if (read == 0) throw new EndOfStreamException(); offset += read; }
            return body;
        }
        public void Dispose()
        {
            if (stopping) return;
            stopping = true; connected = false; stop.Set(); socket?.Close();
            // Worker owns its wait handle; shutdown never waits on network IO in Unity.
        }
    }
}
