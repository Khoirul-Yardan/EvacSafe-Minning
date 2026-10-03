using System.IO;
using UnityEditor;
using UnityEngine;
using SafeMining;

// Play-mode probe only: runs adaptive Story sessions over a seed range (local edge, sped up) and logs which
// seeds reach the safe zone with a hazard-proximity contact. Results go to a file; nothing is saved to the scene.
public static class SeedSearch
{
    const string Out = "C:/Users/UNKNOWN/AppData/Local/Temp/claude/D--Dev-Projects-UnityProjects-EvacSafe-Minning/d9b3bb79-714b-4450-9a98-1f95e6f984bb/scratchpad/paper/seed-search.txt";
    static int seed, last;

    public static string Execute()
    {
        seed = 101; last = 160;
        File.WriteAllText(Out, "seed | state | t | exposure | contacts | reroutes\n");
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Start();
        return "searching " + seed + ".." + last;
    }

    static void Start()
    {
        var s = Object.FindFirstObjectByType<MiningSimulation>();
        s.randomSeedOnLaunch = false;
        s.hazardSource = HazardSource.LocalEdgeSimulation;
        s.scenarioMode = HazardScenarioMode.Random;
        s.scenarioSeed = seed;
        s.Begin(MiningMode.Story, true);
        Time.timeScale = 8;
    }

    static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        var s = Object.FindFirstObjectByType<MiningSimulation>();
        bool done = s.State == SafeMining.SessionState.Success || s.State == SafeMining.SessionState.Blocked || s.Elapsed > 170;
        if (!done) return;
        File.AppendAllText(Out, seed + " | " + s.State + " | " + s.Elapsed.ToString("F1") + " | " + s.Exposure.ToString("F3") + " | " + s.HazardContacts + " | " + s.Reroutes + "\n");
        if (++seed > last) { Time.timeScale = 1; EditorApplication.update -= Tick; File.AppendAllText(Out, "done\n"); return; }
        Start();
    }
}
