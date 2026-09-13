using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.UI;

public class ScenarioRunner : MonoBehaviour
{
    [Header("Manager References")]
    public EvacuationController controller;
    public DangerZone dangerZonePrimary;     // Zona yang menutup Exit 1 (terdekat)
    public DangerZone dangerZoneAlternative; // Zona yang menutup rute alternatif (Exit 2)
    public DangerZone dangerZoneTertiary;    // Zona ketiga jika ada

    [Header("Timeline Settings (Seconds)")]
    public float timeActivateFirstHazard = 5.0f;
    public float timeActivateSecondHazard = 12.0f;
    public float timeClearAllHazards = 20.0f;

    [Header("Status")]
    public float scenarioTimer = 0f;
    public bool isRunning = false;
    public string scenarioPhase = "Persiapan";

    private Coroutine scenarioRoutine;
    private Text finishNotice;

    private void Start()
    {
        if (controller == null)
        {
            controller = FindFirstObjectByType<EvacuationController>();
        }

        AutoAssignZones();
        if (controller != null && controller.workerAgent != null)
            controller.workerAgent.OnArrivedAtExit += HandleArrival;
        StartScenario();
    }

    private void Update()
    {
        if (isRunning)
        {
            scenarioTimer += Time.deltaTime;
        }

        // Shortcut keyboard 'R' untuk restart simulasi otomatis
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartScenario();
        }
    }

    public void AutoAssignZones()
    {
        if (controller != null && controller.dangerZones != null && controller.dangerZones.Count >= 2)
        {
            if (dangerZonePrimary == null) dangerZonePrimary = controller.dangerZones[0];
            if (dangerZoneAlternative == null && controller.dangerZones.Count > 1) dangerZoneAlternative = controller.dangerZones[1];
            if (dangerZoneTertiary == null && controller.dangerZones.Count > 2) dangerZoneTertiary = controller.dangerZones[2];
        }
    }

    public void StartScenario()
    {
        if (scenarioRoutine != null) StopCoroutine(scenarioRoutine);
        scenarioRoutine = StartCoroutine(RunScenarioRoutine());
    }

    public void RestartScenario()
    {
        Debug.Log("<color=cyan>[ScenarioRunner]</color> Merestart skenario evakuasi...");
        if (scenarioRoutine != null) StopCoroutine(scenarioRoutine);

        if (controller != null && controller.workerAgent != null)
        {
            // Ambil posisi awal dari controller atau reset
            Transform entrance = GameObject.Find("EntrancePoint")?.transform;
            controller.ResetSimulation(entrance != null ? entrance.position : controller.workerAgent.transform.position);
        }

        scenarioRoutine = StartCoroutine(RunScenarioRoutine());
    }

    private void HandleArrival()
    {
        if (scenarioRoutine != null) StopCoroutine(scenarioRoutine);
        StartCoroutine(ShowFinishAndRestart());
    }

    private IEnumerator ShowFinishAndRestart()
    {
        isRunning = false;
        scenarioPhase = "EVAKUASI BERHASIL — simulasi akan diulang dari EntrancePoint.";
        EnsureFinishNotice();
        if (finishNotice != null) finishNotice.gameObject.SetActive(true);
        Debug.Log("[ScenarioRunner] EVAKUASI SELESAI. Restart otomatis dalam 4 detik.");
        yield return new WaitForSeconds(4f);
        if (finishNotice != null) finishNotice.gameObject.SetActive(false);
        RestartScenario();
    }

    private void EnsureFinishNotice()
    {
        if (finishNotice != null) return;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        GameObject notice = new GameObject("SimulationFinishedNotice");
        notice.transform.SetParent(canvas.transform, false);
        RectTransform rect = notice.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f); rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(620, 100);
        finishNotice = notice.AddComponent<Text>();
        finishNotice.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        finishNotice.fontSize = 24; finishNotice.alignment = TextAnchor.MiddleCenter; finishNotice.color = new Color(.2f, 1f, .55f);
        finishNotice.text = "EVAKUASI BERHASIL\nSimulasi akan diulang...";
    }

    private IEnumerator RunScenarioRoutine()
    {
        isRunning = true;
        scenarioTimer = 0f;

        // T = 0: Semua DangerZone TIDAK AKTIF
        scenarioPhase = "T=0s: Kondisi normal. Evakuasi dimulai ke exit terdekat.";
        Debug.Log($"<color=white>[ScenarioRunner]</color> {scenarioPhase}");

        if (dangerZonePrimary != null) dangerZonePrimary.SetActive(false);
        if (dangerZoneAlternative != null) dangerZoneAlternative.SetActive(false);
        if (dangerZoneTertiary != null) dangerZoneTertiary.SetActive(false);

        // Tunggu hingga T = 5s
        yield return new WaitForSeconds(timeActivateFirstHazard);

        // T = 5: Aktifkan DangerZone 1
        scenarioPhase = "T=5s: BAHAYA 1 MUNCUL! Jalur utama tertutup. Memaksa reroute adaptif.";
        Debug.Log($"<color=red><b>[ScenarioRunner]</b></color> {scenarioPhase}");
        if (dangerZonePrimary != null)
        {
            dangerZonePrimary.SetActive(true);
        }

        // Tunggu hingga T = 12s
        float waitRemaining = Mathf.Max(0.1f, timeActivateSecondHazard - timeActivateFirstHazard);
        yield return new WaitForSeconds(waitRemaining);

        // T = 12: pindahkan longsor ke cabang lain. Jangan pernah menutup dua exit sekaligus;
        // simulasi adaptif harus selalu memiliki minimal satu rute valid untuk dibandingkan.
        scenarioPhase = "T=12s: BAHAYA pindah ke jalur 2; jalur 1 dibuka kembali.";
        Debug.Log($"<color=red><b>[ScenarioRunner]</b></color> {scenarioPhase}");
        if (dangerZonePrimary != null) dangerZonePrimary.SetActive(false);
        if (dangerZoneAlternative != null)
        {
            dangerZoneAlternative.SetActive(true);
        }

        // Tunggu hingga T = 20s
        float waitClear = Mathf.Max(0.1f, timeClearAllHazards - timeActivateSecondHazard);
        yield return new WaitForSeconds(waitClear);

        // T = 20: Nonaktifkan semua zona
        scenarioPhase = "T=20s: Bahaya mereda. Semua zona dinonaktifkan.";
        Debug.Log($"<color=green>[ScenarioRunner]</color> {scenarioPhase}");
        if (dangerZonePrimary != null) dangerZonePrimary.SetActive(false);
        if (dangerZoneAlternative != null) dangerZoneAlternative.SetActive(false);
        if (dangerZoneTertiary != null) dangerZoneTertiary.SetActive(false);

        isRunning = false;
    }
}
