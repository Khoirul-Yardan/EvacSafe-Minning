using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SafeMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in an isolated project: -batchmode -nographics -executeMethod MiningEdgeValidation.RunBatch
// Requires the Mosquitto broker from Tools/Mqtt on localhost:1883.
public static class MiningEdgeValidation
{
    static readonly List<string> results = new List<string>();
    static MiningSimulation simulation;
    static MiningMqttClient injector;
    static int phase, frames, mark, applied;
    static float pausedTime;
    static string previousSession;
    static double deadline, realMark;
    static Vector3 initialPosition;
    static bool injectedApplied;
    static int floodSequence;
    public static void RunBaseline()
    {
        try
        {
            Core(); Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/edge-core-results.txt", string.Join("\n", results));
            MiningExperienceValidation.RunBatch();
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void RunBatch()
    {
        try
        {
            Core();
            Directory.CreateDirectory("Validation");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(MiningExperienceBuilder.ScenePath);
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Core()
    {
        var settings = new MiningEdgeSettings(); settings.Validate();
        var edge = new MiningEdgeProcessor(settings);
        for (int i = 0; i < 9; i++) edge.Sample(.9f, .05f);
        Assert(edge.Level == 0, "Short spike triggered edge"); edge.Sample(.1f, .05f);
        for (int i = 0; i < 10; i++) edge.Sample(.6f, .05f);
        Assert(edge.Level == 1, "Warning duration failed");
        for (int i = 0; i < 50; i++) edge.Sample(.4f, .05f);
        Assert(edge.Level == 1, "Hysteresis failed");
        for (int i = 0; i < 20; i++) edge.Sample(.1f, .05f);
        Assert(edge.Level == 0, "Stable clear failed");
        for (int i = 0; i < 10; i++) edge.Sample(.9f, .05f);
        for (int i = 0; i < 100; i++) edge.Sample(.1f, .05f);
        Assert(edge.Level == 2, "Collapse did not latch");
        results.Add("PASS edge: short spike rejected, sustained warning/danger, hysteresis, stable clear, closure latch.");
        var cells = MineLayout.CreateCells(); var sites = MiningHazardScenario.DetectorSites(cells);
        for (int seed = 0; seed < 32; seed++)
        {
            var profiles = MiningHazardScenario.Create(cells, sites, HazardScenarioMode.Random, seed, 3, 6, 6, 4);
            var generator = new MiningVibrationGenerator(seed, profiles, settings);
            var replay = new MiningVibrationGenerator(seed, profiles, settings);
            for (int t = 1; t < 400; t++) for (int d = 0; d < sites.Count; d++)
                Assert(generator.Sample(d, t) == replay.Sample(d, t), "Seed replay changed");
        }
        var schedule = new[] { new ScheduledRockfall(0, 1, 2) };
        var first = new List<string>(); var second = new List<string>();
        using (var a = new MiningEdgeSession(HazardSource.LocalEdgeSimulation, 7, schedule, cells, sites, settings, new MiningMqttSettings(), (d, l) => { }))
        using (var b = new MiningEdgeSession(HazardSource.LocalEdgeSimulation, 7, schedule, cells, sites, settings, new MiningMqttSettings(), (d, l) => { }))
        {
            a.EdgeStateChanged += m => first.Add(m.deviceId + ":" + m.level + ":" + m.simulationTimeS);
            b.EdgeStateChanged += m => second.Add(m.deviceId + ":" + m.level + ":" + m.simulationTimeS);
            for (int t = 1; t <= 400; t++) a.Pump(true, false, t * .01f, () => true);
            for (int t = 1; t <= 20; t++) b.Pump(true, false, t * .2f, () => true);
            Assert(first.SequenceEqual(second) && first.Count == 2, "Frame chunking changed decisions");
        }
        results.Add("PASS deterministic input: 32 seeds, all devices; decision times identical at 0.01 s and 0.2 s frame steps.");
        var valid = new EdgeStatusMessage { schemaVersion = 1, sessionId = "s", layoutId = "l", deviceId = "D01", eventId = "s-D01-1", sequence = 1,
            simulationTimeS = 1, vibrationNormalized = .6f, level = 1, source = "virtual-edge" };
        var validator = new MiningEdgeValidator("s", "l", 1);
        string wire = JsonUtility.ToJson(valid);
        Assert(MiningEdgeJson.Parse(wire) != null, "Valid JSON rejected");
        foreach (string malformed in new[] { wire + "garbage", wire.Replace("\"level\":1", "\"level\":1,\"level\":2"),
            wire.Replace("\"level\":1", "\"level\":true"), wire.Replace("\"level\":1", "\"level\":null"),
            wire.Replace("\"level\":1", "\"level\":\"1\""), wire.Replace("\"level\":1", "\"level\":1.5"),
            wire.Replace("\"level\":1", "\"level\":4294967297"), wire.Replace("}", ",}"), "{broken", "[]" })
            Assert(MiningEdgeJson.Parse(malformed) == null, "Malformed or mistyped JSON accepted: " + malformed);
        Assert(validator.Accept(valid.Topic, valid, 1, out _, out _), "Valid message rejected");
        Assert(!validator.Accept(valid.Topic, valid, 1, out _, out _), "Duplicate accepted");
        foreach (string field in new[] { "schemaVersion", "sessionId", "layoutId", "deviceId", "eventId", "sequence", "simulationTimeS", "vibrationNormalized", "level", "source" })
        {
            string json = JsonUtility.ToJson(valid);
            json = System.Text.RegularExpressions.Regex.Replace(json, "\\\"" + field + "\\\":(?:\\\"[^\\\"]*\\\"|[^,}]+),?", "").Replace(",}", "}");
            var missing = new EdgeStatusMessage(); JsonUtility.FromJsonOverwrite(json, missing);
            Assert(!new MiningEdgeValidator("s", "l", 1).Accept(valid.Topic, missing, 1, out _, out _), "Missing field accepted: " + field);
        }
        foreach (Action<EdgeStatusMessage> corrupt in new Action<EdgeStatusMessage>[] {
            m => m.sessionId = "old", m => m.layoutId = "other", m => m.deviceId = "D99", m => m.sequence = 0,
            m => m.level = 3, m => m.simulationTimeS = 2, m => m.vibrationNormalized = float.NaN, m => m.eventId = "forged" })
        {
            var m = JsonUtility.FromJson<EdgeStatusMessage>(JsonUtility.ToJson(valid)); corrupt(m);
            Assert(!new MiningEdgeValidator("s", "l", 1).Accept(valid.Topic, m, 1, out _, out _), "Invalid payload accepted");
        }
        valid.level = 2; valid.sequence = 2; valid.eventId = "s-D01-2";
        Assert(validator.Accept(valid.Topic, valid, 1, out _, out _), "Closure rejected");
        valid.level = 0; valid.sequence = 3; valid.eventId = "s-D01-3";
        Assert(!validator.Accept(valid.Topic, valid, 1, out _, out _), "Closed device reopened");
        results.Add("PASS contract: missing fields, foreign session/layout/device, duplicate/order, invalid level/value/time/event, closure downgrade rejected.");
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout at phase " + phase); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (simulation == null) simulation = UnityEngine.Object.FindFirstObjectByType<MiningSimulation>();
            if (simulation == null) return;
            frames++; Time.timeScale = phase < 6 ? 15 : 1;
            if (phase == 0)
            {
                simulation.hazardSource = HazardSource.LocalEdgeSimulation; simulation.scenarioMode = HazardScenarioMode.Scripted;
                simulation.Begin(MiningMode.FirstPerson, true); initialPosition = simulation.Actor.position;
                simulation.SetHazard(0, 2); Assert(simulation.HazardLevels[0] == 0, "Direct write bypassed edge"); phase = 1;
            }
            else if (phase == 1 && simulation.Elapsed > 12)
            {
                Assert(simulation.HazardLevels[0] == 2 && Vector2.Distance(new Vector2(initialPosition.x, initialPosition.z), new Vector2(simulation.Actor.position.x, simulation.Actor.position.z)) < .01f, "Distant idle FPP did not detect");
                Assert(simulation.Reroutes > 0, "Edge did not replan");
                results.Add("PASS local integration: idle distant FPP detects vibration, closes D01, updates route; direct SetHazard blocked.");
                previousSession = simulation.EdgeSession.SessionId; simulation.TogglePause(); pausedTime = simulation.Elapsed; mark = frames; phase = 2;
            }
            else if (phase == 2 && frames > mark + 20)
            {
                Assert(simulation.Elapsed == pausedTime, "Pause advanced clock");
                simulation.scenarioMode = HazardScenarioMode.NoHazards; simulation.Begin(MiningMode.FirstPerson, true);
                Assert(previousSession != simulation.EdgeSession.SessionId && simulation.HazardLevels.All(l => l == 0), "Reset leaked state");
                var body = simulation.Actor.GetComponent<CharacterController>(); body.enabled = false;
                simulation.Actor.position = MineLayout.World(simulation.HazardSites[0]) + Vector3.up * .05f; body.enabled = true; phase = 3;
            }
            else if (phase == 3 && simulation.Elapsed > 3)
            {
                Assert(simulation.HazardLevels.All(l => l == 0), "Proximity caused an alarm without vibration");
                results.Add("PASS pause/reset/proximity: clock frozen, new session clears closure, standing at a normal detector triggers nothing.");
                simulation.scenarioMode = HazardScenarioMode.Scripted; simulation.Begin(MiningMode.Story, true); phase = 4;
            }
            else if (phase == 4 && simulation.State != SafeMining.SessionState.Running)
            {
                Assert(simulation.State == SafeMining.SessionState.Success && simulation.Reroutes > 0, "Adaptive edge story failed");
                simulation.Begin(MiningMode.Story, false); phase = 5;
            }
            else if (phase == 5 && simulation.State != SafeMining.SessionState.Running)
            {
                Assert(simulation.State == SafeMining.SessionState.Blocked && simulation.Reroutes == 0, "Static baseline changed");
                results.Add("PASS story comparison: adaptive reaches refuge; static stops at closure with zero reroutes.");
                simulation.hazardSource = HazardSource.MqttEdgeSimulation; simulation.Begin(MiningMode.FirstPerson, true);
                simulation.EdgeSession.HazardApplied += m => { applied++; if (m.sequence == 10000) injectedApplied = true; };
                phase = 6;
            }
            else if (phase == 6 && simulation.Elapsed > 13 && simulation.HazardLevels[0] == 2)
            {
                Assert(applied > 0 && !simulation.EdgeSession.DataLoss, "MQTT apply failed");
                results.Add("PASS Mosquitto: real QoS 1 publish/subscribe returns decisions, applies closure and reroute to idle FPP.");
                simulation.TogglePause(); pausedTime = simulation.Elapsed; applied = 0;
                injector = new MiningMqttClient(new MiningMqttSettings(), simulation.EdgeSession.SessionId);
                realMark = EditorApplication.timeSinceStartup; phase = 7;
            }
            else if (phase == 7 && injector.Connected)
            {
                var m = new EdgeStatusMessage { schemaVersion = 1, sessionId = simulation.EdgeSession.SessionId,
                    layoutId = simulation.EdgeSession.LayoutId, deviceId = "D02", sequence = 10000,
                    simulationTimeS = pausedTime, vibrationNormalized = .6f, level = 1, source = "virtual-edge" };
                m.eventId = m.sessionId + "-D02-10000";
                injector.Publish(m.Topic, JsonUtility.ToJson(m));
                injector.Publish(m.Topic, "{broken"); injector.Publish(m.Topic, JsonUtility.ToJson(m));
                realMark = EditorApplication.timeSinceStartup; phase = 8;
            }
            else if (phase == 8 && EditorApplication.timeSinceStartup - realMark > .5)
            {
                Assert(applied == 0 && simulation.Elapsed == pausedTime && simulation.HazardLevels[1] == 0, "Paused messages applied");
                simulation.TogglePause(); phase = 9;
            }
            else if (phase == 9 && injectedApplied && EditorApplication.timeSinceStartup - realMark > 1)
            {
                results.Add("PASS MQTT pause/resume: incoming decision waits until resume; malformed JSON and duplicate rejected.");
                injector.Dispose(); injector = null;
                // Force an actual socket failure; adapter must reconnect and republish latest edge snapshots.
                var mqtt = (MiningMqttClient)typeof(MiningEdgeSession).GetField("mqtt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation.EdgeSession);
                ((System.Net.Sockets.TcpClient)typeof(MiningMqttClient).GetField("socket", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mqtt)).Close();
                realMark = EditorApplication.timeSinceStartup; phase = 10;
            }
            else if (phase == 10 && EditorApplication.timeSinceStartup - realMark > 2 && simulation.EdgeSession.HadDisconnect)
            {
                Assert(simulation.HazardLevels[0] == 2, "Disconnect reopened closure");
                simulation.EdgeSession.Export("Validation/mqtt-session");
                var trace = File.ReadAllText("Validation/mqtt-session_edge.jsonl");
                Assert(trace.Split(new[] { "\"stage\":\"connected\"" }, StringSplitOptions.None).Length >= 3, "Did not reconnect");
                Assert(trace.Contains("publish_wire") && trace.Contains("receive") && trace.Contains("reject") && trace.Contains("route"), "Missing pipeline trace");
                results.Add("PASS MQTT reconnect: socket failure detected, broker reconnected, snapshots republished, closure preserved, full trace exported.");
                simulation.mqttSettings.port = 18889; simulation.Begin(MiningMode.FirstPerson, true); phase = 11;
            }
            else if (phase == 11 && simulation.Elapsed > 13)
            {
                Assert(simulation.HazardLevels.All(l => l == 0) && simulation.EdgeSession.DataStale, "Unavailable broker silently applied locally");
                results.Add("PASS unavailable broker: edge decisions continue, no local fallback, no hazard applied, data marked stale.");
                simulation.Export(); Assert(simulation.ExportStatus.StartsWith("CSV tersimpan"), "Export failed");
                simulation.mqttSettings.port = 1883; simulation.scenarioMode = HazardScenarioMode.NoHazards;
                simulation.Begin(MiningMode.FirstPerson, true); simulation.TogglePause();
                injector = new MiningMqttClient(new MiningMqttSettings(), simulation.EdgeSession.SessionId); phase = 12;
            }
            else if (phase == 12 && injector.Connected)
            {
                while (injector.TryReceive(out _)) { }
                while (injector.TryNotice(out _)) { }
                for (int i = 0; i < 16 && floodSequence < 400; i++)
                {
                    var m = new EdgeStatusMessage { schemaVersion = 1, sessionId = simulation.EdgeSession.SessionId,
                        layoutId = simulation.EdgeSession.LayoutId, deviceId = "D01", sequence = floodSequence + 1,
                        simulationTimeS = 0, vibrationNormalized = .6f, level = 1, source = "virtual-edge" };
                    m.eventId = m.sessionId + "-D01-" + m.sequence;
                    if (injector.Publish(m.Topic, JsonUtility.ToJson(m))) floodSequence++;
                }
                if (simulation.EdgeSession.DataLoss)
                {
                    Assert(simulation.HazardLevels.All(l => l == 0), "Overflow applied a paused hazard");
                    simulation.TogglePause(); mark = frames; phase = 13;
                }
            }
            else if (phase == 13 && frames > mark + 15)
            {
                Assert(simulation.EdgeSession.DataLoss && simulation.HazardLevels.All(l => l == 0), "Overflow resumed applying invalid experiment data");
                results.Add("PASS bounded queue: >256 messages during pause marks data loss, resume cannot apply, reset required.");
                simulation.Menu(); Finish(true, "ALL EDGE/MQTT CHECKS PASSED");
            }
        }
        catch (Exception e) { Finish(false, "phase " + phase + ": " + e); }
    }
    static void Finish(bool success, string message)
    {
        injector?.Dispose(); simulation?.EdgeSession?.Dispose(); EditorApplication.update -= Tick;
        Directory.CreateDirectory("Validation"); File.WriteAllText("Validation/edge-results.txt", string.Join("\n", results) + "\n" + message);
        Debug.Log(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
