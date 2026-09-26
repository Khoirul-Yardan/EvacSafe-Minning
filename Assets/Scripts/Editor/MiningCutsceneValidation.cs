using System;
using System.Collections.Generic;
using System.IO;
using SafeMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Run in the isolated validation project with graphics enabled to capture the actual inset.
public static class MiningCutsceneValidation
{
    static MiningSimulation simulation;
    static MiningLandslideCutscene cutscene;
    static readonly List<string> results = new List<string>();
    static int phase, frames, mark;
    static double deadline;
    static float paused;
    static Vector3 actorPosition;

    public static void RunBatch()
    {
        Directory.CreateDirectory("Validation");
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene(MiningExperienceBuilder.ScenePath);
        deadline = EditorApplication.timeSinceStartup + 150;
        EditorApplication.update += Tick; EditorApplication.isPlaying = true;
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout phase " + phase); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (simulation == null) simulation = UnityEngine.Object.FindFirstObjectByType<MiningSimulation>();
            if (simulation == null) return;
            cutscene = simulation.GetComponent<MiningLandslideCutscene>();
            if (cutscene == null) return;
            frames++; Time.timeScale = 4;
            if (phase == 0)
            {
                simulation.hazardSource = HazardSource.LegacyTimeline;
                simulation.scenarioMode = HazardScenarioMode.NoHazards;
                simulation.Begin(MiningMode.FirstPerson, true);
                actorPosition = simulation.Actor.position;
                simulation.SetHazard(0, 1); mark = frames; phase = 1;
            }
            else if (phase == 1 && frames > mark + 5)
            {
                Check(cutscene.ActiveDeviceIndex == -1 && cutscene.FeedCamera == null, "Warning created a collapse shot");
                simulation.SetHazard(0, 2); simulation.SetHazard(1, 2); simulation.SetHazard(2, 2);
                mark = frames; phase = 2;
            }
            else if (phase == 2 && frames > mark + 5)
            {
                Check(cutscene.ActiveDeviceIndex == 0 && cutscene.PendingCount == 2, "Simultaneous sites lost queue order");
                Check(cutscene.FeedCamera.enabled && cutscene.FeedTexture.width == 640, "Inset camera not rendering");
                Check((simulation.Actor.position - actorPosition).sqrMagnitude < .1f, "Inset moved idle FPP actor");
                Check(simulation.ViewCamera.targetTexture == null && simulation.ViewCamera.enabled, "Inset took over main camera");
                Check(cutscene.FeedCamera.GetComponent<AudioListener>() == null, "Inset duplicated audio listener");
                Capture("cutscene-fpp");
                simulation.TogglePause(); paused = simulation.Elapsed; mark = frames; phase = 3;
            }
            else if (phase == 3 && frames > mark + 25)
            {
                Check(simulation.Elapsed == paused && cutscene.ActiveDeviceIndex == 0 && cutscene.PendingCount == 2 && !cutscene.FeedCamera.enabled, "Pause did not freeze inset");
                simulation.TogglePause(); phase = 4;
            }
            else if (phase == 4 && cutscene.ActiveDeviceIndex == 1)
            {
                Check(cutscene.PendingCount == 1, "Second shot lost queued location");
                Capture("cutscene-second-device"); phase = 5;
            }
            else if (phase == 5 && cutscene.ActiveDeviceIndex == 2)
            {
                Check(cutscene.PendingCount == 0, "Third shot queue not empty"); phase = 6;
            }
            else if (phase == 6 && cutscene.ActiveDeviceIndex == -1)
            {
                simulation.SetHazard(0, 2); mark = frames; phase = 7;
            }
            else if (phase == 7 && frames > mark + 5)
            {
                Check(cutscene.ActiveDeviceIndex == -1 && !cutscene.FeedCamera.enabled, "Repeated closure replayed cutscene");
                results.Add("PASS FPP: warning hidden; three simultaneous closures queued D01/D02/D03; 5-second shots; pause freezes; duplicate closure ignored; main camera/actor/audio unchanged.");
                simulation.Begin(MiningMode.Story, true); simulation.SetHazard(0, 2); mark = frames; phase = 8;
            }
            else if (phase == 8 && frames > mark + 5)
            {
                Check(cutscene.ActiveDeviceIndex == 0 && cutscene.FeedCamera.enabled, "Story inset missing after session reset");
                Capture("cutscene-story");
                simulation.SetHazard(1, 2); simulation.SetHazard(2, 2); mark = frames; phase = 9;
            }
            else if (phase == 9 && frames > mark + 3)
            {
                Check(cutscene.PendingCount == 2, "Story multi-site queue missing");
                simulation.Begin(MiningMode.FirstPerson, true); mark = frames; phase = 10;
            }
            else if (phase == 10 && frames > mark + 5)
            {
                Check(cutscene.ActiveDeviceIndex == -1 && cutscene.PendingCount == 0 && !cutscene.FeedCamera.enabled, "Restart leaked queued shots");
                simulation.SetHazard(0, 2); mark = frames; phase = 11;
            }
            else if (phase == 11 && frames > mark + 5)
            {
                Check(cutscene.ActiveDeviceIndex == 0, "Restart suppressed new closure");
                simulation.Menu(); mark = frames; phase = 12;
            }
            else if (phase == 12 && frames > mark + 5)
            {
                Check(cutscene.ActiveDeviceIndex == -1 && cutscene.PendingCount == 0 && !cutscene.FeedCamera.enabled, "Menu retained camera or queue");
                results.Add("PASS Story: inset and multi-site queue visible; restart clears active/pending shots; same device can trigger in new session; menu disables camera.");
                Finish(true, "ALL CUTSCENE CHECKS PASSED");
            }
        }
        catch (Exception e) { Finish(false, "phase " + phase + ": " + e); }
    }
    static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new Exception("Graphics required for visual validation");
        cutscene.FeedCamera.Render();
        var camera = simulation.ViewCamera;
        var canvas = simulation.GetComponentInChildren<Canvas>();
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance;
        var scaler = canvas.GetComponent<CanvasScaler>(); var oldScale = scaler.uiScaleMode;
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
            File.WriteAllBytes("Validation/" + name + ".png", picture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = oldActive; camera.targetTexture = oldTarget;
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane;
            scaler.uiScaleMode = oldScale; scaler.enabled = false; scaler.enabled = true;
            rt.Release(); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(picture);
        }
    }
    static void Finish(bool success, string message)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText("Validation/cutscene-results.txt", string.Join("\n", results) + "\n" + message);
        Debug.Log(message); EditorApplication.Exit(success ? 0 : 1);
    }
}
