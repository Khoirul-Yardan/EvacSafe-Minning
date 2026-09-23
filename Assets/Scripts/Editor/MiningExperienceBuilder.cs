using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SafeMining;

// The new experience is separate from the legacy demo, so authored work stays reviewable.
[InitializeOnLoad]
public static class MiningExperienceBuilder
{
    public const string ScenePath = "Assets/Scenes/SafeMining_Experience.unity";
    static MiningExperienceBuilder() { EditorApplication.delayCall += EnsureScene; }
    static void EnsureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureMaterials();
        if (File.Exists(ScenePath)) return;
        CreateScene();
    }
    static void EnsureMaterials()
    {
        Directory.CreateDirectory("Assets/Resources");
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/MiningLit.mat") == null)
            AssetDatabase.CreateAsset(MineLayout.Material("MiningLit", Color.white), "Assets/Resources/MiningLit.mat");
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/MiningEmissive.mat") == null)
            AssetDatabase.CreateAsset(MineLayout.Material("MiningEmissive", Color.white, true), "Assets/Resources/MiningEmissive.mat");
    }
    [MenuItem("SafeMining/Create Story Experience", priority = 0)]
    public static void CreateScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureMaterials();
        if (File.Exists(ScenePath)) { Debug.Log("Experience already exists: " + ScenePath); return; }
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var root = new GameObject("SAFE-MINING EVAC | Story");
        SceneManager.MoveGameObjectToScene(root, scene); root.AddComponent<MiningSimulation>(); root.AddComponent<MiningTelemetry>();
        Directory.CreateDirectory("Assets/Scenes");
        // Keep a Lit material in Resources so runtime-generated geometry works in player builds too.
        EditorSceneManager.SaveScene(scene, ScenePath); EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        var builds = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var old in EditorBuildSettings.scenes) if (old.path != ScenePath) builds.Add(old);
        EditorBuildSettings.scenes = builds.ToArray(); AssetDatabase.SaveAssets();
        Debug.Log("[SafeMining] Story ready: " + ScenePath);
    }
    [MenuItem("SafeMining/Open Story Experience", priority = 1)]
    public static void OpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        CreateScene();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }
}

// Executed in an isolated Unity project by -executeMethod MiningExperienceValidation.Run.
public static class MiningExperienceValidation
{
    static int phase, frames;
    static double deadline;
    static MiningSimulation simulation;
    static bool storyCaptured;
    static int lastPhaseFrame;
    static readonly List<string> results = new List<string>();
    public static void Run()
    {
        try
        {
            ValidateGraph(); MiningExperienceBuilder.CreateScene();
            EditorSceneManager.OpenScene(MiningExperienceBuilder.ScenePath);
            EditorApplication.playModeStateChanged += OnPlay;
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void ValidateGraph()
    {
        var corridors = MineLayout.DefaultCorridors();
        var original = MineLayout.CreateCells(corridors);
        corridors.Add(new MineCorridor(new Vector2Int(-4, 2), new Vector2Int(4, 2)));
        var variant = MineLayout.CreateCells(corridors);
        Assert(variant.Count > original.Count && variant.Contains(new Vector2Int(2, 2)), "Inspector corridor did not change topology");
        Assert(MineLayout.FindPath(variant, MineLayout.Spawn, new HashSet<Vector2Int>(), out _).Count > 0, "Variant has no route");
        corridors.Add(new MineCorridor(Vector2Int.zero, new Vector2Int(1, 2)));
        bool rejected = false;
        try { MineLayout.CreateCells(corridors); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, "Diagonal corridor was accepted");
        corridors = MineLayout.DefaultCorridors();
        corridors.Add(new MineCorridor(new Vector2Int(20, 20), new Vector2Int(21, 20)));
        rejected = false;
        try { MineLayout.CreateCells(corridors); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, "Disconnected corridor was accepted");
        rejected = false;
        try { MineLayout.CreateCells(new List<MineCorridor>()); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, "Empty layout was accepted");
        results.Add("PASS configurable layout: topology changes; diagonal, disconnected and empty layouts rejected.");
        var cells = MineLayout.CreateCells(); var blocked = new HashSet<Vector2Int>();
        int checks = 0;
        for (int combination = 0; combination < 8; combination++)
        {
            blocked.Clear();
            for (int h = 0; h < 3; h++) if ((combination & (1 << h)) != 0) blocked.Add(MineLayout.HazardCells[h]);
            foreach (var cell in cells)
            {
                if (blocked.Contains(cell)) continue;
                var path = MineLayout.FindPath(cells, cell, blocked, out int exit);
                Assert(path.Count > 0 && exit >= 0, "Unreachable refuge from " + cell + ", mask " + combination);
                for (int i = 0; i < path.Count; i++)
                {
                    Assert(!blocked.Contains(path[i]), "Route enters blocked cell");
                    if (i > 0) Assert((path[i] - path[i - 1]).sqrMagnitude == 1, "Disconnected path edge");
                }
                checks++;
            }
        }
        // A sealed ring must fail safely instead of proposing a route through hazards.
        blocked.Clear(); foreach (var d in MineLayout.Directions) blocked.Add(MineLayout.Spawn + d);
        Assert(MineLayout.FindPath(cells, MineLayout.Spawn, blocked, out _).Count == 0, "No-route fallback is unsafe");
        results.Add("PASS graph: " + checks + " origin/hazard combinations; blocked-route failure is safe.");
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        // Static fields are reset on domain reload; use SessionState to restore in Tick registration.
    }
    [InitializeOnLoadMethod]
    static void ResumeAfterReload()
    {
        if (!UnityEditor.SessionState.GetBool("MiningValidation", false)) return;
        deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Validation timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            simulation = UnityEngine.Object.FindFirstObjectByType<MiningSimulation>();
            if (simulation == null) return;
            frames++;
            if (phase == 0 && frames > 5)
            {
                var documentation = MiningDocumentationPreview.Find(simulation.gameObject.scene);
                if (documentation != null)
                {
                    Assert(!documentation.gameObject.activeInHierarchy, "Editor preview duplicated runtime geometry");
                    Assert(documentation.CompareTag("EditorOnly"), "Documentation objects would enter player builds");
                    results.Add("PASS editor preview disabled before Play; EditorOnly hierarchy excluded from builds.");
                }
                Capture("01-menu");
                ValidateGeometry(simulation);
                simulation.Begin(MiningMode.FirstPerson, true);
                Assert(simulation.Mode == MiningMode.Story, "Legacy call opened FPP");
                Assert(simulation.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length > 0, "Menu buttons missing");
                foreach (var button in simulation.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    Assert(!button.name.Contains("FPP"), "Menu still offers FPP");
                results.Add("PASS story-only mode: legacy FPP request coerced to Story; no FPP menu button.");
                simulation.Begin(MiningMode.Story, true); Time.timeScale = 12;
                phase = 1;
            }
            else if (phase == 1 && !storyCaptured && simulation.Elapsed > 11)
            {
                Capture("02-story"); storyCaptured = true;
            }
            else if (phase == 1 && simulation.State == SafeMining.SessionState.Success)
            {
                Assert(simulation.Reroutes >= 2, "Adaptive story must reroute twice");
                Assert(simulation.Exposure < .1f, "Story enters danger perimeter");
                results.Add("PASS adaptive story: success, " + simulation.Reroutes + " reroutes, " + simulation.Elapsed.ToString("F1") + " s, exposure " + simulation.Exposure);
                simulation.Begin(MiningMode.Story, false); phase = 2;
            }
            else if (phase == 2 && simulation.State == SafeMining.SessionState.Blocked)
            {
                Assert(simulation.Reroutes == 0, "Static baseline changed route");
                results.Add("PASS static baseline: stops before landslide, zero reroutes.");
                simulation.Begin(MiningMode.Story, true);
                var body = simulation.Actor.GetComponent<CharacterController>(); body.enabled = false;
                simulation.Actor.position = MineLayout.World(new Vector2Int(4, 6)) + Vector3.up * .05f; body.enabled = true;
                simulation.SetHazard(0, 2); simulation.SetHazard(1, 2);
                Assert(simulation.Route.Count > 0 && simulation.Route[0].x == 4, "Story route is not from actor position");
                simulation.TogglePause(); Assert(simulation.State == SafeMining.SessionState.Paused, "Pause failed"); simulation.TogglePause();
                body.enabled = false; simulation.Actor.position = MineLayout.World(MineLayout.Exits[2]) + Vector3.up * .05f; body.enabled = true; phase = 3;
            }
            else if (phase == 3 && simulation.State == SafeMining.SessionState.Success)
            {
                results.Add("PASS story: routes from actor position, pause/resume, refuge completion.");
                simulation.Export(); Assert(simulation.ExportStatus.StartsWith("CSV tersimpan"), "CSV export failed");
                simulation.Begin(MiningMode.Story, true);
                Assert(simulation.Elapsed == 0 && simulation.Blocked.Count == 0 && simulation.Reroutes == 0, "Reset leaked session state");
                results.Add("PASS CSV export and independent session reset.");
                var body = simulation.Actor.GetComponent<CharacterController>(); body.enabled = false;
                simulation.Actor.position = MineLayout.World(new Vector2Int(0, 3)) + Vector3.up * .05f; body.enabled = true;
                simulation.SetHazard(0, 2); simulation.SetHazard(1, 2);
                Time.timeScale = 1; phase = 4; lastPhaseFrame = frames;
            }
            else if (phase == 4 && simulation.Elapsed >= 1.5f)
            {
                Capture("03-story-hazards");
                // A character-sized movement probe must remain inside the physical tunnel.
                simulation.Begin(MiningMode.Story, true);
                var body = simulation.Actor.GetComponent<CharacterController>();
                for (int i = 0; i < 100; i++) body.Move(Vector3.right * .1f);
                Assert(simulation.Actor.position.x < 3, "Worker capsule crossed exterior wall");
                results.Add("PASS worker collision: capsule cannot cross exterior wall.");
                simulation.Begin(MiningMode.Story, true); simulation.TogglePause();
                phase = 5; lastPhaseFrame = frames;
            }
            else if (phase == 5 && frames > lastPhaseFrame + 5)
            {
                Capture("04-paused");
                Assert(simulation.GetComponentInChildren<SafeMining.MineMapGraphic>().GetComponent<CanvasRenderer>() != null, "Minimap renderer missing");
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-miningWebSocketTest") >= 0)
                {
                    simulation.Begin(MiningMode.Story, true);
                    simulation.GetComponent<MiningTelemetry>().Connect(); phase = 6;
                }
                else
                {
                    simulation.Begin(MiningMode.Story, true);
                    var body = simulation.Actor.GetComponent<CharacterController>(); body.enabled = false;
                    simulation.Actor.position = MineLayout.World(new Vector2Int(0, 5)) + Vector3.up * .05f; body.enabled = true;
                    foreach (var direction in MineLayout.Directions) simulation.Blocked.Add(new Vector2Int(0, 5) + direction);
                    simulation.SetHazard(2, 1);
                    Assert(simulation.State == SafeMining.SessionState.Blocked && simulation.Route.Count == 0, "No-route story did not terminate");
                    results.Add("PASS no-route story: terminal Blocked outcome, empty route.");
                    Finish(true, "All checks passed");
                }
            }
            else if (phase == 6 && simulation.HazardLevels[2] == 1)
            {
                Assert(simulation.GetComponent<MiningTelemetry>().Status == "Connected", "WebSocket disconnected");
                results.Add("PASS WebSocket: real Unity snapshot sent and hazard command received/applied on main thread.");
                Finish(true, "All checks passed");
            }
            if (phase == 1 && simulation.Elapsed > 200) throw new Exception("Story stuck at " + simulation.Actor.position + ", route " + simulation.DirectionHint());
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void ValidateGeometry(MiningSimulation s)
    {
        s.Actor.GetComponent<CharacterController>().enabled = false;
        Physics.SyncTransforms(); int checkedEdges = 0, boundaries = 0;
        foreach (var cell in s.Cells)
        {
            Vector3 center = MineLayout.World(cell);
            Assert(Physics.Raycast(center + Vector3.up, Vector3.down, 1.1f), "Missing floor " + cell);
            Assert(Physics.Raycast(center + Vector3.up, Vector3.up, 5), "Missing roof " + cell);
            foreach (var d in MineLayout.Directions)
            {
                Vector3 dir = new Vector3(d.x, 0, d.y);
                if (s.Cells.Contains(cell + d))
                {
                    for (int lane = -1; lane <= 1; lane++)
                    {
                        Vector3 start = center + Vector3.up + Vector3.Cross(Vector3.up, dir) * lane;
                        Assert(!Physics.Raycast(start, dir, 6, ~0, QueryTriggerInteraction.Ignore), "Wall across connected cells: " + cell + " -> " + (cell + d));
                        Assert(Physics.Raycast(start + dir * 3, Vector3.down, 1.1f), "Floor gap at seam " + cell);
                    }
                    checkedEdges++;
                }
                else { Assert(Physics.Raycast(center + Vector3.up * 2.4f, dir, 3.8f), "Open exterior boundary " + cell + " / " + d); boundaries++; }
            }
        }
        results.Add("PASS geometry: " + s.Cells.Count + " floor/roof cells, " + checkedEdges + " open connections (3 lanes each), " + boundaries + " sealed boundaries.");
        s.Actor.GetComponent<CharacterController>().enabled = true;
    }
    static void Finish(bool success, string message)
    {
        UnityEditor.SessionState.SetBool("MiningValidation", false); EditorApplication.update -= Tick;
        Directory.CreateDirectory("Validation"); File.WriteAllText("Validation/results.txt", string.Join("\n", results) + "\n" + message);
        Debug.Log("[MiningValidation] " + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = simulation.ViewCamera;
        var canvas = simulation.GetComponentInChildren<Canvas>();
        var oldMode = canvas.renderMode;
        var rt = new RenderTexture(1600, 900, 24);
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .3f;
        camera.targetTexture = rt;
        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        var oldScaleMode = scaler.uiScaleMode; scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1; scaler.enabled = false; scaler.enabled = true;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        Directory.CreateDirectory("Validation"); File.WriteAllBytes("Validation/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; canvas.renderMode = oldMode;
        scaler.uiScaleMode = oldScaleMode; scaler.enabled = false; scaler.enabled = true;
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt);
    }
    public static void RunBatch()
    {
        UnityEditor.SessionState.SetBool("MiningValidation", true);
        // Disable reload for this isolated run, retaining validation state and graph results.
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Run();
    }
}
