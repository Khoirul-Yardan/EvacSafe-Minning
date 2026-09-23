using System.IO;
using UnityEditor;
using UnityEngine;
using SafeMining;

[InitializeOnLoad]
public static class MiningDetectorAssetBuilder
{
    public const string PrefabPath = "Assets/Resources/Mining/LandslideDetector.prefab";
    static MiningDetectorAssetBuilder() { EditorApplication.delayCall += Ensure; }
    [MenuItem("SafeMining/Create Landslide Detector Asset")]
    public static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        Directory.CreateDirectory("Assets/Resources/Mining"); AssetDatabase.Refresh();
        var model = MiningLandslideDetector.CreateModel();
        try
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (renderer.GetComponent<TextMesh>() != null) continue;
                var material = renderer.sharedMaterial;
                if (EditorUtility.IsPersistent(material)) continue;
                string path = "Assets/Resources/Mining/" + material.name.Replace(" ", "_") + ".mat";
                var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (saved == null) { AssetDatabase.CreateAsset(material, path); saved = material; }
                renderer.sharedMaterial = saved;
            }
            // Use a font atlas owned by Unity; it is recreated after import.
            var text = model.GetComponentInChildren<TextMesh>();
            text.GetComponent<Renderer>().sharedMaterial = text.font.material;
            PrefabUtility.SaveAsPrefabAsset(model, PrefabPath); AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(model); }
    }
}
