using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SafeMining;

// Editor probe only: switches the Game view to a fixed resolution and captures frames for UI review.
public static class ResProbe
{
    const string Dir = "C:/Users/UNKNOWN/AppData/Local/Temp/claude/D--Dev-Projects-UnityProjects-EvacSafe-Minning/d9b3bb79-714b-4450-9a98-1f95e6f984bb/scratchpad/res/";
    static readonly Assembly Asm = typeof(Editor).Assembly;

    public static string R1280x720() => Set(1280, 720);
    public static string R1366x768() => Set(1366, 768);
    public static string R1280x800() => Set(1280, 800);
    public static string R1440x900() => Set(1440, 900);
    public static string R1920x1200() => Set(1920, 1200);
    public static string R1920x1080() => Set(1920, 1080);
    public static string R1024x768() => Set(1024, 768);

    static string Set(int w, int h)
    {
        var sizesType = Asm.GetType("UnityEditor.GameViewSizes");
        var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
        var groupType = group.GetType();
        int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < total; i++)
        {
            var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            var st = size.GetType();
            if ((int)st.GetProperty("width").GetValue(size) == w && (int)st.GetProperty("height").GetValue(size) == h
                && st.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution") { index = i; break; }
        }
        if (index < 0)
        {
            var sizeTypeEnum = Asm.GetType("UnityEditor.GameViewSizeType");
            var gvs = Asm.GetType("UnityEditor.GameViewSize");
            var ctor = gvs.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
            var newSize = ctor.Invoke(new object[] { Enum.Parse(sizeTypeEnum, "FixedResolution"), w, h, "Probe " + w + "x" + h });
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
            index = total;
        }
        var gvType = Asm.GetType("UnityEditor.GameView");
        var gv = EditorWindow.GetWindow(gvType);
        var cb = gvType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (cb != null) cb.Invoke(gv, new object[] { index, null });
        else gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(gv, index);
        gv.Repaint();
        return "selected " + w + "x" + h + " index " + index + " | Screen now " + Screen.width + "x" + Screen.height;
    }

    static int freezeLevel;

    // Freezes simulated time (not the sim's own pause) as soon as any device reaches the level.
    public static string FreezeAtWarning() { Arm(1); return "armed warning"; }
    public static string FreezeAtClosed() { Arm(2); return "armed closed"; }
    // Start and arm in one call so slow tool round trips cannot miss the moment.
    public static string StoryToWarning() => Story(1);
    public static string StoryToClosed() => Story(2);
    public static string FppToClosed() => Begin(MiningMode.FirstPerson, 2);

    static string Story(int level) => Begin(MiningMode.Story, level);

    static string Begin(MiningMode mode, int level)
    {
        var s = UnityEngine.Object.FindFirstObjectByType<MiningSimulation>();
        Time.timeScale = 1f;
        s.hazardSource = HazardSource.LocalEdgeSimulation; s.scenarioMode = HazardScenarioMode.Scripted;
        s.Begin(mode, true);
        Arm(level);
        return "started " + mode + ", freezing at level " + level;
    }

    public static string Status()
    {
        var s = UnityEngine.Object.FindFirstObjectByType<MiningSimulation>();
        return s.State + " t=" + s.Elapsed.ToString("F1") + " timeScale=" + Time.timeScale + " levels=" + string.Join(",", s.HazardLevels) + " screen " + Screen.width + "x" + Screen.height;
    }

    public static string Unfreeze() { Time.timeScale = 1f; return "timeScale 1"; }

    static void Arm(int level)
    {
        freezeLevel = level;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
    }

    static void Watch()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Watch; return; }
        var s = UnityEngine.Object.FindFirstObjectByType<MiningSimulation>();
        if (s == null) return;
        foreach (var l in s.HazardLevels)
            if (l >= freezeLevel) { Time.timeScale = 0f; EditorApplication.update -= Watch; return; }
    }

    public static string Shot()
    {
        System.IO.Directory.CreateDirectory(Dir);
        var s = UnityEngine.Object.FindFirstObjectByType<MiningSimulation>();
        string state = s == null ? "none" : s.State + "-" + Mathf.RoundToInt(s.Elapsed);
        string path = Dir + Screen.width + "x" + Screen.height + "-" + state + ".png";
        ScreenCapture.CaptureScreenshot(path);
        return path;
    }
}
