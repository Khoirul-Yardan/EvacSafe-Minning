using System;
using System.Collections.Generic;
using UnityEngine;

namespace SafeMining
{
    [Serializable]
    public class MineCorridor
    {
        public Vector2Int from;
        public Vector2Int to;
        public MineCorridor(Vector2Int from, Vector2Int to) { this.from = from; this.to = to; }
    }

    // One occupancy map owns geometry, collision, navigation and the minimap.
    // Internal tile edges never receive a wall, including T and four-way junctions.
    public static class MineLayout
    {
        public const float CellSize = 6f;
        public const float Ceiling = 4.6f;
        public static readonly Vector2Int Spawn = new Vector2Int(0, -4);
        public static readonly Vector2Int[] Exits = { new Vector2Int(0, 10), new Vector2Int(-4, 8), new Vector2Int(4, 8) };
        public static readonly Vector2Int[] HazardCells = { new Vector2Int(0, 4), new Vector2Int(-4, 5), new Vector2Int(4, 1) };
        public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.left, Vector2Int.right, Vector2Int.down };

        public static List<MineCorridor> DefaultCorridors() => new List<MineCorridor>
        {
            new MineCorridor(new Vector2Int(0, -5), new Vector2Int(0, 10)),
            new MineCorridor(new Vector2Int(-4, 0), new Vector2Int(4, 0)),
            new MineCorridor(new Vector2Int(-4, 3), new Vector2Int(4, 3)),
            new MineCorridor(new Vector2Int(-4, 6), new Vector2Int(4, 6)),
            new MineCorridor(new Vector2Int(-4, 0), new Vector2Int(-4, 8)),
            new MineCorridor(new Vector2Int(4, 0), new Vector2Int(4, 8))
        };

        public static HashSet<Vector2Int> CreateCells(List<MineCorridor> corridors = null)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (var corridor in corridors ?? DefaultCorridors())
            {
                if (corridor == null) throw new ArgumentException("Koridor tidak boleh kosong.");
                var a = corridor.from; var b = corridor.to;
                if (a.x != b.x && a.y != b.y)
                    throw new ArgumentException("Koridor harus horizontal atau vertikal: " + a + " -> " + b);
                if (Math.Abs((long)a.x) > 30 || Math.Abs((long)a.y) > 30 || Math.Abs((long)b.x) > 30 || Math.Abs((long)b.y) > 30)
                    throw new ArgumentException("Koordinat koridor harus berada di antara -30 dan 30 sel.");
                AddLine(cells, a, b);
            }
            // Widen the refuge rooms without opening the exterior shell.
            foreach (var exit in Exits)
                for (int x = -1; x <= 1; x++)
                    for (int z = 0; z <= 1; z++) cells.Add(exit + new Vector2Int(x, z));
            if (cells.Count > 300) throw new ArgumentException("Maksimum 300 sel untuk skenario ini.");
            if (!cells.Contains(Spawn)) throw new ArgumentException("Koridor harus memuat titik awal " + Spawn);
            foreach (var hazard in HazardCells)
                if (!cells.Contains(hazard)) throw new ArgumentException("Koridor harus memuat lokasi longsor " + hazard);
            var reachable = new HashSet<Vector2Int> { Spawn };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(Spawn);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in Directions)
                    if (cells.Contains(cell + direction) && reachable.Add(cell + direction)) queue.Enqueue(cell + direction);
            }
            if (reachable.Count != cells.Count) throw new ArgumentException("Semua koridor dan zona aman harus terhubung ke titik awal.");
            return cells;
        }

        static void AddLine(HashSet<Vector2Int> cells, Vector2Int a, Vector2Int b)
        {
            var d = new Vector2Int(Math.Sign(b.x - a.x), Math.Sign(b.y - a.y));
            for (var p = a; ; p += d) { cells.Add(p); if (p == b) break; }
        }

        public static Vector3 World(Vector2Int cell) => new Vector3(cell.x * CellSize, 0, cell.y * CellSize);
        public static Vector2Int Cell(Vector3 position) => new Vector2Int(Mathf.RoundToInt(position.x / CellSize), Mathf.RoundToInt(position.z / CellSize));

        // Distance in metres plus an explicit warning penalty; closed cells are never traversable.
        public static List<Vector2Int> FindRiskAwarePath(HashSet<Vector2Int> cells, Vector2Int start,
            HashSet<Vector2Int> blocked, Dictionary<Vector2Int, float> risk, out int exitIndex)
        {
            if (risk.Count == 0) return FindPath(cells, start, blocked, out exitIndex);
            exitIndex = -1;
            if (!cells.Contains(start) || blocked.Contains(start)) return new List<Vector2Int>();
            var open = new List<Vector2Int> { start };
            var cost = new Dictionary<Vector2Int, float> { [start] = 0 };
            var previous = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            var visited = new HashSet<Vector2Int>();
            while (open.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (cost[open[i]] < cost[open[best]]) best = i;
                var p = open[best]; open.RemoveAt(best);
                if (!visited.Add(p)) continue;
                int target = Array.IndexOf(Exits, p);
                if (target >= 0)
                {
                    exitIndex = target; var path = new List<Vector2Int> { p };
                    while (p != start) { p = previous[p]; path.Add(p); }
                    path.Reverse(); return path;
                }
                foreach (var d in Directions)
                {
                    var n = p + d;
                    if (!cells.Contains(n) || blocked.Contains(n) || visited.Contains(n)) continue;
                    float next = cost[p] + CellSize + (risk.TryGetValue(n, out float penalty) ? Mathf.Max(0, penalty) : 0);
                    if (cost.TryGetValue(n, out float known) && next >= known) continue;
                    cost[n] = next; previous[n] = p; if (!open.Contains(n)) open.Add(n);
                }
            }
            return new List<Vector2Int>();
        }

        // Deterministic breadth-first search is shortest-path planning on this equal-cost graph.
        public static List<Vector2Int> FindPath(HashSet<Vector2Int> cells, Vector2Int start,
            HashSet<Vector2Int> blocked, out int exitIndex)
        {
            exitIndex = -1;
            if (!cells.Contains(start) || blocked.Contains(start)) return new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(start); previous[start] = start;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                exitIndex = Array.IndexOf(Exits, p);
                if (exitIndex >= 0)
                {
                    var path = new List<Vector2Int> { p };
                    while (p != start) { p = previous[p]; path.Add(p); }
                    path.Reverse(); return path;
                }
                foreach (var d in Directions)
                {
                    var n = p + d;
                    if (!cells.Contains(n) || blocked.Contains(n) || previous.ContainsKey(n)) continue;
                    previous[n] = p; queue.Enqueue(n);
                }
            }
            return new List<Vector2Int>();
        }

        public static Material Material(string name, Color color, bool emission = false)
        {
            var template = Resources.Load<Material>(emission ? "MiningEmissive" : "MiningLit");
            var m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = name; m.color = color; m.SetFloat("_Smoothness", .15f);
            if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 2f); }
            return m;
        }

        public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) { var c = go.GetComponent<Collider>(); c.enabled = false; }
            return go;
        }

        public static void Build(Transform root, HashSet<Vector2Int> cells)
        {
            var shell = new GameObject("Terrain + Collision | sealed tunnel shell").transform; shell.SetParent(root, false);
            var props = new GameObject("Environment | supports, rails, pipes, lamps").transform; props.SetParent(root, false);
            var rock = Material("Stratified sandstone", new Color(.29f, .255f, .21f));
            var ground = Material("Compacted gravel", new Color(.20f, .18f, .15f));
            var timber = Material("Aged timber", new Color(.21f, .12f, .065f));
            var steel = Material("Galvanized steel", new Color(.19f, .23f, .25f));
            var lamp = Material("Amber safety lamp", new Color(1f, .65f, .23f), true);
            var green = Material("Refuge lighting", new Color(.08f, .8f, .44f), true);
            var texture = new Texture2D(128, 128, TextureFormat.RGB24, true);
            texture.name = "Procedural mineral grain";
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float grain = Mathf.PerlinNoise(x * .22f, y * .22f) * .3f + Mathf.PerlinNoise(x * .065f, y * .31f) * .5f;
                texture.SetPixel(x, y, Color.Lerp(new Color(.3f, .28f, .24f), new Color(.85f, .8f, .7f), grain));
            }
            texture.Apply(); rock.mainTexture = texture; ground.mainTexture = texture;
            var verts = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            foreach (var cell in cells)
            {
                var p = World(cell);
                Box(shell, "Continuous floor", p + Vector3.down * .2f, new Vector3(6.002f, .4f, 6.002f), ground);
                // Ceiling patches share exactly the same world-space boundary samples.
                for (int x = 0; x < 6; x++) for (int z = 0; z < 6; z++)
                {
                    Vector3 a = p + new Vector3(x - 3, 0, z - 3), b = a + Vector3.right;
                    Vector3 c = b + Vector3.forward, d = a + Vector3.forward;
                    a.y = Roof(a); b.y = Roof(b); c.y = Roof(c); d.y = Roof(d);
                    Quad(verts, triangles, uv, a, b, c, d); // faces downward
                }
                foreach (var dir in Directions)
                {
                    if (cells.Contains(cell + dir)) continue;
                    Vector3 normal = new Vector3(dir.x, 0, dir.y), tangent = Vector3.Cross(Vector3.up, normal);
                    Vector3 edge = p + normal * 3;
                    for (int s = 0; s < 6; s++) for (int h = 0; h < 5; h++)
                    {
                        Vector3 a = Wall(edge, tangent, normal, s, h), b = Wall(edge, tangent, normal, s + 1, h);
                        Vector3 c = Wall(edge, tangent, normal, s + 1, h + 1), d = Wall(edge, tangent, normal, s, h + 1);
                        Quad(verts, triangles, uv, b, a, d, c);
                    }
                    Box(props, "Wall footing", edge + Vector3.up * .12f, new Vector3(dir.x == 0 ? 6 : .26f, .24f, dir.x == 0 ? .26f : 6), rock);
                }
                bool vertical = cells.Contains(cell + Vector2Int.up) || cells.Contains(cell + Vector2Int.down);
                bool horizontal = cells.Contains(cell + Vector2Int.left) || cells.Contains(cell + Vector2Int.right);
                if (!(vertical && horizontal))
                {
                    var support = new GameObject("Timber frame").transform; support.SetParent(props, false); support.localPosition = p;
                    if (!vertical) support.localRotation = Quaternion.Euler(0, 90, 0);
                    Box(support, "Left post", new Vector3(-2.65f, 2, 0), new Vector3(.25f, 4, .3f), timber);
                    Box(support, "Right post", new Vector3(2.65f, 2, 0), new Vector3(.25f, 4, .3f), timber);
                    Box(support, "Crossbeam", new Vector3(0, 4, 0), new Vector3(5.65f, .3f, .32f), timber);
                    Box(support, "Ventilation pipe", new Vector3(2.3f, 3.6f, 0), new Vector3(.25f, .25f, 6.01f), steel, false);
                    for (int side = -1; side <= 1; side += 2)
                        Box(support, "Mine rail", new Vector3(side * .72f, .045f, 0), new Vector3(.065f, .09f, 6.01f), steel, false);
                    for (int z = -2; z <= 2; z += 2)
                        Box(support, "Rail sleeper", new Vector3(0, .025f, z), new Vector3(1.95f, .05f, .22f), timber, false);
                }
                if ((cell.x + cell.y) % 2 == 0)
                {
                    var bulb = Box(props, "Caged mine light", p + new Vector3(0, 3.8f, 0), new Vector3(.32f, .18f, .32f), lamp, false);
                    var light = bulb.AddComponent<Light>(); light.color = new Color(1f, .72f, .42f); light.range = 11; light.intensity = 2.4f;
                }
            }
            var mesh = new Mesh { name = "Continuous rock shell", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var rockShell = new GameObject("Rock walls and ceiling", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            rockShell.transform.SetParent(shell, false); rockShell.GetComponent<MeshFilter>().sharedMesh = mesh;
            rockShell.GetComponent<MeshRenderer>().sharedMaterial = rock; rockShell.GetComponent<MeshCollider>().sharedMesh = mesh;
            for (int i = 0; i < Exits.Length; i++)
            {
                Vector3 p = World(Exits[i]);
                Box(props, "Refuge pad", p + Vector3.up * .018f, new Vector3(4, .035f, 4), green, false);
                Sign(props, p + new Vector3(0, 3.1f, 2), "ZONA AMAN " + (i + 1) + "\nREFUGE / EVAKUASI", new Color(.3f, 1, .65f));
                Box(props, "Emergency cabinet", p + new Vector3(2.1f, .8f, 1.8f), new Vector3(.7f, 1.6f, .5f), steel);
            }
            Sign(props, new Vector3(0, 3.1f, -17), "SAFE-MINING EVAC\nGALERI UTAMA  /  -120 m", Color.white);
            Sign(props, new Vector3(0, 3.2f, 1.8f), "BARAT  <     PUSAT     >  TIMUR\nJALUR EVAKUASI", new Color(1, .8f, .35f));
        }

        static float Roof(Vector3 p) => Ceiling + Mathf.PerlinNoise(p.x * .19f + 50, p.z * .19f + 50) * .45f;
        static Vector3 Wall(Vector3 edge, Vector3 tangent, Vector3 normal, int s, int h)
        {
            Vector3 p = edge + tangent * (s - 3);
            float top = Roof(p); p.y = top * h / 5;
            // Boundary vertices stay on the exact shared seam; only interior vertices recede.
            if (s > 0 && s < 6 && h > 0 && h < 5)
                p += normal * (.05f + Mathf.PerlinNoise(p.x + p.y * 3 + 50, p.z + 50) * .32f);
            return p;
        }
        static void Quad(List<Vector3> v, List<int> t, List<Vector2> uv, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count; v.AddRange(new[] { a, b, c, d });
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            uv.AddRange(new[] { new Vector2(a.x + a.z, a.y + a.z), new Vector2(b.x + b.z, b.y + b.z), new Vector2(c.x + c.z, c.y + c.z), new Vector2(d.x + d.z, d.y + d.z) });
        }
        public static void Sign(Transform parent, Vector3 p, string text, Color color)
        {
            var go = new GameObject("Mine signage", typeof(TextMesh)); go.transform.SetParent(parent, false); go.transform.localPosition = p;
            go.transform.localRotation = Quaternion.identity;
            var label = go.GetComponent<TextMesh>(); label.text = text; label.fontSize = 48; label.characterSize = .06f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font.RequestCharactersInTexture(text, 48);
            var shader = Resources.Load<Shader>("MiningWorldSign");
            if (shader != null)
            {
                var signMaterial = new Material(shader); signMaterial.mainTexture = label.font.material.mainTexture;
                go.GetComponent<MeshRenderer>().sharedMaterial = signMaterial;
            }
        }
    }
}
