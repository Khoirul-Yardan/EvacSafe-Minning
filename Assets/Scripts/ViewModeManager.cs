using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.EventSystems;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using System.Collections.Generic;

public enum ViewMode
{
    AutoSimulation,
    FirstPerson
}

public class ViewModeManager : MonoBehaviour
{
    [Header("Mode References")]
    public ViewMode currentMode = ViewMode.AutoSimulation;

    public GameObject workerAgentGO;
    public GameObject playerFPPGO;
    public Camera mainSimulationCamera;
    public FPPController fppController;
    public MinimapController minimapController;

    [Header("UI Controls")]
    public Button toggleViewModeButton;
    public Text toggleViewModeButtonText;
    public float wideMapScale = 1.0f;

    private bool wideMapApplied;
    private GameObject simulationPanel;
    private GameObject fppMissionPanel;
    private Text fppMissionText;

    private void Start()
    {
        if (toggleViewModeButton != null)
        {
            toggleViewModeButton.onClick.AddListener(ToggleViewMode);
        }

        SetViewMode(currentMode);
    }

    private void Awake()
    {
        RemoveGeneratedRuntimeObjects();
        RebuildRuntimeNavMesh();
        ConfigureAuthoredMapRoute();
        ConfigureMinimapCamera();
        CreateFppMissionPanel();

        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null) Destroy(legacyModule);
        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void RebuildRuntimeNavMesh()
    {
        Transform mapRoot = GameObject.Find("Environment_UndergroundMine")?.transform;
        NavMeshSurface surface = mapRoot != null ? mapRoot.GetComponent<NavMeshSurface>() : null;
        if (surface == null) return;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();
        Debug.Log("[ViewModeManager] NavMesh dibangun ulang setelah semua instance corridor.fbx ditambahkan.");
    }

    private void ConfigureMinimapCamera()
    {
        MinimapController controller = minimapController != null ? minimapController : FindFirstObjectByType<MinimapController>();
        if (controller == null) return;

        Camera minimapCamera = GameObject.Find("MinimapCamera")?.GetComponent<Camera>();
        if (minimapCamera == null) return;

        Vector3 center = new Vector3(controller.worldCenter.x, 0f, controller.worldCenter.y);
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = Mathf.Max(controller.worldSize.x, controller.worldSize.y) * 0.55f;
        minimapCamera.transform.position = center + Vector3.up * 80f;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        controller.minimapCamera = minimapCamera;
        controller.followActiveTarget = true;
    }

    private void CreateFppMissionPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        simulationPanel = canvas.transform.Find("Panel_HUD")?.gameObject;
        fppMissionPanel = canvas.transform.Find("FPP_MissionPanel")?.gameObject;
        if (fppMissionPanel == null)
        {
            fppMissionPanel = new GameObject("FPP_MissionPanel");
            fppMissionPanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = fppMissionPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -18f);
            panelRect.sizeDelta = new Vector2(470f, 70f);
            Image panelImage = fppMissionPanel.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.05f, 0.07f, 0.82f);

            GameObject textObject = new GameObject("MissionText");
            textObject.transform.SetParent(fppMissionPanel.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-16f, -8f);
            fppMissionText = textObject.AddComponent<Text>();
            fppMissionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fppMissionText.fontSize = 16;
            fppMissionText.alignment = TextAnchor.MiddleCenter;
            fppMissionText.color = Color.white;
            fppMissionText.raycastTarget = false;
        }
        else
        {
            fppMissionText = fppMissionPanel.GetComponentInChildren<Text>();
        }
        fppMissionPanel.SetActive(false);
        if (fppController != null) fppController.missionText = fppMissionText;
    }

    private void RemoveGeneratedRuntimeObjects()
    {
        GameObject safetyPlatform = GameObject.Find("SafetyPlatform_FallRecovery");
        if (safetyPlatform != null) Destroy(safetyPlatform);
    }

    private void ConfigureAuthoredMapRoute()
    {
        Transform mapRoot = GameObject.Find("Environment_UndergroundMine")?.transform;
        if (mapRoot == null) return;

        // The editor layout owns this coordinate. Never replace it with a bounds sample at runtime.
        Transform entrance = GameObject.Find("EntrancePoint")?.transform;
        if (entrance != null)
        {
            if (!NavMesh.SamplePosition(entrance.position, out NavMeshHit entranceHit, 4f, NavMesh.AllAreas))
            {
                Debug.LogError($"[ViewModeManager] EntrancePoint tidak berada di NavMesh: {entrance.position}");
                return;
            }
            Vector3 navStart = entranceHit.position;
            PlaceActor(workerAgentGO, navStart);
            if (playerFPPGO != null)
            {
                CharacterController cc = playerFPPGO.GetComponent<CharacterController>();
                playerFPPGO.transform.position = navStart + (cc != null ? Vector3.up * (cc.height * .5f) : Vector3.zero);
                playerFPPGO.transform.rotation = Quaternion.identity;
            }
            Vector3 heading = (GameObject.Find("JunctionPoint")?.transform.position ?? (navStart + Vector3.forward * 5f)) - navStart;
            heading.y = 0f;
            if (heading.sqrMagnitude > .01f)
            {
                Quaternion rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
                if (workerAgentGO != null) workerAgentGO.transform.rotation = rotation;
                if (playerFPPGO != null) playerFPPGO.transform.rotation = rotation;
            }
            CameraFollow follow = mainSimulationCamera != null ? mainSimulationCamera.GetComponent<CameraFollow>() : null;
            if (follow != null && workerAgentGO != null) follow.target = workerAgentGO.transform;
            Debug.Log($"[ViewModeManager] Layout authority: Entrance={navStart}, Worker={workerAgentGO?.transform.position}, Player={playerFPPGO?.transform.position}");
            return;
        }

        Renderer[] corridorRenderers = GetCorridorRenderers(mapRoot);
        if (corridorRenderers.Length == 0) return;

        Bounds bounds = corridorRenderers[0].bounds;
        for (int i = 1; i < corridorRenderers.Length; i++) bounds.Encapsulate(corridorRenderers[i].bounds);
        List<Vector3> navPoints = CollectNavMeshBoundaryPoints(bounds);
        if (navPoints.Count < 2) return;

        Vector3 start = navPoints[0];
        Vector3 finish = navPoints[1];
        float longestPath = -1f;
        for (int i = 0; i < navPoints.Count; i++)
        {
            for (int j = i + 1; j < navPoints.Count; j++)
            {
                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(navPoints[i], navPoints[j], NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float length = PathLength(path);
                if (length > longestPath)
                {
                    longestPath = length;
                    start = navPoints[i];
                    finish = navPoints[j];
                }
            }
        }

        NavMeshPath chosenRoute = new NavMeshPath();
        if (NavMesh.CalculatePath(start, finish, NavMesh.AllAreas, chosenRoute) && chosenRoute.corners.Length > 2)
        {
            start = Vector3.MoveTowards(chosenRoute.corners[0], chosenRoute.corners[1], 2f);
            int last = chosenRoute.corners.Length - 1;
            finish = Vector3.MoveTowards(chosenRoute.corners[last], chosenRoute.corners[last - 1], 1f);
            Vector3 startDirection = chosenRoute.corners[1] - chosenRoute.corners[0];
            startDirection.y = 0f;
            if (startDirection.sqrMagnitude > 0.01f)
            {
                Quaternion heading = Quaternion.LookRotation(startDirection.normalized, Vector3.up);
                if (playerFPPGO != null) playerFPPGO.transform.rotation = heading;
                if (workerAgentGO != null) workerAgentGO.transform.rotation = heading;
            }
        }

        PlaceActor(workerAgentGO, start);
        PlaceActor(playerFPPGO, start);

        GameObject[] exits = GameObject.FindGameObjectsWithTag("ExitPoint");
        if (exits.Length > 0) exits[0].transform.position = finish;

        EvacuationController controller = FindFirstObjectByType<EvacuationController>();
        if (controller != null)
        {
            controller.exitPoints = new List<Transform>();
            foreach (GameObject exit in exits)
            {
                if (exit.activeInHierarchy) controller.exitPoints.Add(exit.transform);
            }
        }

        if (minimapController != null) minimapController.SetWorldBounds(bounds);

        DangerZone[] zones = FindObjectsByType<DangerZone>(FindObjectsSortMode.None);
        NavMeshPath route = new NavMeshPath();
        if (NavMesh.CalculatePath(start, finish, NavMesh.AllAreas, route) && route.corners.Length > 1)
        {
            int checkpointIndex = Mathf.Clamp(route.corners.Length / 2, 1, route.corners.Length - 1);
            GameObject checkpointObject = GameObject.Find("Checkpoint_1") ?? new GameObject("Checkpoint_1");
            checkpointObject.transform.position = route.corners[checkpointIndex];
            checkpointObject.transform.SetParent(mapRoot, true);
            Transform[] checkpoints = new[] { checkpointObject.transform };
            if (minimapController != null) minimapController.SetCheckpointPoints(checkpoints);
            if (fppController != null) fppController.SetCheckpointPoints(checkpoints);

            for (int i = 0; i < zones.Length; i++)
            {
                Vector3 point = route.corners[Mathf.Clamp((i + 1) * route.corners.Length / (zones.Length + 1), 1, route.corners.Length - 1)];
                zones[i].transform.position = new Vector3(point.x, zones[i].transform.position.y, point.z);
            }
        }

        Debug.Log($"[ViewModeManager] Authored map aktif: Start={start}, Finish={finish}, Path={longestPath:F1}m. Tidak ada cabang/map tambahan.");
    }

    private Renderer[] GetCorridorRenderers(Transform mapRoot)
    {
        List<Renderer> renderers = new List<Renderer>();
        foreach (Renderer renderer in mapRoot.GetComponentsInChildren<Renderer>(true))
        {
            string objectName = renderer.gameObject.name.ToLowerInvariant();
            if (objectName.Contains("corridor") || objectName.Contains("tunnel")) renderers.Add(renderer);
        }
        return renderers.ToArray();
    }

    private List<Vector3> CollectNavMeshBoundaryPoints(Bounds bounds)
    {
        List<Vector3> points = new List<Vector3>();
        for (int x = 0; x <= 4; x++)
        {
            for (int z = 0; z <= 4; z++)
            {
                Vector3 sample = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, x / 4f),
                    bounds.center.y,
                    Mathf.Lerp(bounds.min.z, bounds.max.z, z / 4f));
                if (NavMesh.SamplePosition(sample, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                {
                    bool duplicate = false;
                    foreach (Vector3 point in points)
                    {
                        if (Vector3.Distance(point, hit.position) < 2f) duplicate = true;
                    }
                    if (!duplicate) points.Add(hit.position);
                }
            }
        }
        return points;
    }

    private float PathLength(NavMeshPath path)
    {
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length;
    }

    private void PlaceActor(GameObject actor, Vector3 position)
    {
        if (actor == null) return;
        actor.transform.position = position;
    }

    private void EnsureRuntimeNetwork()
    {
        Transform mapRoot = GameObject.Find("Environment_UndergroundMine")?.transform;
        if (mapRoot == null || mapRoot.Find("Corridor_Branch_South") != null) return;

        Transform template = mapRoot.Find("Corridor_Map");
        if (template == null)
        {
            foreach (Transform child in mapRoot)
            {
                if (child.name.ToLowerInvariant().Contains("corridor"))
                {
                    template = child;
                    break;
                }
            }
        }

        if (template == null) return;

        Vector3 entryAnchor = new Vector3(0f, 0f, 9.41f);
        Vector3 branchAnchor = new Vector3(-12f, 0f, -47.05f);
        Vector3 westAnchor = new Vector3(-25.78f, 0f, -32.64f);
        Vector3 southPosition = branchAnchor - entryAnchor;
        CreateRuntimeBranch(template, "Corridor_Branch_South", southPosition, Quaternion.identity);
        Quaternion eastRotation = Quaternion.Euler(0f, 90f, 0f);
        Vector3 eastPosition = branchAnchor + new Vector3(19.32f, 0f, 0f) - eastRotation * entryAnchor;
        CreateRuntimeBranch(template, "Corridor_Branch_East", eastPosition, eastRotation);
        Quaternion westRotation = Quaternion.Euler(0f, 270f, 0f);
        Vector3 westPosition = westAnchor - westRotation * entryAnchor;
        CreateRuntimeBranch(template, "Corridor_Branch_West", westPosition, westRotation);
        GameObject[] exits = GameObject.FindGameObjectsWithTag("ExitPoint");
        if (exits.Length >= 3)
        {
            exits[0].transform.position = WorldConnector(southPosition, Quaternion.identity, branchAnchor);
            exits[1].transform.position = WorldConnector(eastPosition, eastRotation, branchAnchor);
            exits[2].transform.position = WorldConnector(westPosition, westRotation, branchAnchor);
            for (int i = 0; i < 3; i++) exits[i].transform.position = new Vector3(exits[i].transform.position.x, -2.6f, exits[i].transform.position.z);
        }
        Debug.Log("[ViewModeManager] Runtime network: Corridor_Map diperluas menjadi 4 instance dengan 3 cabang.");
    }

    private void CreateRuntimeBranch(Transform template, string branchName, Vector3 position, Quaternion rotation)
    {
        Transform branch = Instantiate(template, position, rotation, template.parent);
        branch.name = branchName;
    }

    private Vector3 WorldConnector(Vector3 segmentPosition, Quaternion segmentRotation, Vector3 localConnector)
    {
        return segmentPosition + segmentRotation * localConnector;
    }

    private void ApplyWideMapLayout()
    {
        if (wideMapApplied || wideMapScale <= 1.01f) return;
        Transform mapRoot = GameObject.Find("Environment_UndergroundMine")?.transform;
        if (mapRoot == null) return;

        Renderer[] mapRenderers = mapRoot.GetComponentsInChildren<Renderer>(true);
        if (mapRenderers.Length == 0) return;

        Bounds bounds = mapRenderers[0].bounds;
        for (int i = 1; i < mapRenderers.Length; i++) bounds.Encapsulate(mapRenderers[i].bounds);
        Vector3 center = bounds.center;

        mapRoot.localScale = new Vector3(
            mapRoot.localScale.x * wideMapScale,
            mapRoot.localScale.y,
            mapRoot.localScale.z * wideMapScale);

        GameObject[] exitObjects = GameObject.FindGameObjectsWithTag("ExitPoint");
        foreach (GameObject exitObject in exitObjects)
        {
            Transform exit = exitObject != null ? exitObject.transform : null;
            if (exit != null && !exit.IsChildOf(mapRoot)) MoveAroundCenter(exit, center);
        }

        DangerZone[] zones = FindObjectsByType<DangerZone>(FindObjectsSortMode.None);
        foreach (DangerZone zone in zones)
        {
            if (zone != null && !zone.transform.IsChildOf(mapRoot)) MoveAroundCenter(zone.transform, center);
        }

        MoveAroundCenter(workerAgentGO != null ? workerAgentGO.transform : null, center);
        MoveAroundCenter(playerFPPGO != null ? playerFPPGO.transform : null, center);

        NavMeshSurface surface = mapRoot.GetComponent<NavMeshSurface>();
        if (surface != null) surface.BuildNavMesh();

        Bounds scaledBounds = mapRenderers[0].bounds;
        for (int i = 1; i < mapRenderers.Length; i++) scaledBounds.Encapsulate(mapRenderers[i].bounds);
        if (minimapController != null)
        {
            minimapController.SetWorldBounds(scaledBounds);
        }
        ResizeSafetyPlatform(scaledBounds);

        wideMapApplied = true;
        Debug.Log($"[ViewModeManager] Map diperlebar {wideMapScale:F2}x pada sumbu horizontal dan NavMesh dibangun ulang.");
    }

    private void MoveAroundCenter(Transform target, Vector3 center)
    {
        if (target == null) return;
        Vector3 offset = target.position - center;
        target.position = center + new Vector3(offset.x * wideMapScale, offset.y, offset.z * wideMapScale);
    }

    private void EnsureSafetyPlatform()
    {
        if (GameObject.Find("SafetyPlatform_FallRecovery") != null) return;

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        Bounds bounds = new Bounds();
        bool foundCorridor = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.gameObject.name.ToLowerInvariant().Contains("corridor")) continue;
            if (!foundCorridor)
            {
                bounds = renderer.bounds;
                foundCorridor = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!foundCorridor) return;

        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "SafetyPlatform_FallRecovery";
        platform.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.35f, bounds.center.z);
        platform.transform.localScale = new Vector3(bounds.size.x + 20f, 0.5f, bounds.size.z + 20f);
        Renderer platformRenderer = platform.GetComponent<Renderer>();
        if (platformRenderer != null) platformRenderer.enabled = false;
    }

    private void ResizeSafetyPlatform(Bounds bounds)
    {
        GameObject platform = GameObject.Find("SafetyPlatform_FallRecovery");
        if (platform == null) return;
        platform.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.35f, bounds.center.z);
        platform.transform.localScale = new Vector3(bounds.size.x + 20f, 0.5f, bounds.size.z + 20f);
    }

    private void Update()
    {
        // Hotkey Tab to toggle view mode
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleViewMode();
        }

        UpdateFppMission();
    }

    private void UpdateFppMission()
    {
        if (fppMissionText == null || currentMode != ViewMode.FirstPerson) return;

        EvacuationController controller = FindFirstObjectByType<EvacuationController>();
        if (controller == null || fppController == null) return;

        if (fppController.checkpointAlertUntil > Time.time)
        {
            fppMissionText.text = $"<color=#FFE45C>CHECKPOINT TERCAPAI</color>\n{fppController.lastCheckpointName}: posisi aman tersimpan.";
            return;
        }

        Transform targetTransform = controller.workerAgent != null && controller.workerAgent.CurrentExitPoint != null
            ? controller.workerAgent.CurrentExitPoint
            : (controller.exitPoints != null && controller.exitPoints.Count > 0 ? controller.exitPoints[0] : null);
        string target = targetTransform != null
            ? targetTransform.name
            : "titik finish";
        float targetDistance = targetTransform != null
            ? Vector3.Distance(fppController.transform.position, targetTransform.position)
            : 0f;
        Vector3 targetDirection = targetTransform != null ? targetTransform.position - fppController.transform.position : Vector3.zero;
        float relativeAngle = targetDirection.sqrMagnitude > 0.01f
            ? Vector3.SignedAngle(fppController.transform.forward, targetDirection, Vector3.up)
            : 0f;
        string direction = Mathf.Abs(relativeAngle) < 25f ? "DEPAN" : relativeAngle > 0f ? "KANAN" : "KIRI";
        DangerZone nearestDanger = null;
        float nearestDistance = float.MaxValue;
        foreach (DangerZone zone in controller.dangerZones)
        {
            if (zone == null || !zone.isActive) continue;
            float distance = Vector3.Distance(fppController.transform.position, zone.ZoneBounds.ClosestPoint(fppController.transform.position));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestDanger = zone;
            }
        }

        if (nearestDanger != null && nearestDistance < 14f)
        {
            fppMissionText.text = $"<color=#FF5555>BAHAYA DI DEPAN: {nearestDanger.zoneName}</color>\nArah finish: {direction}. Hindari merah, lanjut ke {target} ({targetDistance:F1}m). Cyan=Start, kuning=Checkpoint.";
        }
        else
        {
            fppMissionText.text = $"<color=#62F5C4>MISI EVAKUASI</color>\nArah {direction} menuju {target} ({targetDistance:F1}m). Cyan=Start, kuning=Checkpoint, hijau=Finish.";
        }
    }

    public void ToggleViewMode()
    {
        ViewMode nextMode = currentMode == ViewMode.AutoSimulation ? ViewMode.FirstPerson : ViewMode.AutoSimulation;
        SetViewMode(nextMode);
    }

    public void SetViewMode(ViewMode mode)
    {
        currentMode = mode;

        if (mode == ViewMode.AutoSimulation)
        {
            // Activate WorkerAgent, enable Main Simulation Camera
            if (workerAgentGO != null) workerAgentGO.SetActive(true);
            if (playerFPPGO != null) playerFPPGO.SetActive(false);

            if (mainSimulationCamera != null) mainSimulationCamera.enabled = true;
            if (fppController != null) fppController.SetControlEnabled(false);

            if (minimapController != null && workerAgentGO != null)
            {
                minimapController.SetActiveTarget(workerAgentGO.transform);
            }

            if (toggleViewModeButtonText != null)
            {
                toggleViewModeButtonText.text = "Mode: SIMULASI (Klik/Tab ke FPP)";
            }
            if (simulationPanel != null) simulationPanel.SetActive(true);
            if (fppMissionPanel != null) fppMissionPanel.SetActive(false);

            Debug.Log("<color=cyan>[ViewModeManager] Mode berpindah ke: SIMULASI OTOMATIS</color>");
        }
        else
        {
            // Activate PlayerFPP, disable Main Simulation Camera
            if (workerAgentGO != null) workerAgentGO.SetActive(false);
            if (playerFPPGO != null) playerFPPGO.SetActive(true);

            if (mainSimulationCamera != null) mainSimulationCamera.enabled = false;
            Camera fppCamera = playerFPPGO != null ? playerFPPGO.GetComponentInChildren<Camera>(true) : null;
            if (fppCamera != null) fppCamera.enabled = true;
            if (fppController != null) fppController.SetControlEnabled(true);

            if (minimapController != null && playerFPPGO != null)
            {
                minimapController.SetActiveTarget(playerFPPGO.transform);
            }

            if (toggleViewModeButtonText != null)
            {
                toggleViewModeButtonText.text = "Mode: FIRST PERSON (Klik/Tab ke Simulasi)";
            }
            if (simulationPanel != null) simulationPanel.SetActive(false);
            if (fppMissionPanel != null) fppMissionPanel.SetActive(true);

            Debug.Log("<color=cyan>[ViewModeManager] Mode berpindah ke: FIRST PERSON (FPP MANUAL)</color>");
        }
    }
}
