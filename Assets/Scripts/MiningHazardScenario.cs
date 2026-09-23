using System;
using System.Collections.Generic;
using UnityEngine;

namespace SafeMining
{
    public enum HazardScenarioMode { Random, Scripted, NoHazards }

    [Serializable]
    public class ScheduledRockfall
    {
        public int detectorIndex;
        public float warningTime;
        public float collapseTime;
        public ScheduledRockfall(int index, float warning, float collapse)
        { detectorIndex = index; warningTime = warning; collapseTime = collapse; }
    }

    public static class MiningHazardScenario
    {
        // Stable station IDs for a given layout, independent of seed and navigation mode.
        public static List<Vector2Int> DetectorSites(HashSet<Vector2Int> cells)
        {
            var sites = new List<Vector2Int>();
            foreach (var cell in MineLayout.HazardCells) if (cells.Contains(cell)) sites.Add(cell);
            var candidates = new List<Vector2Int>();
            foreach (var cell in cells)
            {
                if ((cell - MineLayout.Spawn).sqrMagnitude < 16 || sites.Contains(cell)) continue;
                bool nearExit = false;
                foreach (var exit in MineLayout.Exits) if ((cell - exit).sqrMagnitude <= 4) nearExit = true;
                bool wall = false;
                foreach (var d in MineLayout.Directions) if (!cells.Contains(cell + d)) wall = true;
                if (!nearExit && wall) candidates.Add(cell);
            }
            candidates.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            while (sites.Count < 12 && candidates.Count > 0)
            {
                int best = -1, separation = 3;
                for (int i = 0; i < candidates.Count; i++)
                {
                    int nearest = int.MaxValue;
                    foreach (var site in sites) nearest = Math.Min(nearest, (candidates[i] - site).sqrMagnitude);
                    if (nearest > separation) { separation = nearest; best = i; }
                }
                if (best < 0) break;
                sites.Add(candidates[best]); candidates.RemoveAt(best);
            }
            return sites;
        }

        public static List<ScheduledRockfall> Create(HashSet<Vector2Int> cells, List<Vector2Int> sites,
            HazardScenarioMode mode, int seed, int count, float firstWarning, float interval, float warningDuration)
        {
            if (mode == HazardScenarioMode.NoHazards) return new List<ScheduledRockfall>();
            if (mode == HazardScenarioMode.Scripted)
                return new List<ScheduledRockfall> { new ScheduledRockfall(0, 6, 10), new ScheduledRockfall(1, 19, 23) };
            var random = new System.Random(seed);
            var available = new List<int>();
            for (int i = 0; i < sites.Count; i++) available.Add(i);
            var initialRoute = MineLayout.FindPath(cells, MineLayout.Spawn, new HashSet<Vector2Int>(), out _);
            var firstChoices = new List<int>();
            foreach (int i in available)
            {
                // Random challenge on the initial route, where an alternative exists initially.
                if (initialRoute.Contains(sites[i]) && MineLayout.FindPath(cells, MineLayout.Spawn,
                    new HashSet<Vector2Int> { sites[i] }, out _).Count > 0) firstChoices.Add(i);
            }
            var result = new List<ScheduledRockfall>();
            float time = Mathf.Max(4, firstWarning);
            float lead = Mathf.Max(2, warningDuration);
            for (int e = 0; e < Mathf.Clamp(count, 1, sites.Count); e++)
            {
                var pool = e == 0 && firstChoices.Count > 0 ? firstChoices : available;
                int selected = pool[random.Next(pool.Count)]; available.Remove(selected);
                float warning = time + (float)random.NextDouble() * 2;
                result.Add(new ScheduledRockfall(selected, warning, warning + lead));
                time = warning + lead + Mathf.Max(2, interval);
            }
            return result;
        }
    }
}
