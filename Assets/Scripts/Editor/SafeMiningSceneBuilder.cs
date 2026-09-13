using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;

[InitializeOnLoad]
public static class SafeMiningSceneBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/SafeMiningEvac_Demo.unity";
    private const string NAVMESH_PATH = "Assets/Scenes/SafeMiningEvac_Demo_NavMesh.asset";
    private const string CORRIDOR_FBX_PATH = "Assets/Models/corridor.fbx";
    private const string MINIMAP_LAYER_NAME = "MinimapWorld";

    static SafeMiningSceneBuilder()
    {
        EditorApplication.delayCall += CheckAndAutoBuild;
    }

    private static void NormalizeExistingCorridorLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != SCENE_PATH) return;

        GameObject mapRoot = GameObject.Find("Environment_UndergroundMine");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CORRIDOR_FBX_PATH);
        if (mapRoot == null || prefab == null) return;

        bool changed = false;
        foreach (Transform child in mapRoot.transform)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(child.gameObject);
            if (source != prefab || child.name == "Corridor_Map") continue;
            if (child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
                changed = true;
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SafeMiningSceneBuilder] Layout dinormalisasi: hanya Corridor_Map authored yang aktif; salinan yang belum memiliki connector valid dinonaktifkan.");
        }
    }

    [MenuItem("SafeMining/Enable Saved corridor.fbx Network", priority = 3)]
    public static void EnableSavedCorridorNetwork()
    {
        GameObject mapRoot = GameObject.Find("Environment_UndergroundMine");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CORRIDOR_FBX_PATH);
        if (mapRoot == null || prefab == null) return;

        if (!CanBuildGridAlignedLoop(prefab))
        {
            Debug.LogWarning("[SafeMiningSceneBuilder] Saved corridor network tidak diaktifkan. " +
                             "Ia berasal dari algoritme anchor lama dan akan menghidupkan salinan mesh yang overlap.");
            return;
        }

        int enabledCount = 0;
        foreach (Transform child in mapRoot.transform)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(child.gameObject);
            if (source != prefab) continue;
            Undo.RecordObject(child.gameObject, "Enable saved corridor network");
            child.gameObject.SetActive(true);
            enabledCount++;
        }

        EditorSceneManager.MarkSceneDirty(mapRoot.scene);
        EditorSceneManager.SaveScene(mapRoot.scene);
        Debug.Log($"[SafeMiningSceneBuilder] {enabledCount} saved corridor.fbx instance diaktifkan kembali.");
    }

    private static void CheckAndAutoBuild()
    {
        if (!File.Exists(SCENE_PATH))
        {
            Debug.Log("[SafeMiningSceneBuilder] Scene SafeMiningEvac_Demo belum ditemukan. Menjalankan auto-build...");
            BuildCompleteScene();
        }
    }

    [MenuItem("SafeMining/Generate SafeMiningEvac_Demo Scene", priority = 1)]
    public static void BuildCompleteScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[SafeMiningSceneBuilder] Build dibatalkan: hentikan Play Mode sebelum membuat scene baru.");
            return;
        }

        Debug.Log("<color=cyan>==================================================\n[SafeMiningSceneBuilder] MEMULAI GENERASI SCENE SafeMiningEvac_Demo...\n==================================================</color>");

        EnsureExitPointTag();
        int minimapLayer = EnsureLayer(MINIMAP_LAYER_NAME);

        AssetDatabase.ImportAsset(CORRIDOR_FBX_PATH, ImportAssetOptions.ForceUpdate);
        GameObject tunnelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CORRIDOR_FBX_PATH);
        if (tunnelPrefab == null)
        {
            Debug.LogError($"[SafeMiningSceneBuilder] Gagal memuat corridor asset di {CORRIDOR_FBX_PATH}.");
            return;
        }

        LogImportedTunnelAsset(tunnelPrefab);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Materials
        Material rockMat = CreateOrLoadMaterial("Assets/Materials/M_MineRock.mat", new Color(0.22f, 0.22f, 0.24f), 0.1f);
        Material workerMat = CreateOrLoadMaterial("Assets/Materials/M_WorkerSuit.mat", new Color(1.0f, 0.55f, 0.0f), 0.3f);
        Material exitMat = CreateOrLoadMaterial("Assets/Materials/M_ExitBeacon.mat", new Color(0.0f, 1.0f, 0.5f, 0.7f), 0.8f, true);
        Material dangerMat = CreateOrLoadMaterial("Assets/Materials/M_DangerZone.mat", new Color(1.0f, 0.1f, 0.1f, 0.55f), 0.5f, true);
        Material inactiveDangerMat = CreateOrLoadMaterial("Assets/Materials/M_DangerInactive.mat", new Color(0.2f, 0.8f, 0.2f, 0.15f), 0.1f, true);

        GameObject envRoot = new GameObject("Environment_UndergroundMine");

          // The source FBX is monolithic, so use a deterministic procedural modular fallback.
          GameObject corridor = ProceduralMineLoop.Create(envRoot.transform, rockMat);
          List<GameObject> corridorSegments = new List<GameObject> { corridor };
          SetLayerRecursively(corridor, minimapLayer);
          Bounds corridorBounds = CalculateBounds(envRoot);

          Debug.Log("<color=yellow>[SafeMiningSceneBuilder] AUDIT NETWORK CORRIDOR.FBX:\n" +
              $"- Source asset: {CORRIDOR_FBX_PATH}\n" +
              "- Layout: procedural closed rounded-rectangle loop\n" +
              "- Straight: axis-aligned; Curve90: radius konsisten 2m.</color>");

        // Mine Lanterns along snapped corridors
        GameObject lightsRoot = new GameObject("Lighting_MineLamps");
        lightsRoot.transform.SetParent(envRoot.transform);

        Vector3[] lightPositions = new Vector3[]
        {
            new Vector3(0f, 1.5f, 5f),
            new Vector3(0f, 1.5f, -15f),
            new Vector3(0f, 1.5f, -30f),
            new Vector3(-12f, 1.5f, -47f),
            new Vector3(-12f, 1.5f, -75f),
            new Vector3(-4f, 1.5f, -100f),
            new Vector3(7.32f, 1.5f, -47f),
            new Vector3(35f, 1.5f, -53f),
            new Vector3(60f, 1.5f, -59f),
            new Vector3(-25.78f, 1.5f, -32.64f),
            new Vector3(-50f, 1.5f, -18f),
            new Vector3(-65f, 1.5f, -7f)
        };

        foreach (var pos in lightPositions)
        {
            CreateMineLantern(lightsRoot.transform, pos);
        }

        // =========================================================================
        // 2. EXITS & WORKER AGENT & PLAYER FPP
        // =========================================================================
        GameObject exitsRoot = new GameObject("ExitPoints");
        Vector3 exitPos1 = ProceduralMineLoop.Points[9];
        Vector3 exitPos2 = ProceduralMineLoop.Points[17];
        Vector3 exitPos3 = ProceduralMineLoop.Points[17];

        GameObject ep1 = CreateExitPoint("ExitPoint_1_NorthPortal", exitPos1, exitsRoot.transform, exitMat);
        GameObject ep2 = CreateExitPoint("ExitPoint_2_SouthShaft", exitPos2, exitsRoot.transform, exitMat);
        GameObject ep3 = CreateExitPoint("ExitPoint_3_EastVent", exitPos3, exitsRoot.transform, exitMat);

        // Worker Agent Start Pos (Junction 1: center of Seg 1)
        Vector3 workerStartPos = ProceduralMineLoop.Points[0] + Vector3.forward * 2f;
        GameObject workerGO = CreateWorkerAgent("WorkerAgent", workerStartPos, workerMat);

        // Player FPP
        GameObject playerFPP = CreatePlayerFPP("PlayerFPP", workerStartPos);

        // =========================================================================
        // 3. DYNAMICS DANGER ZONES
        // =========================================================================
        GameObject dangerRoot = new GameObject("DangerZones");
        // DangerZone 1: Blocks North Exit Path
        DangerZone dz1 = CreateDangerZone("DangerZone_1_Exit1Route", new Vector3(-36f, ProceduralMineLoop.FloorY, 52f), new Vector3(4f, 4f, 4f), dangerRoot.transform, dangerMat, inactiveDangerMat);

        // DangerZone 2: Blocks South Exit Path (at Junction South)
        DangerZone dz2 = CreateDangerZone("DangerZone_2_Exit2Route", new Vector3(36f, ProceduralMineLoop.FloorY, 48f), new Vector3(4f, 4f, 4f), dangerRoot.transform, dangerMat, inactiveDangerMat);

        // DangerZone 3: Blocks East Exit Path (at Junction East)
        DangerZone dz3 = CreateDangerZone("DangerZone_3_WestRoute", new Vector3(2f, ProceduralMineLoop.FloorY, -20f), new Vector3(4f, 4f, 4f), dangerRoot.transform, dangerMat, inactiveDangerMat);
        FPPController fppController = playerFPP.GetComponent<FPPController>();
        fppController.dangerZones = new[] { dz1, dz2, dz3 };

        // =========================================================================
        // 4. BAKE NAVMESH
        // =========================================================================
        Debug.Log("[SafeMiningSceneBuilder] Memulai proses bake NavMesh di atas layout koridor baru...");
        NavMeshSurface navSurface = envRoot.AddComponent<NavMeshSurface>();
        navSurface.collectObjects = CollectObjects.All;
        navSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        navSurface.defaultArea = 0;
        navSurface.agentTypeID = 0;
        navSurface.BuildNavMesh();

        if (navSurface.navMeshData != null)
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NAVMESH_PATH) != null)
            {
                AssetDatabase.DeleteAsset(NAVMESH_PATH);
            }
            AssetDatabase.CreateAsset(navSurface.navMeshData, NAVMESH_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[SafeMiningSceneBuilder] NavMesh berhasil di-bake di layout koridor baru!</color>");
        }

        GameObject entrancePoint = new GameObject("EntrancePoint");
        SnapToNavMesh(entrancePoint, workerStartPos);
        GameObject junctionPoint = new GameObject("JunctionPoint");
        SnapToNavMesh(junctionPoint, ProceduralMineLoop.Points[1]);
        workerStartPos = entrancePoint.transform.position;
        SnapToNavMesh(workerGO, workerStartPos);
        SnapToNavMesh(playerFPP, workerStartPos);
        LogNetworkAudit(workerStartPos, new[] { ep1.transform, ep2.transform, ep3.transform }, corridorSegments.Count);

        // =========================================================================
        // 5. SYSTEM CONTROLLERS & MINIMAP SETUP
        // =========================================================================
        GameObject systemGO = new GameObject("[EvacuationSystem]");
        EvacuationController controller = systemGO.AddComponent<EvacuationController>();
        controller.workerAgent = workerGO.GetComponent<WorkerAgent>();
        controller.dangerZones = new List<DangerZone> { dz1, dz2, dz3 };
        controller.exitPoints = new List<Transform> { ep1.transform, ep2.transform, ep3.transform };
        controller.adaptiveModeEnabled = true;

        ScenarioRunner runner = systemGO.AddComponent<ScenarioRunner>();
        runner.controller = controller;
        runner.dangerZonePrimary = dz1;
        runner.dangerZoneAlternative = dz2;
        runner.dangerZoneTertiary = dz3;

        // Minimap Render Texture & Camera
        RenderTexture minimapRT = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        if (!Directory.Exists("Assets/Textures")) Directory.CreateDirectory("Assets/Textures");
        if (AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Textures/MinimapRT.renderTexture") != null)
        {
            AssetDatabase.DeleteAsset("Assets/Textures/MinimapRT.renderTexture");
        }
        AssetDatabase.CreateAsset(minimapRT, "Assets/Textures/MinimapRT.renderTexture");

        GameObject minimapCamGO = new GameObject("MinimapCamera");
        Camera minimapCam = minimapCamGO.AddComponent<Camera>();
        minimapCam.orthographic = true;
        minimapCam.cullingMask = 1 << minimapLayer;
        minimapCam.clearFlags = CameraClearFlags.SolidColor;
        minimapCam.backgroundColor = new Color(0.015f, 0.02f, 0.025f, 1f);
        float minimapSize = Mathf.Max(corridorBounds.size.x, corridorBounds.size.z) * 0.55f;
        minimapCam.orthographicSize = Mathf.Max(minimapSize, 10f);
        minimapCamGO.transform.position = new Vector3(corridorBounds.center.x, corridorBounds.max.y + 80f, corridorBounds.center.z);
        minimapCamGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        minimapCam.targetTexture = minimapRT;

        // UI Canvas HUD & Minimap Overlay
        Canvas hudCanvas = CreateEvacuationHUDCanvas(controller, runner, minimapRT, ep1.transform, ep2.transform, ep3.transform, dz1, dz2, dz3, workerGO, playerFPP);
        MinimapController minimapController = hudCanvas.GetComponentInChildren<MinimapController>();
        minimapController.SetWorldBounds(corridorBounds);
        fppController.dangerOverlay = CreateDangerOverlay(hudCanvas.transform);
        CreateSafetyPlatform(corridorBounds, envRoot.transform);

        // View Mode Manager
        ViewModeManager vmm = systemGO.AddComponent<ViewModeManager>();
        vmm.workerAgentGO = workerGO;
        vmm.playerFPPGO = playerFPP;
        vmm.mainSimulationCamera = Camera.main;
        vmm.fppController = fppController;
        vmm.minimapController = minimapController;

        // Find HUD button to bind view mode
        Transform btnView = hudCanvas.transform.Find("Panel_HUD/Btn_ToggleViewMode");
        if (btnView != null)
        {
            vmm.toggleViewModeButton = btnView.GetComponent<Button>();
            vmm.toggleViewModeButtonText = btnView.GetComponentInChildren<Text>();
        }

        // Camera Follow Setup for Auto Simulation
        SetupCamera(workerGO.transform);

        // Simpan Scene
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.Refresh();

        Debug.Log("<color=green>==================================================\n" +
                  "[SafeMiningSceneBuilder] GENERASI SCENE & FPP + MINIMAP SELESAI!\n" +
                  $"- Corridor mesh: {corridorSegments.Count} instance dari corridor.fbx\n" +
                  $"- Exit Points Final: 3 Titik ({ep1.name}, {ep2.name}, {ep3.name})\n" +
                  "- FPP Mode: Aktif (WASD + Mouse Look + CharacterController + Camera Mata)\n" +
                  "- Minimap: Real-time RT Overlay (Worker/Player, Exits, DangerZones)\n" +
                  "==================================================</color>");
    }

    [MenuItem("SafeMining/Arrange Existing corridor.fbx Instances", priority = 2)]
    public static void ArrangeExistingCorridorInstances()
    {
        GameObject mapRoot = GameObject.Find("Environment_UndergroundMine");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CORRIDOR_FBX_PATH);
        if (mapRoot == null || prefab == null)
        {
            Debug.LogWarning("[SafeMiningSceneBuilder] Environment_UndergroundMine atau corridor.fbx belum tersedia.");
            return;
        }

        if (!CanBuildGridAlignedLoop(prefab))
        {
            ArrangeProceduralFallback(mapRoot);
            return;
        }

        List<Transform> instances = new List<Transform>();
        foreach (Transform child in mapRoot.transform)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(child.gameObject);
            if (source == prefab || child.name.ToLowerInvariant().Contains("corridor")) instances.Add(child);
        }

        Debug.Log($"[SafeMiningSceneBuilder] Ditemukan {instances.Count} instance corridor.fbx tersimpan di scene.");
        for (int i = 0; i < instances.Count; i++)
        {
            Debug.Log($"[SafeMiningSceneBuilder] Instance {i + 1}: {instances[i].name}, position={instances[i].position}, rotation={instances[i].eulerAngles}");
        }

        Debug.LogError("[SafeMiningSceneBuilder] Arrangement dihentikan tanpa mengubah scene. " +
                       "Asset saat ini tidak menyediakan modul straight/curve serta connector endpoint; " +
                       "perhitungan loop, validasi gap < 0.01, dan bake NavMesh yang benar tidak dapat dilakukan.");
    }

    // A loop assembler requires independently placeable modules and explicit endpoints. A single
    // monolithic FBX cannot be cut safely at runtime/editor time without generating new geometry.
    private static bool CanBuildGridAlignedLoop(GameObject prefab)
    {
        MeshFilter[] meshes = prefab != null ? prefab.GetComponentsInChildren<MeshFilter>(true) : new MeshFilter[0];
        if (meshes.Length >= 2) return true;

        Debug.LogError("[SafeMiningSceneBuilder] Rounded-rectangle loop tidak dijalankan: Assets/Models/corridor.fbx " +
                       "hanya berisi satu mesh monolitik. Diperlukan minimal prefab Straight dan Curve90 " +
                       "dengan dua connector endpoint lokal per prefab (serta radius Curve90 yang sama). " +
                       "Tool lama memakai anchor hard-code (entry/branch/west) dan menduplikasi seluruh mesh; " +
                       "logika itu telah dinonaktifkan agar tidak membuat overlap atau rotasi/layout acak.");
        return false;
    }

    private static void ArrangeProceduralFallback(GameObject mapRoot)
    {
        // The old hidden fall-recovery cube had a collider spanning the whole map. Because NavMesh
        // collects PhysicsColliders it created walkable terrain outside the tunnels.
        GameObject oldPlatform = GameObject.Find("SafetyPlatform_FallRecovery");
        if (oldPlatform != null) Undo.DestroyObjectImmediate(oldPlatform);
        List<GameObject> remove = new List<GameObject>();
        foreach (Transform child in mapRoot.transform)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(child.gameObject);
            if (source != null || child.name.StartsWith("ProceduralCorridor") || child.name.StartsWith("Corridor_")) remove.Add(child.gameObject);
        }
        foreach (GameObject old in remove) Undo.DestroyObjectImmediate(old);

        Material rock = CreateOrLoadMaterial("Assets/Materials/M_MineRock.mat", new Color(.22f, .22f, .24f), .1f);
        GameObject loop = ProceduralMineLoop.Create(mapRoot.transform, rock);
        SetLayerRecursively(loop, EnsureLayer(MINIMAP_LAYER_NAME));
        Bounds bounds = CalculateBounds(mapRoot);
        NavMeshSurface surface = mapRoot.GetComponent<NavMeshSurface>();
        if (surface == null) surface = mapRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();

        Vector3 start = ProceduralMineLoop.Points[0] + Vector3.forward * 2f;
        GameObject entrance = GameObject.Find("EntrancePoint") ?? new GameObject("EntrancePoint");
        SnapToNavMesh(entrance, start);
        start = entrance.transform.position;
        GameObject junction = GameObject.Find("JunctionPoint") ?? new GameObject("JunctionPoint");
        SnapToNavMesh(junction, ProceduralMineLoop.Points[1]);
        GameObject worker = GameObject.Find("WorkerAgent"), player = GameObject.Find("PlayerFPP");
        SnapToNavMesh(worker, start); SnapToNavMesh(player, start);
        GameObject[] exits = GameObject.FindGameObjectsWithTag("ExitPoint");
        Vector3[] exitPositions = { ProceduralMineLoop.Points[9], ProceduralMineLoop.Points[17] };
        for (int i = 0; i < exits.Length; i++)
        {
            exits[i].SetActive(i < 2);
            if (i < exitPositions.Length) SnapToNavMesh(exits[i], exitPositions[i]);
        }
        if (NavMesh.SamplePosition(start, out NavMeshHit entranceHit, 4f, NavMesh.AllAreas))
        {
            for (int i = 0; i < 2 && i < exits.Length; i++)
            {
                bool validExit = NavMesh.SamplePosition(exits[i].transform.position, out NavMeshHit exitHit, 4f, NavMesh.AllAreas);
                NavMeshPath path = new NavMeshPath();
                bool complete = validExit && NavMesh.CalculatePath(entranceHit.position, exitHit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                Debug.Log($"[SafeMiningSceneBuilder] NAV TEST Entrance -> {exits[i].name}: {(complete ? "PathComplete" : "FAILED")}, corners={path.corners.Length}");
                if (!complete) Debug.LogError($"[SafeMiningSceneBuilder] Cabang {i + 1} terputus dari NavMesh; jangan mulai simulasi sebelum diperbaiki.");
            }
        }
        Transform checkpoint1 = GameObject.Find("Checkpoint_Exit1")?.transform ?? new GameObject("Checkpoint_Exit1").transform;
        Transform checkpoint2 = GameObject.Find("Checkpoint_Exit2")?.transform ?? new GameObject("Checkpoint_Exit2").transform;
        checkpoint1.position = new Vector3(-28, ProceduralMineLoop.FloorY, 20);
        checkpoint2.position = new Vector3(28, ProceduralMineLoop.FloorY, 0);
        DangerZone[] zones = Object.FindObjectsByType<DangerZone>(FindObjectsSortMode.None);
        Vector3[] dangerPositions = { new Vector3(-36, ProceduralMineLoop.FloorY, 52), new Vector3(36, ProceduralMineLoop.FloorY, 48) };
        for (int i = 0; i < zones.Length && i < dangerPositions.Length; i++) zones[i].transform.position = dangerPositions[i];
        Camera minimap = GameObject.Find("MinimapCamera")?.GetComponent<Camera>();
        if (minimap != null) { minimap.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * .55f; minimap.transform.position = bounds.center + Vector3.up * 80f; }
        MinimapController minimapController = Object.FindFirstObjectByType<MinimapController>();
        if (minimapController != null) { minimapController.SetWorldBounds(bounds); minimapController.SetCheckpointPoints(new[] { checkpoint1, checkpoint2 }); }
        FPPController fpp = player != null ? player.GetComponent<FPPController>() : null;
        if (fpp != null) fpp.SetCheckpointPoints(new[] { checkpoint1, checkpoint2 });
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        Transform oldOverlay = canvas != null ? canvas.transform.Find("FPP_DangerOverlay") : null;
        if (oldOverlay != null) Undo.DestroyObjectImmediate(oldOverlay.gameObject);
        if (fpp != null && canvas != null) fpp.dangerOverlay = CreateDangerOverlay(canvas.transform);
        GameObject system = GameObject.Find("[EvacuationSystem]");
        if (system != null && system.GetComponent<SafeMiningRuntimeDiagnostics>() == null) system.AddComponent<SafeMiningRuntimeDiagnostics>();
        Camera mainCamera = Camera.main;
        if (mainCamera != null && worker != null)
        {
            CameraFollow follow = mainCamera.GetComponent<CameraFollow>() ?? mainCamera.gameObject.AddComponent<CameraFollow>();
            follow.target = worker.transform;
            follow.offset = new Vector3(0f, 18f, -14f);
            mainCamera.transform.position = worker.transform.position + follow.offset;
            mainCamera.transform.LookAt(worker.transform.position + Vector3.up);
        }
        EditorSceneManager.MarkSceneDirty(mapRoot.scene); EditorSceneManager.SaveScene(mapRoot.scene);
        Debug.Log($"[SafeMiningSceneBuilder] PROCEDURAL LOOP READY: bounds={bounds.size}; radius={ProceduralMineLoop.Radius:F1}; straight=axis-aligned; exits={exits.Length}; zones={zones.Length}");
    }

    private static GameObject CreateCorridorSegment(GameObject prefab, string name, Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject inst = null;
        if (prefab != null)
        {
            inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = name;
            inst.transform.position = position;
            inst.transform.rotation = rotation;
            inst.transform.SetParent(parent);

            MeshFilter[] mfs = inst.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh != null)
                {
                    MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
                    if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
        }
        else
        {
            inst = new GameObject(name);
            inst.transform.position = position;
            inst.transform.rotation = rotation;
            inst.transform.SetParent(parent);
        }

        return inst;
    }

    private static Vector3 SnapConnector(Vector3 targetWorld, Vector3 sourceLocal, Quaternion rotation)
    {
        return targetWorld - rotation * sourceLocal;
    }

    private static Vector3 WorldConnector(Vector3 segmentPosition, Quaternion segmentRotation, Vector3 localConnector)
    {
        return segmentPosition + segmentRotation * localConnector;
    }

    private static void LogNetworkAudit(Vector3 startPosition, Transform[] exits, int segmentCount)
    {
        Debug.Log($"[SafeMiningSceneBuilder] NETWORK AUDIT: Segments={segmentCount}, Junctions=3, ExitPoints={exits.Length}");
        if (!NavMesh.SamplePosition(startPosition, out NavMeshHit startHit, 20f, NavMesh.AllAreas)) return;

        for (int i = 0; i < exits.Length; i++)
        {
            if (exits[i] == null || !NavMesh.SamplePosition(exits[i].position, out NavMeshHit exitHit, 20f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[SafeMiningSceneBuilder] ExitPoint_{i + 1}: tidak memiliki titik NavMesh valid.");
                continue;
            }

            NavMeshPath path = new NavMeshPath();
            bool valid = NavMesh.CalculatePath(startHit.position, exitHit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            float length = 0f;
            if (valid)
            {
                for (int corner = 1; corner < path.corners.Length; corner++) length += Vector3.Distance(path.corners[corner - 1], path.corners[corner]);
            }
            Debug.Log($"[SafeMiningSceneBuilder] {exits[i].name}: position={exits[i].position}, path={(valid ? $"{length:F1}m / {length / 4f:F1}s" : "INVALID")}");
        }
    }

    private static void SnapToNavMesh(GameObject actor, Vector3 requestedPosition)
    {
        if (actor == null) return;

        if (NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, 20f, NavMesh.AllAreas))
        {
            CharacterController character = actor.GetComponent<CharacterController>();
            actor.transform.position = hit.position + (character != null ? Vector3.up * (character.height * .5f) : Vector3.zero);
            Debug.Log($"[SafeMiningSceneBuilder] {actor.name} snapped to NavMesh at {hit.position}");
        }
        else
        {
            Debug.LogWarning($"[SafeMiningSceneBuilder] Tidak menemukan NavMesh dekat posisi awal {actor.name}: {requestedPosition}");
        }
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, new Vector3(20f, 10f, 20f));

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Debug.Log($"[SafeMiningSceneBuilder] Corridor bounds: center={bounds.center}, size={bounds.size}");
        return bounds;
    }

    private static void LogImportedTunnelAsset(GameObject tunnelPrefab)
    {
        Renderer[] renderers = tunnelPrefab.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(tunnelPrefab.transform.position, Vector3.zero);
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        int meshCount = tunnelPrefab.GetComponentsInChildren<MeshFilter>(true).Length;
        Debug.Log($"[SafeMiningSceneBuilder] Corridor import: root={tunnelPrefab.name}, children={tunnelPrefab.transform.childCount}, meshes={meshCount}, renderers={renderers.Length}, bounds={bounds.size}");
    }

    private static void CreateSafetyPlatform(Bounds corridorBounds, Transform parent)
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "SafetyPlatform_FallRecovery";
        platform.transform.SetParent(parent);
        platform.transform.position = new Vector3(corridorBounds.center.x, corridorBounds.min.y - 0.35f, corridorBounds.center.z);
        platform.transform.localScale = new Vector3(corridorBounds.size.x + 20f, 0.5f, corridorBounds.size.z + 20f);
        platform.isStatic = true;
        Renderer renderer = platform.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        Collider collider = platform.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
    }

    private static Image CreateDangerOverlay(Transform canvas)
    {
        GameObject overlayGO = new GameObject("FPP_DangerOverlay");
        overlayGO.transform.SetParent(canvas, false);
        RectTransform rect = overlayGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image overlay = overlayGO.AddComponent<Image>();
        // Parent only controls alpha. Four thin red edges form a non-obstructive vignette.
        overlay.enabled = false;
        overlay.raycastTarget = false;
        CanvasGroup group = overlayGO.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        CreateOverlayEdge(overlayGO.transform, "Top", new Vector2(0, .88f), new Vector2(1, 1));
        CreateOverlayEdge(overlayGO.transform, "Bottom", new Vector2(0, 0), new Vector2(1, .12f));
        CreateOverlayEdge(overlayGO.transform, "Left", new Vector2(0, .12f), new Vector2(.09f, .88f));
        CreateOverlayEdge(overlayGO.transform, "Right", new Vector2(.91f, .12f), new Vector2(1, .88f));
        return overlay;
    }

    private static void CreateOverlayEdge(Transform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject edge = new GameObject("Vignette_" + name);
        edge.transform.SetParent(parent, false);
        RectTransform rect = edge.AddComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        Image image = edge.AddComponent<Image>(); image.color = new Color(1f, .05f, .02f, .85f); image.raycastTarget = false;
    }

    private static void CreateMineLantern(Transform parent, Vector3 position)
    {
        GameObject lamp = new GameObject("MineLantern");
        lamp.transform.SetParent(parent);
        lamp.transform.position = position;

        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1.0f, 0.85f, 0.6f);
        light.range = 16f;
        light.intensity = 2.5f;

        GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "Bulb";
        bulb.transform.SetParent(lamp.transform);
        bulb.transform.localPosition = Vector3.zero;
        bulb.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        Object.DestroyImmediate(bulb.GetComponent<Collider>());
        Material bulbMat = CreateOrLoadMaterial("Assets/Materials/M_Bulb.mat", new Color(1f, 0.9f, 0.7f), 0.9f, false, Color.yellow * 2f);
        bulb.GetComponent<Renderer>().sharedMaterial = bulbMat;
    }

    private static GameObject CreateExitPoint(string name, Vector3 position, Transform parent, Material exitMat)
    {
        GameObject ep = new GameObject(name);
        ep.tag = "ExitPoint";
        ep.transform.position = position;
        ep.transform.SetParent(parent);

        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "BeaconVisual";
        beacon.transform.SetParent(ep.transform);
        beacon.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        beacon.transform.localScale = new Vector3(2.0f, 0.1f, 2.0f);
        beacon.GetComponent<Renderer>().sharedMaterial = exitMat;
        Object.DestroyImmediate(beacon.GetComponent<Collider>());

        Light greenLight = ep.AddComponent<Light>();
        greenLight.type = LightType.Point;
        greenLight.color = new Color(0.1f, 1.0f, 0.4f);
        greenLight.range = 10f;
        greenLight.intensity = 3.5f;

        return ep;
    }

    private static GameObject CreateWorkerAgent(string name, Vector3 position, Material suitMat)
    {
        GameObject worker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        worker.name = name;
        worker.transform.position = position + Vector3.up * 1.0f;
        worker.transform.localScale = new Vector3(0.9f, 1.0f, 0.9f);
        worker.GetComponent<Renderer>().sharedMaterial = suitMat;

        NavMeshAgent agent = worker.AddComponent<NavMeshAgent>();
        agent.radius = 0.45f;
        agent.height = 1.8f;
        agent.speed = 4.0f;
        agent.acceleration = 12f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 1.5f;

        worker.AddComponent<WorkerAgent>();

        GameObject headlamp = new GameObject("Headlamp");
        headlamp.transform.SetParent(worker.transform);
        headlamp.transform.localPosition = new Vector3(0f, 0.7f, 0.4f);
        Light spot = headlamp.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = Color.white;
        spot.range = 16f;
        spot.spotAngle = 60f;
        spot.intensity = 3.0f;

        return worker;
    }

    private static GameObject CreatePlayerFPP(string name, Vector3 position)
    {
        GameObject player = new GameObject(name);
        player.transform.position = position + Vector3.up * 0.9f;

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.32f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.skinWidth = 0.03f;
        cc.stepOffset = 0.2f;

        GameObject camGO = new GameObject("FPP_Camera");
        camGO.transform.SetParent(player.transform);
        camGO.transform.localPosition = new Vector3(0f, 1.65f, 0.1f);

        Camera cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.1f;

        // Headlamp for FPP Player
        Light spot = camGO.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1.0f, 0.95f, 0.85f);
        spot.range = 20f;
        spot.spotAngle = 65f;
        spot.intensity = 3.5f;

        FPPController fpp = player.AddComponent<FPPController>();
        fpp.cameraHolder = camGO.transform;
        fpp.SetControlEnabled(false); // Initially disabled until switched

        player.SetActive(false); // Hidden during auto simulation start
        return player;
    }

    private static DangerZone CreateDangerZone(string name, Vector3 position, Vector3 size, Transform parent, Material activeMat, Material inactiveMat)
    {
        GameObject dzGO = new GameObject(name);
        dzGO.transform.position = position;
        dzGO.transform.SetParent(parent);

        BoxCollider col = dzGO.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = size;
        col.center = new Vector3(0f, size.y * 0.5f, 0f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "ZoneVisual";
        visual.transform.SetParent(dzGO.transform);
        visual.transform.localPosition = col.center;
        visual.transform.localScale = size;
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        Renderer visualRend = visual.GetComponent<Renderer>();
        visualRend.sharedMaterial = inactiveMat;

        DangerZone dz = dzGO.AddComponent<DangerZone>();
        dz.zoneName = name;
        dz.ConfigureVisuals(visualRend, activeMat.color, inactiveMat.color);
        dz.isActive = false;

        return dz;
    }

    private static Canvas CreateEvacuationHUDCanvas(
        EvacuationController controller,
        ScenarioRunner runner,
        RenderTexture minimapRT,
        Transform ep1, Transform ep2, Transform ep3,
        DangerZone dz1, DangerZone dz2, DangerZone dz3,
        GameObject workerGO, GameObject playerFPPGO)
    {
        GameObject canvasGO = new GameObject("EvacuationHUDCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        // Panel HUD Utama (Background Kiri Atas)
        GameObject panelGO = new GameObject("Panel_HUD");
        panelGO.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0, 1);
        panelRT.anchorMax = new Vector2(0, 1);
        panelRT.pivot = new Vector2(0, 1);
        panelRT.anchoredPosition = new Vector2(15, -15);
        panelRT.sizeDelta = new Vector2(400, 310);

        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.1f, 0.14f, 0.85f);

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Title Text
        CreateUIText("TitleText", "SAFE-MINING EVAC: ADAPTIVE EVACUATION", panelGO.transform, new Vector2(15, -10), new Vector2(370, 30), defaultFont, 15, FontStyle.Bold, Color.cyan);

        // Mode Text
        Text modeTxt = CreateUIText("ModeText", "MODE: ADAPTIF", panelGO.transform, new Vector2(15, -42), new Vector2(370, 25), defaultFont, 13, FontStyle.Bold, Color.green);

        // Target Exit Text
        Text targetTxt = CreateUIText("TargetText", "Target Exit: Calculating...", panelGO.transform, new Vector2(15, -67), new Vector2(370, 25), defaultFont, 12, FontStyle.Normal, Color.white);

        // Reroute Count Text
        Text rerouteTxt = CreateUIText("RerouteText", "Jumlah Reroute: 0", panelGO.transform, new Vector2(15, -92), new Vector2(370, 25), defaultFont, 12, FontStyle.Normal, Color.white);

        // Timer Text
        Text timerTxt = CreateUIText("TimerText", "Waktu Evakuasi: 0.0 s", panelGO.transform, new Vector2(15, -117), new Vector2(370, 25), defaultFont, 12, FontStyle.Normal, Color.yellow);

        // Response Time Text
        Text respTxt = CreateUIText("RespTimeText", "Waktu Respons: 0.000 ms", panelGO.transform, new Vector2(15, -142), new Vector2(370, 25), defaultFont, 12, FontStyle.Normal, Color.white);

        // Scenario Phase Text
        Text phaseTxt = CreateUIText("PhaseText", "Fase Skenario: Initializing", panelGO.transform, new Vector2(15, -167), new Vector2(370, 25), defaultFont, 12, FontStyle.Italic, Color.white);

        // Danger Zones List Text
        Text dzTxt = CreateUIText("DangerStatusText", "Status Zona Bahaya...", panelGO.transform, new Vector2(15, -195), new Vector2(370, 65), defaultFont, 11, FontStyle.Normal, Color.white);

        // Toggle Adaptive/Static Mode Button
        GameObject btnToggleGO = CreateUIButton("Btn_ToggleMode", "Ubah Mode Adaptive/Static", panelGO.transform, new Vector2(15, -265), new Vector2(180, 32), defaultFont, 11);
        Button btnToggle = btnToggleGO.GetComponent<Button>();
        Text btnToggleTxt = btnToggleGO.GetComponentInChildren<Text>();

        // Toggle View Mode (Simulasi / FPP) Button
        GameObject btnViewGO = CreateUIButton("Btn_ToggleViewMode", "Mode: SIMULASI (Klik/Tab ke FPP)", panelGO.transform, new Vector2(205, -265), new Vector2(180, 32), defaultFont, 11);

        // Attach EvacuationHUD Script
        EvacuationHUD hud = canvasGO.AddComponent<EvacuationHUD>();
        hud.controller = controller;
        hud.scenarioRunner = runner;
        hud.modeText = modeTxt;
        hud.workerTargetText = targetTxt;
        hud.rerouteCountText = rerouteTxt;
        hud.timerText = timerTxt;
        hud.responseTimeText = respTxt;
        hud.scenarioPhaseText = phaseTxt;
        hud.dangerZonesStatusText = dzTxt;
        hud.toggleModeButton = btnToggle;
        hud.toggleModeButtonText = btnToggleTxt;

        // =========================================================================
        // MINIMAP UI OVERLAY (Kanan Atas Screen)
        // =========================================================================
        GameObject minimapPanel = new GameObject("Panel_Minimap");
        minimapPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform mmRT = minimapPanel.AddComponent<RectTransform>();
        mmRT.anchorMin = new Vector2(1, 1);
        mmRT.anchorMax = new Vector2(1, 1);
        mmRT.pivot = new Vector2(1, 1);
        mmRT.anchoredPosition = new Vector2(-15, -15);
        mmRT.sizeDelta = new Vector2(180, 180);

        Image mmBg = minimapPanel.AddComponent<Image>();
        mmBg.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);

        // RawImage for Render Texture
        GameObject rawImgGO = new GameObject("MinimapRawImage");
        rawImgGO.transform.SetParent(minimapPanel.transform, false);
        RectTransform rawRT = rawImgGO.AddComponent<RectTransform>();
        rawRT.anchorMin = Vector2.zero;
        rawRT.anchorMax = Vector2.one;
        rawRT.sizeDelta = Vector2.zero;
        RawImage rawImg = rawImgGO.AddComponent<RawImage>();
        rawImg.texture = minimapRT;

        // Minimap Controller Script
        MinimapController mmController = minimapPanel.AddComponent<MinimapController>();
        mmController.activeTarget = workerGO.transform;
        mmController.exitPoints = new Transform[] { ep1, ep2, ep3 };
        mmController.dangerZones = new DangerZone[] { dz1, dz2, dz3 };

        // Player Marker (Blue Dot)
        GameObject pMarkerGO = new GameObject("PlayerMarker");
        pMarkerGO.transform.SetParent(minimapPanel.transform, false);
        RectTransform pmRT = pMarkerGO.AddComponent<RectTransform>();
        pmRT.sizeDelta = new Vector2(12, 12);
        Image pmImg = pMarkerGO.AddComponent<Image>();
        pmImg.color = Color.cyan;
        mmController.playerMarker = pmRT;

        // Exit Markers (Green Dots)
        mmController.exitMarkers = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject exMarkerGO = new GameObject($"ExitMarker_{i+1}");
            exMarkerGO.transform.SetParent(minimapPanel.transform, false);
            RectTransform exRT = exMarkerGO.AddComponent<RectTransform>();
            exRT.sizeDelta = new Vector2(10, 10);
            Image exImg = exMarkerGO.AddComponent<Image>();
            exImg.color = Color.green;
            mmController.exitMarkers[i] = exRT;
        }

        // Danger Markers (Red Dots)
        mmController.dangerMarkers = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject dzMarkerGO = new GameObject($"DangerMarker_{i+1}");
            dzMarkerGO.transform.SetParent(minimapPanel.transform, false);
            RectTransform dzRT = dzMarkerGO.AddComponent<RectTransform>();
            dzRT.sizeDelta = new Vector2(14, 14);
            Image dzImg = dzMarkerGO.AddComponent<Image>();
            dzImg.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            mmController.dangerMarkers[i] = dzRT;
        }

        return canvas;
    }

    private static Text CreateUIText(string name, string text, Transform parent, Vector2 pos, Vector2 size, Font font, int fontSize, FontStyle style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text txt = go.AddComponent<Text>();
        txt.font = font;
        txt.text = text;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.raycastTarget = false;
        return txt;
    }

    private static GameObject CreateUIButton(string name, string label, Transform parent, Vector2 pos, Vector2 size, Font font, int fontSize)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);
        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.2f, 0.45f, 0.75f, 0.9f);

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.3f, 0.55f, 0.9f, 1f);
        cb.pressedColor = new Color(0.15f, 0.35f, 0.6f, 1f);
        btn.colors = cb;

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        Text txt = txtGO.AddComponent<Text>();
        txt.font = font;
        txt.text = label;
        txt.fontSize = fontSize;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;

        return btnGO;
    }

    private static void SetupCamera(Transform target)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            mainCam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }

        CameraFollow follow = mainCam.gameObject.GetComponent<CameraFollow>();
        if (follow == null) follow = mainCam.gameObject.AddComponent<CameraFollow>();
        follow.target = target;
        follow.offset = new Vector3(0f, 22f, -18f);
        mainCam.transform.position = target.position + follow.offset;
        mainCam.transform.LookAt(target.position + Vector3.up);
    }

    private static Material CreateOrLoadMaterial(string path, Color color, float smoothness, bool transparent = false, Color? emissionColor = null)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            if (transparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            if (emissionColor.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor.Value);
            }

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static void EnsureExitPointTag()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        bool exists = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue.Equals("ExitPoint"))
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = "ExitPoint";
            tagManager.ApplyModifiedProperties();
        }
    }

    private static int EnsureLayer(string layerName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return i;
        }

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }

        Debug.LogError($"[SafeMiningSceneBuilder] Tidak ada slot layer kosong untuk {layerName}.");
        return 0;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null) Object.DestroyImmediate(legacyModule);
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }
    }
}
