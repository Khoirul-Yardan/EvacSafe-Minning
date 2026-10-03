using UnityEngine;
using UnityEngine.UI;
using SafeMining;

// Play-mode probe only: drives sessions for UI screenshots. Nothing here is saved to the scene.
public static class UiProbe
{
    const string Dir = "C:/Users/UNKNOWN/AppData/Local/Temp/claude/D--Dev-Projects-UnityProjects-EvacSafe-Minning/d9b3bb79-714b-4450-9a98-1f95e6f984bb/scratchpad/";
    static MiningSimulation Sim => Object.FindFirstObjectByType<MiningSimulation>();

    public static string StartStory()
    {
        var s = Sim; if (s == null) return "no simulation";
        s.hazardSource = HazardSource.LocalEdgeSimulation; s.scenarioMode = HazardScenarioMode.Scripted;
        s.Begin(MiningMode.Story, true);
        return Status();
    }

    public static string Pause() { Sim.TogglePause(); return Status(); }

    public static string StartFpp()
    {
        var s = Sim; if (s == null) return "no simulation";
        s.hazardSource = HazardSource.LocalEdgeSimulation; s.scenarioMode = HazardScenarioMode.Scripted;
        s.Begin(MiningMode.FirstPerson, true);
        return Status();
    }

    public static string ToggleHudDetail() => Click("System detail toggle");

    public static string Shot()
    {
        var s = Sim;
        string path = Dir + "ui-" + s.State + "-" + Mathf.RoundToInt(s.Elapsed) + "-" + (s.Adaptive ? "a" : "s") + ".png";
        ScreenCapture.CaptureScreenshot(path);
        return path + " | " + Status() + " | screen " + Screen.width + "x" + Screen.height;
    }

    public static string Compare() => Click("Secondary action");
    public static string SelectFpp() => Click("FPP mode");
    public static string SelectLocal() => Click("Edge lokal option");
    public static string StartFromMenu() => Click("Start simulation");
    public static string ToggleDetail() => Click("Lihat detail sistem link");

    static string Click(string name)
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (b.name == name) { b.onClick.Invoke(); return "clicked " + name + " | " + Status(); }
        return "button not found: " + name;
    }

    public static string Status()
    {
        var s = Sim;
        return s == null ? "none" : s.State + " t=" + s.Elapsed.ToString("F1") + (s.Adaptive ? " adaptive" : " static") + " levels=" + string.Join(",", s.HazardLevels);
    }
}
