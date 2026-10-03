using System.IO;
using UnityEditor;
using UnityEngine;
using SafeMining;

// Play-mode probe only: staged illustration for the paper's proximity figure. Runs an adaptive Story session on the
// comparison timeline (scripted scenario) and, once the worker walks the new route, closes a detector 3-5 m beside it
// by retiming an unsent schedule item. Captures the closure and the result. Nothing is saved to the scene.
public static class CustomProximity
{
    const string Dir = "C:/Users/UNKNOWN/AppData/Local/Temp/claude/D--Dev-Projects-UnityProjects-EvacSafe-Minning/d9b3bb79-714b-4450-9a98-1f95e6f984bb/scratchpad/paper/";
    const string Log = Dir + "custom-proximity.txt";
    static int stage, target, startReroutes;
    static float closedAt;

    public static string Execute()
    {
        var s = Object.FindFirstObjectByType<MiningSimulation>();
        Time.timeScale = 1;
        s.randomSeedOnLaunch = false;
        s.hazardSource = HazardSource.LegacyTimeline;
        s.scenarioMode = HazardScenarioMode.Scripted;
        s.Begin(MiningMode.Story, true);
        stage = 0; target = -1; startReroutes = s.Reroutes;
        // Hold back every event after the first; one of them is retimed next to the worker later.
        for (int i = 1; i < s.Schedule.Count; i++) { s.Schedule[i].warningTime = 9000; s.Schedule[i].collapseTime = 9001; }
        File.WriteAllText(Log, "schedule: " + s.Schedule.Count + " items\n");
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return "started custom proximity, source=" + s.ActiveHazardSource;
    }

    static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

    static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        var s = Object.FindFirstObjectByType<MiningSimulation>();
        if (stage == 0)
        {
            // Wait until the first closure was handled and the worker walks the new route.
            if (s.Reroutes <= startReroutes || s.StoryHolding || s.Elapsed < 12) return;
            var cell = MineLayout.Cell(s.Actor.position);
            int item = -1;
            for (int i = 0; i < s.Schedule.Count; i++) if (s.Schedule[i].warningTime > s.Elapsed + .1f) { item = i; break; }
            if (item < 0) { File.AppendAllText(Log, "no unsent schedule item left at t=" + s.Elapsed + "\n"); stage = 9; return; }
            for (int d = 0; d < s.HazardSites.Count; d++)
            {
                var site = s.HazardSites[d];
                float dist = Flat(s.Actor.position, MineLayout.World(site));
                if (s.HazardLevels[d] != 0 || site == cell || s.Route.Contains(site) || dist < 4f || dist > 5.2f) continue;
                target = d;
                s.Schedule[item].detectorIndex = d;
                s.Schedule[item].warningTime = s.Elapsed + .02f;
                s.Schedule[item].collapseTime = s.Elapsed + .04f;
                File.AppendAllText(Log, "t=" + s.Elapsed.ToString("F2") + " close D" + (d + 1).ToString("00") + " grid " + site + " at " + dist.ToString("F2") + " m from worker cell " + cell + "\n");
                stage = 1;
                return;
            }
        }
        else if (stage == 1 && s.HazardLevels[target] == 2)
        {
            closedAt = s.Elapsed; stage = 2;
        }
        else if (stage >= 2 && stage <= 4)
        {
            float[] at = { .05f, 1.2f, 3f };
            if (s.Elapsed < closedAt + at[stage - 2]) return;
            ScreenCapture.CaptureScreenshot(Dir + "p1-closed-near-" + (stage - 1) + ".png");
            File.AppendAllText(Log, "p1-" + (stage - 1) + " t=" + s.Elapsed.ToString("F2") + " exposure=" + s.Exposure.ToString("F3") + " contacts=" + s.HazardContacts + "\n");
            stage++;
            if (stage == 5) stage = 13; // wait for the result
        }
        else if (stage == 13 && (s.State == SafeMining.SessionState.Success || s.State == SafeMining.SessionState.Blocked))
        {
            ScreenCapture.CaptureScreenshot(Dir + "p2-result.png");
            File.AppendAllText(Log, "p2 t=" + s.Elapsed.ToString("F2") + " state=" + s.State + " exposure=" + s.Exposure.ToString("F3") + " contacts=" + s.HazardContacts + "\n");
            stage = 9;
        }
        if (stage == 9) EditorApplication.update -= Tick;
    }
}
