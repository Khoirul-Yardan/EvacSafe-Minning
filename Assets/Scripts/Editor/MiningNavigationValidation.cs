using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SafeMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SessionState = SafeMining.SessionState;

// Requires both Mosquitto and the current Python edge. Does not stop or alter either service.
public static class MiningNavigationValidation
{
    static MiningSimulation simulation;
    static readonly List<string> results = new List<string>();
    static double deadline;
    static int phase;
    static Vector3 heldPosition;
    static void Check(bool value, string detail) { if (!value) throw new Exception(detail); }
    public static void RunBatch()
    {
        Directory.CreateDirectory("Validation");
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene(MiningExperienceBuilder.ScenePath);
        deadline = EditorApplication.timeSinceStartup + 150;
        EditorApplication.update += Tick; EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout phase " + phase); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (simulation == null)
            {
                simulation = UnityEngine.Object.FindFirstObjectByType<MiningSimulation>();
                if (simulation == null) return;
                simulation.hazardSource = HazardSource.MqttEdgeSimulation;
                simulation.mqttSettings.host = "127.0.0.1"; simulation.mqttSettings.port = 1883;
                simulation.scenarioMode = HazardScenarioMode.Scripted;
                simulation.Begin(MiningMode.Story, true); return;
            }
            if (phase < 2) Check(simulation.State == SessionState.Running, simulation.TerminalReason + ": " + simulation.Dialogue);
            if (phase == 0 && simulation.StoryHolding)
            {
                Check(simulation.Travelled > 1 && simulation.EdgeRouteSession.AppliedRoutes >= 1, "No initial route/movement from Python");
                Check(!simulation.EdgeSession.DataStale && !simulation.EdgeSession.DataLoss, "Python sensor data unavailable");
                results.Add("PASS real MQTT startup: broker + Python v2 return route; worker moves before first tremor; sensor status current.");
                heldPosition = simulation.Actor.position; phase = 1;
            }
            else if (phase == 1)
            {
                if (simulation.StoryHolding)
                    Check(Vector2.Distance(new Vector2(heldPosition.x, heldPosition.z), new Vector2(simulation.Actor.position.x, simulation.Actor.position.z)) < .01f, "MQTT inspection moved worker");
                else
                {
                    Check(simulation.HazardLevels[0] == 2 && simulation.EdgeRouteSession.AppliedRoutes >= 2, "No Python hazard reroute");
                    Check(simulation.Route.All(cell => !simulation.Blocked.Contains(cell)), "Remote route crosses landslide");
                    simulation.EdgeRouteSession.Export("Validation/navigation-live"); simulation.EdgeSession.Export("Validation/navigation-live");
                    results.Add("PASS real MQTT story: Python confirms closure, actor holds, v2 route arrives and avoids landslide, then story resumes.");
                    simulation.mqttSettings.port = 18889; simulation.scenarioMode = HazardScenarioMode.NoHazards;
                    simulation.Begin(MiningMode.Story, true); phase = 2;
                }
            }
            else if (phase == 2 && simulation.State == SessionState.Blocked)
            {
                Check(simulation.TerminalReason == "navigation_broker_unavailable", "Offline broker error is ambiguous");
                Check(simulation.Travelled == 0 && simulation.Dialogue.Contains("broker MQTT"), "Offline failure missing actionable message");
                results.Add("PASS unavailable broker: explicit broker error, no movement and no local fallback.");
                Finish(true, "Navigation integration passed.");
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Finish(bool pass, string detail)
    {
        EditorApplication.update -= Tick;
        results.Add((pass ? "PASS " : "FAIL ") + detail);
        File.WriteAllLines("Validation/navigation-results.txt", results);
        Debug.Log(string.Join("\n", results)); EditorApplication.Exit(pass ? 0 : 1);
    }
}
