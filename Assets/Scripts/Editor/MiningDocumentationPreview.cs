using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SafeMining;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MiningDocumentationPreview
{
    const string AssetPath = "Assets/Generated/MiningDocumentation/PreviewResources.asset";
    static MiningDocumentationPreview()
    {
        EditorApplication.delayCall += EnsureOpenScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => Ensure(scene);
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += EnsureOpenScenes;
        };
    }
    static void EnsureOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++) Ensure(SceneManager.GetSceneAt(i));
    }
    static void Ensure(Scene scene)
    {
        if (!scene.isLoaded || scene.path != MiningExperienceBuilder.ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var existing = Find(scene);
        if (existing != null && existing.GetComponentsInChildren<MiningLandslideDetector>(true).Length > 0) return;
        bool wasDirty = scene.isDirty;
        var preview = Generate(scene);
        // Do not silently save unrelated unsaved edits in an already-open scene.
        if (!wasDirty) EditorSceneManager.SaveScene(scene);
        if (!Application.isBatchMode) Focus(preview, 0);
    }
    public static MiningEditorPreview Find(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var preview = root.GetComponent<MiningEditorPreview>(); if (preview != null) return preview;
        }
        return null;
    }
    [MenuItem("SafeMining/Documentation/Rebuild Editor Preview", priority = 20)]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.path != MiningExperienceBuilder.ScenePath) { Debug.LogWarning("Open SafeMining_Experience before rebuilding its documentation preview."); return; }
        var preview = Generate(scene); Focus(preview, 0);
    }
    public static MiningEditorPreview Generate(Scene scene)
    {
        MiningSimulation simulation = null;
        foreach (var sceneRoot in scene.GetRootGameObjects())
        {
            simulation = sceneRoot.GetComponentInChildren<MiningSimulation>(true);
            if (simulation != null) break;
        }
        // Validate before replacing a working preview.
        var cells = MineLayout.CreateCells(simulation != null ? simulation.corridors : null);
        var old = Find(scene); if (old != null) Object.DestroyImmediate(old.gameObject);
        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        var container = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
        if (container == null) { container = new Mesh { name = "Generated documentation resources" }; AssetDatabase.CreateAsset(container, AssetPath); }
        else foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetPath)) if (asset != container) Object.DestroyImmediate(asset, true);

        var root = new GameObject("EDITOR PREVIEW | Documentation (excluded from Play)");
        SceneManager.MoveGameObjectToScene(root, scene); root.tag = "EditorOnly";
        var preview = root.AddComponent<MiningEditorPreview>();
        var environment = new GameObject("01 Environment | same map as Play").transform; environment.SetParent(root.transform, false);
        MineLayout.Build(environment, cells);
        var actors = new GameObject("02 Worker, landslides, cameras").transform; actors.SetParent(root.transform, false);
        MiningSimulation.CreateDocumentationActors(actors, cells);
        preview.worker = actors.Find("Worker | Story + FPP").gameObject;
        preview.landslides = new GameObject[MiningHazardScenario.DetectorSites(cells).Count];
        for (int i = 0; i < preview.landslides.Length; i++) preview.landslides[i] = actors.Find("Landslide " + (i + 1)).gameObject;

        // Split the existing shell for documentation only; gameplay keeps its original closed shell.
        var shell = environment.GetComponentInChildren<MeshFilter>(true);
        foreach (var filter in environment.GetComponentsInChildren<MeshFilter>(true))
            if (filter.name == "Rock walls and ceiling") { shell = filter; break; }
        var original = shell.sharedMesh; var vertices = original.vertices; var triangles = original.triangles;
        var roof = new List<int>(); var walls = new List<int>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var target = vertices[triangles[i]].y >= MineLayout.Ceiling && vertices[triangles[i + 1]].y >= MineLayout.Ceiling && vertices[triangles[i + 2]].y >= MineLayout.Ceiling ? roof : walls;
            target.Add(triangles[i]); target.Add(triangles[i + 1]); target.Add(triangles[i + 2]);
        }
        var roofMesh = Object.Instantiate(original); roofMesh.name = "Documentation ceiling"; roofMesh.triangles = roof.ToArray();
        original.triangles = walls.ToArray(); original.name = "Documentation walls";
        preview.ceiling = new GameObject("Ceiling | toggle for documentation", typeof(MeshFilter), typeof(MeshRenderer));
        preview.ceiling.transform.SetParent(shell.transform.parent, false);
        preview.ceiling.GetComponent<MeshFilter>().sharedMesh = roofMesh;
        preview.ceiling.GetComponent<MeshRenderer>().sharedMaterial = shell.GetComponent<MeshRenderer>().sharedMaterial;
        preview.ceiling.SetActive(false);

        preview.storyCamera = actors.Find("Simulation camera").GetComponent<Camera>();
        preview.storyCamera.name = "Documentation camera | Story";
        Vector3 start = MineLayout.World(MineLayout.Spawn);
        preview.storyCamera.transform.position = start + new Vector3(0, 2.65f, -3.1f);
        preview.storyCamera.transform.LookAt(start + new Vector3(0, 1.65f, 3));
        preview.fppCamera = Camera(root.transform, "Documentation camera | FPP", start + Vector3.up * 1.65f, Quaternion.identity);
        preview.overviewCamera = Camera(root.transform, "Documentation camera | Overview", new Vector3(0, 95, 18), Quaternion.Euler(90, 0, 0));
        var bounds = new Bounds(MineLayout.World(MineLayout.Spawn), Vector3.zero);
        foreach (var cell in cells) bounds.Encapsulate(MineLayout.World(cell));
        preview.overviewCamera.transform.position = new Vector3(bounds.center.x, 150, bounds.center.z);
        preview.overviewCamera.farClipPlane = 300;
        preview.overviewCamera.orthographic = true;
        preview.overviewCamera.orthographicSize = Mathf.Max(bounds.size.z * .5f + 9, (bounds.size.x * .5f + 9) / preview.overviewCamera.aspect);
        preview.overviewCamera.backgroundColor = new Color(.025f, .04f, .06f);
        foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = c == preview.overviewCamera;
        foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
        foreach (var effect in root.GetComponentsInChildren<MiningRockfall>(true)) Object.DestroyImmediate(effect);

        var fill = new GameObject("Documentation fill light", typeof(Light)); fill.transform.SetParent(root.transform, false);
        fill.transform.rotation = Quaternion.Euler(60, -25, 0);
        var light = fill.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.15f; light.shadows = LightShadows.None;
        var route = MineLayout.FindPath(cells, MineLayout.Spawn, new HashSet<Vector2Int>(), out _);
        var routeGO = new GameObject("03 Initial evacuation route", typeof(LineRenderer)); routeGO.transform.SetParent(root.transform, false);
        var line = routeGO.GetComponent<LineRenderer>(); line.sharedMaterial = MineLayout.Material("Documentation route", new Color(.05f, .85f, .48f), true);
        line.startWidth = line.endWidth = .18f; line.positionCount = route.Count;
        for (int i = 0; i < route.Count; i++) line.SetPosition(i, MineLayout.World(route[i]) + Vector3.up * .15f);
        Persist(root);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); SceneView.RepaintAll();
        Debug.Log("[MiningDocumentation] Saved editor geometry and cameras ready. EditorOnly objects are disabled before runtime generation.");
        return preview;
    }
    static Camera Camera(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(name, typeof(Camera)); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(position, rotation);
        var camera = go.GetComponent<Camera>(); camera.nearClipPlane = .06f; camera.farClipPlane = 200;
        camera.fieldOfView = 72; camera.clearFlags = CameraClearFlags.SolidColor; return camera;
    }
    static void Persist(GameObject root)
    {
        var saved = new HashSet<Object>();
        void Save(Object asset)
        {
            if (asset == null || EditorUtility.IsPersistent(asset) || !saved.Add(asset)) return;
            AssetDatabase.AddObjectToAsset(asset, AssetPath);
        }
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) Save(filter.sharedMesh);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            // The built-in font owns a dynamic atlas; let Unity reconstruct it after reopening.
            var text = renderer.GetComponent<TextMesh>();
            if (text != null)
            {
                if (text.GetComponentInParent<MiningLandslideDetector>() == null) renderer.sharedMaterial = text.font.material;
                else Save(renderer.sharedMaterial);
                continue;
            }
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                Save(material.mainTexture); Save(material);
            }
        }
    }
    public static void Focus(MiningEditorPreview preview, int view)
    {
        if (preview == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Undo.RecordObject(preview.ceiling, "Documentation ceiling");
        preview.ceiling.SetActive(view != 0); preview.worker.SetActive(view != 2);
        var camera = view == 0 ? preview.overviewCamera : view == 1 ? preview.storyCamera : preview.fppCamera;
        foreach (var c in preview.GetComponentsInChildren<Camera>(true)) c.enabled = c == camera;
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            sceneView.orthographic = view == 0;
            if (view == 0) sceneView.LookAtDirect(new Vector3(0, 0, 18), Quaternion.Euler(65, 0, 0), 65);
            else sceneView.LookAtDirect(camera.transform.position + camera.transform.forward * 4, camera.transform.rotation, 4);
            sceneView.sceneLighting = false; sceneView.Repaint();
        }
        Selection.activeGameObject = preview.gameObject;
        EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
    }
    [MenuItem("SafeMining/Documentation/Overview Map")] public static void Overview() => Focus(Find(SceneManager.GetActiveScene()), 0);
    [MenuItem("SafeMining/Documentation/Story View")] public static void Story() => Focus(Find(SceneManager.GetActiveScene()), 1);
    [MenuItem("SafeMining/Documentation/FPP View")] public static void Fpp() => Focus(Find(SceneManager.GetActiveScene()), 2);

    public static void Export(MiningEditorPreview preview)
    {
        var camera = preview.overviewCamera.enabled ? preview.overviewCamera : preview.storyCamera.enabled ? preview.storyCamera : preview.fppCamera;
        const string directory = "Documentation/Previews"; Directory.CreateDirectory(directory);
        string name = camera == preview.overviewCamera ? "editor-overview" : camera == preview.storyCamera ? "editor-story" : "editor-fpp";
        var rt = new RenderTexture(1920, 1080, 24); var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
            File.WriteAllBytes(directory + "/" + name + ".png", texture.EncodeToPNG());
            Debug.Log("Documentation PNG: " + Path.GetFullPath(directory + "/" + name + ".png"));
        }
        finally { camera.targetTexture = previousTarget; RenderTexture.active = previous; Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt); }
    }
    // For the isolated validation project; closes the editor when the usual gameplay suite finishes.
    public static void ValidateBatch()
    {
        var scene = EditorSceneManager.OpenScene(MiningExperienceBuilder.ScenePath);
        var preview = Find(scene) ?? Generate(scene);
        EditorSceneManager.SaveScene(scene);
        scene = EditorSceneManager.OpenScene(MiningExperienceBuilder.ScenePath);
        preview = Find(scene);
        if (preview == null || preview.GetComponentsInChildren<Renderer>(true).Length < 100) throw new Exception("Preview missing after reopening");
        foreach (var mesh in preview.GetComponentsInChildren<MeshFilter>(true))
            if (mesh.sharedMesh == null || !EditorUtility.IsPersistent(mesh.sharedMesh)) throw new Exception("Preview mesh was not persisted: " + mesh.name);
        foreach (var renderer in preview.GetComponentsInChildren<MeshRenderer>(true))
            foreach (var material in renderer.sharedMaterials) if (material == null) throw new Exception("Missing material: " + renderer.name);
        for (int i = 0; i < 3; i++) { Focus(preview, i); Export(preview); }
        Focus(preview, 0); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Validation");
        File.WriteAllText("Validation/editor-preview.txt", "PASS saved scene reopened with persistent meshes/materials.\nPASS overview/story/FPP PNG exported without Play.\n");
        MiningExperienceValidation.RunBatch();
    }
}

[CustomEditor(typeof(MiningEditorPreview))]
public class MiningEditorPreviewInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Pratinjau dokumentasi tersimpan di scene. Otomatis dinonaktifkan saat Play dan tidak disertakan dalam build. Geometri dibuat ulang dari map simulasi.", MessageType.Info);
        var preview = (MiningEditorPreview)target;
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Seluruh map / atap terbuka")) MiningDocumentationPreview.Focus(preview, 0);
            if (GUILayout.Button("Sudut kamera cerita")) MiningDocumentationPreview.Focus(preview, 1);
            if (GUILayout.Button("Sudut kamera FPP")) MiningDocumentationPreview.Focus(preview, 2);
            if (GUILayout.Button("Ekspor gambar PNG 1920 x 1080")) MiningDocumentationPreview.Export(preview);
            bool show = preview.landslides.Length > 0 && preview.landslides[0].activeSelf;
            bool next = EditorGUILayout.Toggle("Tampilkan contoh longsor", show);
            if (show != next)
            {
                for (int i = 0; i < preview.landslides.Length; i++) { Undo.RecordObject(preview.landslides[i], "Documentation landslides"); preview.landslides[i].SetActive(next && i == 0); }
                foreach (var detector in preview.GetComponentsInChildren<MiningLandslideDetector>(true)) detector.SetLevel(next && detector.StationIndex == 0 ? 2 : 0);
                EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
            }
            preview.showLabels = EditorGUILayout.Toggle("Label titik penting (Gizmos)", preview.showLabels);
        }
    }
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    static void DrawLabels(MiningEditorPreview preview, GizmoType type)
    {
        if (Application.isPlaying || !preview.showLabels) return;
        var style = new GUIStyle(EditorStyles.boldLabel); style.normal.textColor = Color.white;
        Handles.Label(MineLayout.World(MineLayout.Spawn) + Vector3.up * 2, "MULAI / PEKERJA", style);
        for (int i = 0; i < MineLayout.Exits.Length; i++) Handles.Label(MineLayout.World(MineLayout.Exits[i]) + Vector3.up * 5, "ZONA AMAN " + (i + 1), style);
        foreach (var detector in preview.GetComponentsInChildren<MiningLandslideDetector>(true)) Handles.Label(MineLayout.World(detector.Cell) + Vector3.up * 5, "DETEKTOR D" + (detector.StationIndex + 1).ToString("00"), style);
    }
}
