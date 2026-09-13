using UnityEngine;
using UnityEditor;

public static class CorridorInspector
{
    [MenuItem("SafeMining/Inspect Corridor FBX")]
    public static void InspectFBX()
    {
        string path = "Assets/Models/corridor.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[CorridorInspector] FBX tidak ditemukan: {path}");
            return;
        }

        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        Debug.Log($"[CorridorInspector] FBX={prefab.name}; children={prefab.transform.childCount}; " +
                  $"meshFilters={filters.Length}; renderers={renderers.Length}; " +
                  $"boundsCenter={(hasBounds ? bounds.center.ToString() : "N/A")}; " +
                  $"boundsSize={(hasBounds ? bounds.size.ToString() : "N/A")}");
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            Debug.Log($"[CorridorInspector] Mesh={filter.sharedMesh.name}; localBounds={filter.sharedMesh.bounds}; " +
                      $"vertices={filter.sharedMesh.vertexCount}; object={filter.name}");
        }

        if (filters.Length < 2)
        {
            Debug.LogWarning("[CorridorInspector] corridor.fbx adalah satu mesh monolitik, bukan kit modular. " +
                             "Tidak ada segmen lurus/curve 90 derajat terpisah atau connector untuk disusun otomatis.");
        }
    }
}
