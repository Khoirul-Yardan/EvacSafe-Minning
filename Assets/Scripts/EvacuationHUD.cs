using UnityEngine;
using UnityEngine.UI;
using System.Text;

public class EvacuationHUD : MonoBehaviour
{
    [Header("Controller Reference")]
    public EvacuationController controller;
    public ScenarioRunner scenarioRunner;

    [Header("UI Text Displays")]
    public Text titleText;
    public Text modeText;
    public Text workerTargetText;
    public Text rerouteCountText;
    public Text timerText;
    public Text responseTimeText;
    public Text dangerZonesStatusText;
    public Text scenarioPhaseText;

    [Header("UI Buttons")]
    public Button toggleModeButton;
    public Text toggleModeButtonText;
    public Button restartScenarioButton;

    private StringBuilder sb = new StringBuilder();

    private void Start()
    {
        if (controller == null)
            controller = FindFirstObjectByType<EvacuationController>();

        if (scenarioRunner == null)
            scenarioRunner = FindFirstObjectByType<ScenarioRunner>();

        if (toggleModeButton != null)
        {
            toggleModeButton.onClick.AddListener(OnToggleModeClicked);
        }

        if (restartScenarioButton != null)
        {
            restartScenarioButton.onClick.AddListener(OnRestartClicked);
        }

        UpdateHUD();
    }

    private void Update()
    {
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (controller == null) return;

        // Mode Status
        if (modeText != null)
        {
            if (controller.adaptiveModeEnabled)
            {
                modeText.text = "<color=#00FF88>? MODE: ADAPTIF (Edge Intelligence)</color>";
            }
            else
            {
                modeText.text = "<color=#FFAA00>? MODE: STATIS (Baseline / Jalur Tetap)</color>";
            }
        }

        if (toggleModeButtonText != null)
        {
            toggleModeButtonText.text = controller.adaptiveModeEnabled ? "Ubah ke Mode STATIS" : "Ubah ke Mode ADAPTIF";
        }

        // Worker Target & Distance
        if (workerTargetText != null)
        {
            string targetName = controller.workerAgent != null && controller.workerAgent.CurrentExitPoint != null
                ? controller.workerAgent.CurrentExitPoint.name
                : "Belum Ditetapkan";

            float dist = controller.workerAgent != null ? controller.workerAgent.remainingDistance : 0f;
            string statusStr = controller.workerAgent != null && controller.workerAgent.hasArrived
                ? "<color=#00FF88> [SAMPAI DI TUJUAN]</color>"
                : $" ({dist:F1}m tersisa)";

            workerTargetText.text = $"Target Exit: <b>{targetName}</b>{statusStr}";
        }

        // Reroute Count
        if (rerouteCountText != null)
        {
            rerouteCountText.text = $"Jumlah Reroute: <b>{controller.rerouteCount}</b> kali";
        }

        // Timer
        if (timerText != null)
        {
            timerText.text = $"Waktu Evakuasi: <b>{controller.totalEvacuationTime:F1} s</b>";
        }

        // Response Time
        if (responseTimeText != null)
        {
            responseTimeText.text = $"Waktu Respons Sistem: <b>{controller.lastSystemResponseTimeMs:F3} ms</b>";
        }

        // Scenario Phase
        if (scenarioPhaseText != null && scenarioRunner != null)
        {
            scenarioPhaseText.text = $"Fase Skenario: {scenarioRunner.scenarioPhase} (Timer: {scenarioRunner.scenarioTimer:F1}s)";
        }

        // Danger Zones List
        if (dangerZonesStatusText != null)
        {
            sb.Clear();
            sb.AppendLine("<b>Status Zona Bahaya:</b>");
            if (controller.dangerZones != null)
            {
                for (int i = 0; i < controller.dangerZones.Count; i++)
                {
                    var z = controller.dangerZones[i];
                    if (z == null) continue;
                    string status = z.isActive
                        ? "<color=#FF4444>[AKTIF / BAHAYA]</color>"
                        : "<color=#44FF44>[TIDAK AKTIF / AMAN]</color>";
                    sb.AppendLine($" • {z.zoneName}: {status}");
                }
            }
            dangerZonesStatusText.text = sb.ToString();
        }
    }

    private void OnToggleModeClicked()
    {
        if (controller != null)
        {
            controller.ToggleAdaptiveMode();
            UpdateHUD();
        }
    }

    private void OnRestartClicked()
    {
        if (scenarioRunner != null)
        {
            scenarioRunner.RestartScenario();
        }
    }
}
