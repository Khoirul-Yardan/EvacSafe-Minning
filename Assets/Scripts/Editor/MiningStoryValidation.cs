using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SafeMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SessionState = SafeMining.SessionState;

// Run in the isolated validation project; exercises actual movement, edge input and story stages.
public static class MiningStoryValidation
{
    static MiningSimulation simulation;
    static readonly List<string> results = new List<string>();
    static double deadline;
    static Vector3 stopped;
    static bool held, confirmed, mapping, resumed;
    static int reroutesAtStop;
    static bool captured;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Invoke(string name, params object[] args) => typeof(MiningSimulation)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(simulation, args);
    static void At(float time)
    {
        typeof(MiningSimulation).GetProperty("Elapsed").SetValue(simulation, time);
        Invoke("TickStory");
    }
    static Vector3 Flat(Vector3 position) => new Vector3(position.x, 0, position.z);
    public static void RunBatch()
    {
        Directory.CreateDirectory("Validation");
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene(MiningExperienceBuilder.ScenePath);
        deadline = EditorApplication.timeSinceStartup + 160;
        EditorApplication.update += Tick; EditorApplication.isPlaying = true;
    }
    static void Cases()
    {
        MiningRoutingValidation.Core(results);
        simulation.Menu(); simulation.scenarioMode = HazardScenarioMode.Random; simulation.randomSeedOnLaunch = true;
        var learning = (MiningLearningPanel)typeof(MiningHUD).GetField("learningPanel", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(simulation.GetComponent<MiningHUD>());
        learning.ShowPre(MiningMode.Story, true);
        typeof(MiningLearningPanel).GetMethod("SelectAnswer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(learning, new object[] { 0 });
        typeof(MiningLearningPanel).GetMethod("Continue", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(learning, null);
        Check(simulation.ActiveScenario == HazardScenarioMode.Random && simulation.LearningSession, "Guided start overrode Random choice");
        int randomSeed = simulation.ActiveSeed;
        string schedule = string.Join(";", simulation.Schedule.ConvertAll(s => s.detectorIndex + ":" + s.warningTime));
        simulation.Begin(MiningMode.Story, false, true);
        Check(simulation.ActiveSeed == randomSeed && schedule == string.Join(";", simulation.Schedule.ConvertAll(s => s.detectorIndex + ":" + s.warningTime)), "Retry changed random experiment");
        simulation.Menu(); simulation.BeginNewSession(MiningMode.Story, true, true);
        Check(simulation.ActiveSeed != randomSeed, "New random session reused seed");
        simulation.Menu(); simulation.scenarioMode = HazardScenarioMode.NoHazards;
        simulation.BeginNewSession(MiningMode.Story, true, true);
        Check(simulation.ActiveScenario == HazardScenarioMode.NoHazards && simulation.Schedule.Count == 0, "Guided start overrode NoHazards choice");
        results.Add("PASS actual guided start preserves Random; retry keeps seed/schedule; new menu session gets new seed; guided NoHazards stays empty.");
        simulation.hazardSource = HazardSource.LegacyTimeline;
        simulation.scenarioMode = HazardScenarioMode.NoHazards;
        simulation.Begin(MiningMode.Story, true);
        var route = string.Join(";", simulation.Route);
        simulation.SetHazard(0, 1);
        At(3);
        Check(simulation.StoryHolding && simulation.StoryStep == StoryStage.Checking, "Warning did not hold for edge confirmation");
        Check(string.Join(";", simulation.Route) == route && simulation.Reroutes == 0, "Warning rerouted early");
        simulation.SetHazard(0, 2); simulation.SetHazard(1, 2);
        At(8);
        Check(simulation.StoryHolding && simulation.Reroutes == 0, "Multiple closure shots bypassed waiting");
        At(13.2f); At(15);
        Check(simulation.StoryStep == StoryStage.Ready && simulation.Route.TrueForAll(c => !simulation.Blocked.Contains(c)), "Route includes confirmed closures");
        At(17);
        Check(!simulation.StoryHolding, "Worker did not resume after mapping");
        results.Add("PASS simultaneous closures: no early reroute; wait for both shots; map avoids closures; resume after ready.");

        simulation.Begin(MiningMode.Story, true);
        simulation.SetHazard(0, 1); At(3); simulation.SetHazard(0, 0); At(4); At(6.1f); At(8); At(10);
        Check(!simulation.StoryHolding && simulation.Blocked.Count == 0, "Normal recovery stuck or invented collapse");
        simulation.SetHazard(1, 1); Check(simulation.StoryHolding, "Second tremor ignored");
        simulation.Begin(MiningMode.Story, true);
        Check(!simulation.StoryHolding, "Reset leaked story hold");
        results.Add("PASS warning clears without collapse; repeated incident holds again; reset clears story state.");

        simulation.Begin(MiningMode.Story, false);
        int routeSite = simulation.HazardSites.FindIndex(c => simulation.Route.Contains(c));
        Check(routeSite >= 0, "Missing route hazard fixture");
        simulation.SetHazard(routeSite, 2); At(2); At(5.2f); At(7);
        Check(simulation.State == SessionState.Blocked && simulation.Reroutes == 0, "Static mode rerouted or resumed through closure");
        simulation.Begin(MiningMode.FirstPerson, true); simulation.SetHazard(0, 1);
        Check(!simulation.StoryHolding, "Story hold affected FPP");
        results.Add("PASS static route stays fixed and stops; FPP unaffected.");

        simulation.hazardSource = HazardSource.LocalEdgeSimulation;
        simulation.Begin(MiningMode.Story, true);
        Invoke("ObserveStoryVibration", new EdgeStatusMessage { deviceId = "D01", vibrationNormalized = .6f, simulationTimeS = 1 });
        Invoke("ObserveStoryReport", new EdgeStatusMessage { deviceId = "D01", level = 0, simulationTimeS = 1 });
        At(3);
        Check(simulation.StoryStep == StoryStage.Checking, "Old normal snapshot released tremor hold");
        Invoke("ObserveStoryReport", new EdgeStatusMessage { deviceId = "D01", level = 0, simulationTimeS = 3 });
        At(3);
        Check(simulation.StoryStep == StoryStage.Confirmed, "Fresh stable normal report ignored");
        results.Add("PASS raw vibration stops before decision; outdated normal snapshot cannot release hold.");
        typeof(MiningEdgeSession).GetProperty("DataStale").SetValue(simulation.EdgeSession, true);
        At(10);
        Check(simulation.StoryHolding && simulation.StoryStep == StoryStage.Checking, "Stale edge data allowed mapping");
        typeof(MiningEdgeSession).GetProperty("DataStale").SetValue(simulation.EdgeSession, false);
        At(11); At(14); At(16);
        Invoke("ObserveStoryVibration", new EdgeStatusMessage { deviceId = "D02", vibrationNormalized = .6f, simulationTimeS = 16 });
        Check(simulation.StoryHolding && simulation.StoryStep == StoryStage.Checking, "New vibration did not interrupt route readiness");
        results.Add("PASS stale edge data holds worker; a new tremor interrupts route readiness.");
        simulation.scenarioMode = HazardScenarioMode.Scripted;
        simulation.Begin(MiningMode.Story, true);
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (simulation == null)
            {
                simulation = UnityEngine.Object.FindFirstObjectByType<MiningSimulation>();
                if (simulation == null) return;
                Cases(); return;
            }
            if (!held && simulation.StoryHolding)
            {
                Check(simulation.Travelled > 1, "Story did not walk before first tremor");
                held = true; stopped = Flat(simulation.Actor.position); reroutesAtStop = simulation.Reroutes;
                float time = simulation.Elapsed;
                simulation.TogglePause(); Invoke("Update");
                Check(simulation.Elapsed == time && simulation.StoryHolding, "Pause advanced story");
                simulation.TogglePause();
            }
            if (held && simulation.StoryHolding)
            {
                Check(Vector3.Distance(stopped, Flat(simulation.Actor.position)) < .01f, "Worker walked during inspection/cutscene/mapping");
                if (!mapping) Check(simulation.Reroutes == reroutesAtStop, "Reroute occurred before mapping phase");
                confirmed |= simulation.HazardLevels[0] == 2;
                mapping |= simulation.StoryStep == StoryStage.Mapping;
                if (!captured && simulation.Elapsed > 11 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                { Capture(); captured = true; }
            }
            if (held && !simulation.StoryHolding) resumed = true;
            if (resumed && Vector3.Distance(stopped, Flat(simulation.Actor.position)) > .2f)
            {
                Check(confirmed && mapping, "Missing confirmation/mapping stage");
                Check(simulation.Route.TrueForAll(c => !simulation.Blocked.Contains(c)), "Resumed route contains closed corridor");
                results.Add("PASS live LocalEdgeSimulation: walks first, stops on sample, edge/collapse clocks continue, pause freezes, remains still during inspection and mapping, resumes along safe route.");
                Finish(true, "All story checks passed.");
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Capture()
    {
        var camera = simulation.ViewCamera;
        var canvas = simulation.GetComponentInChildren<Canvas>();
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance;
        var scaler = canvas.GetComponent<CanvasScaler>(); var oldScale = scaler.uiScaleMode; float oldFactor = scaler.scaleFactor;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var rt = new RenderTexture(1600, 900, 24);
        var picture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .3f;
            camera.targetTexture = rt; scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1; scaler.enabled = false; scaler.enabled = true;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
            picture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); picture.Apply();
            File.WriteAllBytes("Validation/story-inspection.png", picture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = oldActive; camera.targetTexture = oldTarget;
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane;
            scaler.uiScaleMode = oldScale; scaler.scaleFactor = oldFactor; scaler.enabled = false; scaler.enabled = true;
            rt.Release(); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(picture);
        }
    }
    static void Finish(bool pass, string detail)
    {
        EditorApplication.update -= Tick;
        results.Add((pass ? "PASS " : "FAIL ") + detail);
        File.WriteAllLines("Validation/story-flow-results.txt", results);
        Debug.Log(string.Join("\n", results)); EditorApplication.Exit(pass ? 0 : 1);
    }
}
