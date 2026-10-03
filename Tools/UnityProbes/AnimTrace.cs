using System.IO;
using UnityEditor;
using UnityEngine;
using SafeMining;

// Play-mode probe only: runs a Story session and logs every change of the rigged worker's Animator state together
// with the simulation stage; captures the first frame of each standing pose. Nothing is saved to the scene.
public static class AnimTrace
{
    const string Dir = "C:/Users/UNKNOWN/AppData/Local/Temp/claude/D--Dev-Projects-UnityProjects-EvacSafe-Minning/d9b3bb79-714b-4450-9a98-1f95e6f984bb/scratchpad/anim/";
    static string last = "", log;
    static readonly System.Collections.Generic.HashSet<string> shot = new System.Collections.Generic.HashSet<string>();
    static readonly string[] States = { "Idle", "Locomotion", "Run to stop", "Radio", "LookAround", "Injured" };

    public static string Adaptive() { return Run(true, "adaptive"); }
    public static string Static() { return Run(false, "static"); }

    // Traces a session someone else started (e.g. the staged contact probe).
    public static string WatchOnly()
    {
        Directory.CreateDirectory(Dir);
        log = Dir + "trace-contact.txt"; File.WriteAllText(log, "t | state | stage | sim | speed\n");
        last = ""; shot.Clear();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return "watching";
    }

    static string Run(bool adaptive, string tag)
    {
        Directory.CreateDirectory(Dir);
        log = Dir + "trace-" + tag + ".txt"; File.WriteAllText(log, "t | state | stage | sim | speed\n");
        last = ""; shot.Clear();
        var s = Object.FindFirstObjectByType<MiningSimulation>();
        Time.timeScale = 1; s.randomSeedOnLaunch = false;
        s.hazardSource = HazardSource.LocalEdgeSimulation; s.scenarioMode = HazardScenarioMode.Scripted;
        s.Begin(MiningMode.Story, adaptive);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return "started " + tag;
    }

    static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        var s = Object.FindFirstObjectByType<MiningSimulation>();
        var worker = GameObject.Find("Rigged worker");
        if (worker == null) return;
        var a = worker.GetComponent<Animator>();
        var info = a.GetCurrentAnimatorStateInfo(0);
        string state = "?";
        foreach (var n in States) if (info.IsName(n)) state = n;
        if (state != last)
        {
            File.AppendAllText(log, s.Elapsed.ToString("F2") + " | " + state + " | " + s.StoryStep + " | " + s.State + " | " + a.GetFloat("Speed").ToString("F2") + "\n");
            last = state;
        }
        string key = state + (state == "Locomotion" ? (s.Elapsed > 5 ? "-mid" : "-start") : "");
        if (!shot.Contains(key) && info.normalizedTime > .35f && (state == "LookAround" || state == "Injured" || key == "Locomotion-mid" || state == "Radio"))
        {
            shot.Add(key);
            ScreenCapture.CaptureScreenshot(Dir + Path.GetFileNameWithoutExtension(log) + "-" + key + ".png");
        }
        if (s.State == SafeMining.SessionState.Success || s.State == SafeMining.SessionState.Blocked)
        {
            if (s.Elapsed > 0 && !shot.Contains("end-" + s.State) && Time.realtimeSinceStartup > 0)
            {
                shot.Add("end-" + s.State);
                File.AppendAllText(log, "end " + s.State + " at " + s.Elapsed.ToString("F2") + "\n");
            }
        }
    }
}
