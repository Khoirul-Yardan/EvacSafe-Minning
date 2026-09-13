using System.Collections.Generic;
using UnityEngine;

// Geometry fallback for the research demo: one entrance and two independent exit branches.
public static class ProceduralMineLoop
{
    public const float FloorY = -2.6f;
    public const float Width = 5f;
    public const float Height = 5.8f;
    public const float Radius = 2f;

    public static readonly Vector3[] Points =
    {
        new Vector3(0, FloorY, -32), new Vector3(0, FloorY, 0),
        new Vector3(-12, FloorY, 0), new Vector3(-12, FloorY, 12), new Vector3(-28, FloorY, 12), new Vector3(-28, FloorY, 28), new Vector3(-12, FloorY, 28), new Vector3(-12, FloorY, 44), new Vector3(-36, FloorY, 44), new Vector3(-36, FloorY, 60),
        new Vector3(12, FloorY, 0), new Vector3(12, FloorY, -12), new Vector3(28, FloorY, -12), new Vector3(28, FloorY, 10), new Vector3(16, FloorY, 10), new Vector3(16, FloorY, 32), new Vector3(36, FloorY, 32), new Vector3(36, FloorY, 60)
    };

    public static GameObject Create(Transform parent, Material material)
    {
        GameObject root = new GameObject("ProceduralCorridorY_GridAligned");
        root.transform.SetParent(parent, false);
        CreateStraight(root.transform, "Entrance_Trunk", Points[0], Points[1] + Vector3.forward * Radius, material);
        CreatePath(root.transform, "Exit1", new[] { Points[1], Points[2], Points[3], Points[4], Points[5], Points[6], Points[7], Points[8], Points[9] }, material);
        CreatePath(root.transform, "Exit2", new[] { Points[1], Points[10], Points[11], Points[12], Points[13], Points[14], Points[15], Points[16], Points[17] }, material);
        CreateEndPortal(root.transform, "Exit1_Portal", Points[8], Points[9], material);
        CreateEndPortal(root.transform, "Exit2_Portal", Points[16], Points[17], material);
        return root;
    }

    private static void CreateEndPortal(Transform parent, string name, Vector3 previous, Vector3 end, Material material)
    {
        Vector3 direction = (end - previous).normalized;
        Vector3 wallCenter = end + direction * .15f + Vector3.up * (Height * .5f);
        bool alongX = Mathf.Abs(direction.x) > .5f;
        CreateBox(parent, name + "_BackWall", wallCenter, new Vector3(alongX ? .3f : Width, Height, alongX ? Width : .3f), material);
    }

    private static void CreatePath(Transform parent, string name, Vector3[] path, Material material)
    {
        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector3 direction = (path[i + 1] - path[i]).normalized;
            Vector3 start = path[i] + (i == 0 ? Vector3.zero : direction * Radius);
            Vector3 end = path[i + 1] - (i == path.Length - 2 ? Vector3.zero : direction * Radius);
            // The first branch segment meets the trunk: leave its sides open to form a real junction,
            // rather than placing two wall colliders across the incoming corridor.
            CreateStraight(parent, name + "_Straight" + i, start, end, material, i != 0);
            if (i < path.Length - 2)
            {
                Vector3 outDirection = (path[i + 2] - path[i + 1]).normalized;
                CreateTurn(parent, name + "_Curve90_" + i, path[i + 1], end, path[i + 1] + outDirection * Radius, material);
            }
        }
    }

    private static void CreateStraight(Transform parent, string name, Vector3 a, Vector3 b, Material material, bool createSideWalls = true)
    {
        float length = Vector3.Distance(a, b); if (length < .01f) return;
        // Slight overlap prevents hairline gaps between procedural modules.
        Vector3 direction = (b - a).normalized;
        a -= direction * .08f; b += direction * .08f; length += .16f;
        Vector3 mid = (a + b) * .5f; bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        CreateBox(parent, name + "_Floor", mid + Vector3.down * .15f, new Vector3(alongX ? length : Width, .3f, alongX ? Width : length), material);
        CreateBox(parent, name + "_Ceiling", mid + Vector3.up * (Height - .15f), new Vector3(alongX ? length : Width, .3f, alongX ? Width : length), material);
        Vector3 side = alongX ? Vector3.forward * Width * .5f : Vector3.right * Width * .5f;
        Vector3 wallSize = alongX ? new Vector3(length, Height, .25f) : new Vector3(.25f, Height, length);
        if (createSideWalls)
        {
            CreateBox(parent, name + "_WallL", mid + Vector3.up * (Height * .5f) + side, wallSize, material);
            CreateBox(parent, name + "_WallR", mid + Vector3.up * (Height * .5f) - side, wallSize, material);
        }
    }

    private static void CreateTurn(Transform parent, string name, Vector3 corner, Vector3 start, Vector3 end, Material material)
    {
        // Small tangent boxes form one consistently-sized rounded 90-degree tunnel turn.
        const int steps = 8;
        Vector3 v0 = start - corner, v1 = end - corner;
        float cross = v0.x * v1.z - v0.z * v1.x;
        float a0 = Mathf.Atan2(v0.z, v0.x), delta = cross > 0 ? Mathf.PI * .5f : -Mathf.PI * .5f;
        for (int i = 0; i < steps; i++)
        {
            float t = (i + .5f) / steps, angle = a0 + delta * t;
            Vector3 pos = corner + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Radius;
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * Mathf.Sign(delta);
            Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);
            float segmentLength = Radius * Mathf.PI * .5f / steps + .08f;
            CreateRotatedBox(parent, name + "_Floor" + i, pos + Vector3.down * .15f, new Vector3(Width, .3f, segmentLength), rotation, material);
            CreateRotatedBox(parent, name + "_Ceiling" + i, pos + Vector3.up * (Height - .15f), new Vector3(Width, .3f, segmentLength), rotation, material);
            CreateRotatedBox(parent, name + "_WallL" + i, pos + rotation * (Vector3.right * Width * .5f + Vector3.up * (Height * .5f)), new Vector3(.25f, Height, segmentLength), rotation, material);
            CreateRotatedBox(parent, name + "_WallR" + i, pos + rotation * (-Vector3.right * Width * .5f + Vector3.up * (Height * .5f)), new Vector3(.25f, Height, segmentLength), rotation, material);
        }
    }

    private static void CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name; box.transform.SetParent(parent); box.transform.position = position; box.transform.localScale = scale;
        box.isStatic = true; Renderer r = box.GetComponent<Renderer>(); if (r != null && material != null) r.sharedMaterial = material;
    }

    private static void CreateRotatedBox(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name; box.transform.SetParent(parent); box.transform.position = position; box.transform.rotation = rotation; box.transform.localScale = scale;
        box.isStatic = true; Renderer r = box.GetComponent<Renderer>(); if (r != null && material != null) r.sharedMaterial = material;
    }
}
