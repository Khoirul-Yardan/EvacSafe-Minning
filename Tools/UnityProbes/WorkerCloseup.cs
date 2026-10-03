using System.IO;
using UnityEngine;

// Play-mode probe only: renders the rigged worker from a temporary side camera into a PNG. Nothing is saved to the scene.
public static class WorkerCloseup
{
    const string Dir = "C:/Users/UNKNOWN/AppData/Local/Temp/claude/D--Dev-Projects-UnityProjects-EvacSafe-Minning/d9b3bb79-714b-4450-9a98-1f95e6f984bb/scratchpad/res/";

    public static string Execute() => Shoot("closeup", false);
    public static string NoHeadlamp() => Shoot("closeup-nolamp", true);
    public static string MainView() => Shoot("mainview", false, true);
    public static string MainViewNoLamp() => Shoot("mainview-nolamp", true, true);

    static string Shoot(string name, bool dimHeadlamp, bool fromViewCamera = false)
    {
        var sim = Object.FindFirstObjectByType<SafeMining.MiningSimulation>();
        var worker = GameObject.Find("Rigged worker").transform;
        var headlamp = sim.ViewCamera.GetComponent<Light>();
        float intensity = headlamp.intensity;
        if (dimHeadlamp) headlamp.intensity = 0;
        var go = new GameObject("Probe camera"); var cam = go.AddComponent<Camera>();
        cam.CopyFrom(sim.ViewCamera); cam.fieldOfView = 40;
        go.transform.position = worker.position + worker.forward * 2.6f + worker.right * 1.2f + Vector3.up * 1.5f;
        go.transform.LookAt(worker.position + Vector3.up * 1.1f);
        if (fromViewCamera) { go.transform.SetPositionAndRotation(sim.ViewCamera.transform.position, sim.ViewCamera.transform.rotation); cam.fieldOfView = sim.ViewCamera.fieldOfView; }
        var rt = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); tex.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(Dir + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(go); rt.Release(); headlamp.intensity = intensity;
        return Dir + name + ".png";
    }
}
