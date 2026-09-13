using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class EvacuationController : MonoBehaviour
{
    [Header("Simulation Mode")]
    [Tooltip("Jika TRUE: rute adaptif mengikuti zona bahaya. Jika FALSE: rute statis tetap ke exit awal.")]
    public bool adaptiveModeEnabled = true;

    [Header("Entity References")]
    public WorkerAgent workerAgent;
    public List<DangerZone> dangerZones = new List<DangerZone>();
    public List<Transform> exitPoints = new List<Transform>();

    [Header("Baseline & Metrics")]
    public Transform staticAssignedExit;
    public int rerouteCount = 0;
    public float lastSystemResponseTimeMs = 0f;
    public float totalEvacuationTime = 0f;
    public bool isSimulationRunning = false;
    public string currentStatusMessage = "Menunggu Inisialisasi...";

    public event Action OnStateUpdated;

    private Stopwatch stopwatch = new Stopwatch();

    private void Awake()
    {
        AutoDiscoverReferences();
    }

    private void Start()
    {
        AutoDiscoverReferences();
        AlignDangerZonesToNavMeshRoutes();
        SubscribeToDangerZones();

        if (workerAgent != null)
        {
            workerAgent.OnArrivedAtExit += HandleWorkerArrived;
        }

        // Tentukan exit awal (terdekat dan aman saat awal)
        InitializeInitialRoute();
        isSimulationRunning = true;
    }

    private void Update()
    {
        if (isSimulationRunning && workerAgent != null && workerAgent.isEvacuating)
        {
            totalEvacuationTime += Time.deltaTime;
        }
    }

    public void AutoDiscoverReferences()
    {
        if (workerAgent == null)
        {
            workerAgent = FindFirstObjectByType<WorkerAgent>();
        }

        if (dangerZones == null || dangerZones.Count == 0)
        {
            dangerZones = new List<DangerZone>(FindObjectsByType<DangerZone>(FindObjectsSortMode.None));
        }

        if (exitPoints == null || exitPoints.Count == 0)
        {
            exitPoints = new List<Transform>();
            GameObject[] exits = GameObject.FindGameObjectsWithTag("ExitPoint");
            foreach (var ex in exits)
            {
                exitPoints.Add(ex.transform);
            }
        }
    }

    private void SubscribeToDangerZones()
    {
        foreach (var zone in dangerZones)
        {
            if (zone != null)
            {
                zone.OnZoneStatusChanged.RemoveListener(HandleZoneStatusChanged);
                zone.OnZoneStatusChanged.AddListener(HandleZoneStatusChanged);
            }
        }
    }

    private void AlignDangerZonesToNavMeshRoutes()
    {
        if (workerAgent == null || dangerZones == null || dangerZones.Count == 0) return;

        Vector3 start = workerAgent.transform.position;
        if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, 10f, NavMesh.AllAreas)) return;
        start = startHit.position;

        for (int i = 0; i < dangerZones.Count; i++)
        {
            DangerZone zone = dangerZones[i];
            if (zone == null) continue;

            Vector3 candidate = start;
            if (exitPoints != null && exitPoints.Count > 0)
            {
                Transform exit = exitPoints[Mathf.Min(i, exitPoints.Count - 1)];
                if (exit != null && NavMesh.SamplePosition(exit.position, out NavMeshHit exitHit, 10f, NavMesh.AllAreas))
                {
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(start, exitHit.position, NavMesh.AllAreas, path) && path.corners.Length > 1)
                    {
                        candidate = path.corners[Mathf.Max(1, path.corners.Length / 2)];
                    }
                }
            }

            if (NavMesh.SamplePosition(candidate, out NavMeshHit routeHit, 8f, NavMesh.AllAreas))
            {
                Vector3 position = zone.transform.position;
                position.x = routeHit.position.x;
                position.z = routeHit.position.z;
                zone.transform.position = position;
            }
        }
    }

    public void InitializeInitialRoute()
    {
        if (workerAgent == null || exitPoints.Count == 0)
        {
            Debug.LogWarning("[EvacuationController] WorkerAgent atau ExitPoint belum siap!");
            return;
        }

        Transform bestExit = FindBestExit(out float bestDist);
        if (bestExit != null)
        {
            staticAssignedExit = bestExit;
            workerAgent.SetDestination(bestExit);
            currentStatusMessage = $"Menuju Exit Awal: {bestExit.name} ({bestDist:F1}m)";
            Debug.Log($"<color=cyan>[EvacuationController]</color> Inisialisasi Jalur Awal -> Exit: {bestExit.name} (Jarak: {bestDist:F1}m)");
        }
        else
        {
            currentStatusMessage = "PERINGATAN: Tidak ada rute exit awal yang valid!";
            Debug.LogError("[EvacuationController] Tidak ada rute exit awal yang valid!");
        }

        OnStateUpdated?.Invoke();
    }

    public void HandleZoneStatusChanged(DangerZone zone, bool isActive)
    {
        if (!isSimulationRunning) return;

        Debug.Log($"<color=yellow>[EvacuationController]</color> Menerima sinyal perubahan zona '{zone.zoneName}' -> Active={isActive}");

        if (!adaptiveModeEnabled)
        {
            currentStatusMessage = $"[MODE STATIS] Zona '{zone.zoneName}' berubah, namun rute dipertahankan ke {staticAssignedExit?.name}";
            Debug.Log($"<color=orange>[EvacuationController - MODE STATIS]</color> Zona {zone.zoneName} berubah, rute statis TETAP menuju {staticAssignedExit?.name} (Baseline tanpa reroute).");
            OnStateUpdated?.Invoke();
            return;
        }

        // MODE ADAPTIF: Hitung ulang jalur teraman dan catat waktu respons sistem
        stopwatch.Restart();

        Transform safestExit = FindBestExit(out float shortestDistance);

        stopwatch.Stop();
        lastSystemResponseTimeMs = (float)stopwatch.Elapsed.TotalMilliseconds;

        if (safestExit != null)
        {
            if (workerAgent.CurrentExitPoint != safestExit)
            {
                rerouteCount++;
                workerAgent.SetDestination(safestExit);
                currentStatusMessage = $"[REROUTE #{rerouteCount}] Beralih ke {safestExit.name} ({shortestDistance:F1}m) | Waktu Respons: {lastSystemResponseTimeMs:F3} ms";
                Debug.Log($"<color=magenta>[EvacuationController - REROUTE ADAPTIF #{rerouteCount}]</color> Timestamp: {Time.time:F2}s | Exit Terpilih: {safestExit.name} | Jarak: {shortestDistance:F1}m | Waktu Respons Sistem: {lastSystemResponseTimeMs:F3} ms");
            }
            else
            {
                currentStatusMessage = $"Jalur aktif ke {safestExit.name} tetap aman ({shortestDistance:F1}m) | Waktu Evaluasi: {lastSystemResponseTimeMs:F3} ms";
                Debug.Log($"[EvacuationController] Jalur ke exit saat ini ({safestExit.name}) tetap aman. Waktu evaluasi: {lastSystemResponseTimeMs:F3} ms");
            }
        }
        else
        {
            Transform fallbackExit = FindBestExitIgnoringDanger(out float fallbackDistance);
            if (fallbackExit != null)
            {
                workerAgent.SetDestination(fallbackExit);
                currentStatusMessage = $"PERINGATAN: Zona bahaya menutup rute aman; memakai rute darurat ke {fallbackExit.name} ({fallbackDistance:F1}m)";
                Debug.LogWarning($"[EvacuationController] Semua rute aman terblokir. Rute darurat dipilih: {fallbackExit.name}");
            }
            else
            {
                currentStatusMessage = "BAHAYA MAKSIMAL: Tidak ada jalur NavMesh ke exit mana pun.";
                Debug.LogError("[EvacuationController] Tidak ada jalur NavMesh ke exit mana pun.");
            }
        }

        OnStateUpdated?.Invoke();
    }

    public Transform FindBestExit(out float shortestDistance)
    {
        shortestDistance = float.MaxValue;
        Transform bestExit = null;
        if (workerAgent == null) return null;

        Vector3 startPos = workerAgent.transform.position;
        if (NavMesh.SamplePosition(startPos, out NavMeshHit startHit, 5f, NavMesh.AllAreas))
        {
            startPos = startHit.position;
        }

        foreach (Transform exit in exitPoints)
        {
            if (exit == null) continue;

            Vector3 exitPosition = exit.position;
            if (NavMesh.SamplePosition(exitPosition, out NavMeshHit exitHit, 5f, NavMesh.AllAreas))
            {
                exitPosition = exitHit.position;
            }

            NavMeshPath path = new NavMeshPath();
            bool pathCalculated = NavMesh.CalculatePath(startPos, exitPosition, NavMesh.AllAreas, path);

            if (!pathCalculated || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            // Cek apakah jalur ini menembus zona bahaya yang aktif
            bool pathBlockedByDanger = false;
            foreach (var zone in dangerZones)
            {
                if (zone != null && zone.isActive)
                {
                    if (zone.IntersectsPath(path))
                    {
                        pathBlockedByDanger = true;
                        break;
                    }
                }
            }

            if (pathBlockedByDanger)
            {
                continue;
            }

            float dist = CalculatePathLength(path);
            if (dist < shortestDistance)
            {
                shortestDistance = dist;
                bestExit = exit;
            }
        }

        return bestExit;
    }

    private Transform FindBestExitIgnoringDanger(out float shortestDistance)
    {
        shortestDistance = float.MaxValue;
        Transform bestExit = null;
        if (workerAgent == null) return null;

        foreach (Transform exit in exitPoints)
        {
            if (exit == null) continue;
            Vector3 startPosition = workerAgent.transform.position;
            Vector3 exitPosition = exit.position;
            if (!NavMesh.SamplePosition(startPosition, out NavMeshHit startHit, 5f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(exitPosition, out NavMeshHit exitHit, 5f, NavMesh.AllAreas))
                continue;

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(startHit.position, exitHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;

            float distance = CalculatePathLength(path);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                bestExit = exit;
            }
        }

        return bestExit;
    }

    private float CalculatePathLength(NavMeshPath path)
    {
        float length = 0f;
        if (path.corners.Length < 2) return length;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        }
        return length;
    }

    public void ToggleAdaptiveMode()
    {
        SetAdaptiveMode(!adaptiveModeEnabled);
    }

    public void SetAdaptiveMode(bool enabled)
    {
        adaptiveModeEnabled = enabled;
        Debug.Log($"<color=yellow>[EvacuationController]</color> Mode diubah menjadi: {(adaptiveModeEnabled ? "ADAPTIF" : "STATIS (BASELINE)")}");

        if (adaptiveModeEnabled)
        {
            // Re-evaluasi rute adaptif
            if (dangerZones.Count > 0)
            {
                HandleZoneStatusChanged(dangerZones[0], dangerZones[0].isActive);
            }
        }
        else
        {
            // Kembalikan ke static exit awal
            if (staticAssignedExit != null && workerAgent != null)
            {
                workerAgent.SetDestination(staticAssignedExit);
                currentStatusMessage = $"[MODE STATIS] Kembali ke target baseline: {staticAssignedExit.name}";
            }
        }

        OnStateUpdated?.Invoke();
    }

    private void HandleWorkerArrived()
    {
        isSimulationRunning = false;
        currentStatusMessage = $"EVAKUASI SELESAI dalam {totalEvacuationTime:F1}s! Reroute: {rerouteCount}x | Mode: {(adaptiveModeEnabled ? "Adaptif" : "Statis")}";
        Debug.Log($"<color=green>========================================\n[EvacuationController] HASIL SIMULASI EVAKUASI:\n- Total Waktu: {totalEvacuationTime:F2} detik\n- Jumlah Reroute: {rerouteCount}\n- Mode: {(adaptiveModeEnabled ? "ADAPTIF" : "STATIS")}\n- Exit Akhir: {(workerAgent.CurrentExitPoint != null ? workerAgent.CurrentExitPoint.name : "Unknown")}\n========================================</color>");
        OnStateUpdated?.Invoke();
    }

    public void ResetSimulation(Vector3 workerStartPos)
    {
        totalEvacuationTime = 0f;
        rerouteCount = 0;
        lastSystemResponseTimeMs = 0f;
        isSimulationRunning = true;

        foreach (var z in dangerZones)
        {
            if (z != null) z.SetActive(false);
        }

        if (workerAgent != null)
        {
            workerAgent.ResetAgent(workerStartPos);
        }

        InitializeInitialRoute();
        OnStateUpdated?.Invoke();
    }
}
