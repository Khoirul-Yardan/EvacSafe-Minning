using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SafeMining;

// Play-mode probe only: runs Story sessions with the MQTT edge and the scripted scenario, and captures
// Game view frames at the moments the paper's figures need. Nothing is saved to the scene.
public static class PaperShots
{
    const string Dir = "C:/Users/UNKNOWN/AppData/Local/Temp/claude/D--Dev-Projects-UnityProjects-EvacSafe-Minning/d9b3bb79-714b-4450-9a98-1f95e6f984bb/scratchpad/paper/";
    static readonly List<string> pending = new List<string>();
    static readonly List<string> log = new List<string>();
    static int startReroutes;

    public static string Adaptive()
    {
        return Run(true, new[] { "a1-initial-route", "a2-warning", "a3-closed", "a4-rerouted", "a5-proximity", "a6-result" });
    }

    public static string Static()
    {
        return Run(false, new[] { "s1-initial-route", "s2-closed", "s3-blocked" });
    }

    static bool Any(int[] levels, int level)
    {
        foreach (int l in levels) if (l == level) return true;
        return false;
    }

    static bool Ready(string name, MiningSimulation s)
    {
        if (name.EndsWith("initial-route")) return s.Elapsed >= 3f;
        if (name.EndsWith("warning")) return Any(s.HazardLevels, 1);
        if (name.EndsWith("closed")) return Any(s.HazardLevels, 2);
        if (name.EndsWith("rerouted")) return s.Reroutes > startReroutes && !s.StoryHolding && !s.NavigationWaiting && Any(s.HazardLevels, 2);
        if (name.EndsWith("proximity")) return s.Exposure > 0 || s.HazardContacts > 0;
        if (name.EndsWith("result")) return s.State == SafeMining.SessionState.Success;
        if (name.EndsWith("blocked")) return s.State == SafeMining.SessionState.Blocked;
        return false;
    }

    static string Run(bool adaptive, string[] shots)
    {
        Directory.CreateDirectory(Dir);
        var s = Object.FindFirstObjectByType<MiningSimulation>();
        Time.timeScale = 1;
        s.randomSeedOnLaunch = false;
        s.hazardSource = HazardSource.MqttEdgeSimulation;
        s.scenarioMode = HazardScenarioMode.Scripted;
        s.Begin(MiningMode.Story, adaptive);
        startReroutes = s.Reroutes;
        pending.Clear(); pending.AddRange(shots); log.Clear();
        EditorApplication.update -= Watch; EditorApplication.update += Watch;
        return "started " + (adaptive ? "adaptive" : "static") + " source=" + s.ActiveHazardSource + " scenario=" + s.ActiveScenario + " seed=" + s.ActiveSeed + " screen " + Screen.width + "x" + Screen.height;
    }

    static void Watch()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Watch; return; }
        var s = Object.FindFirstObjectByType<MiningSimulation>();
        if (s == null || pending.Count == 0) { EditorApplication.update -= Watch; return; }
        for (int i = 0; i < pending.Count; i++)
        {
            string name = pending[i];
            if (!Ready(name, s)) continue;
            ScreenCapture.CaptureScreenshot(Dir + name + ".png");
            log.Add(name + " | t=" + s.Elapsed.ToString("F2") + " state=" + s.State + " levels=" + string.Join(",", s.HazardLevels)
                + " reroutes=" + s.Reroutes + " exposure=" + s.Exposure.ToString("F3") + " contacts=" + s.HazardContacts
                + " source=" + s.ActiveHazardSource + " session=" + (s.EdgeSession != null ? s.EdgeSession.SessionId.Substring(0, 8) : "-"));
            pending.RemoveAt(i); i--;
        }
    }

    public static string Status()
    {
        var s = Object.FindFirstObjectByType<MiningSimulation>();
        return s.State + " t=" + s.Elapsed.ToString("F1") + " holding=" + s.StoryHolding + " levels=" + string.Join(",", s.HazardLevels)
            + " pending=" + string.Join(",", pending.ToArray()) + "\n" + string.Join("\n", log.ToArray());
    }
}
