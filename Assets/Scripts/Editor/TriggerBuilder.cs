using UnityEditor;

[InitializeOnLoad]
public static class TriggerBuilder
{
    static TriggerBuilder()
    {
        EditorApplication.delayCall += ForceRunBuild;
    }

    private static void ForceRunBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            UnityEngine.Debug.Log("[TriggerBuilder] Build dilewati karena Unity sedang Play Mode.");
            return;
        }

        if (System.IO.File.Exists("Assets/Scenes/SafeMiningEvac_Demo.unity"))
        {
            UnityEngine.Debug.Log("[TriggerBuilder] Scene existing dipertahankan; gunakan menu SafeMining untuk rebuild manual.");
            return;
        }

        UnityEngine.Debug.Log("[TriggerBuilder] Executing BuildCompleteScene via delayCall...");
        SafeMiningSceneBuilder.BuildCompleteScene();
    }
}
