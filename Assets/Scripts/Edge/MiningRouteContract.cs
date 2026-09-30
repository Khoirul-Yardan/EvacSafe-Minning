using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SafeMining
{
    [Serializable]
    public sealed class EdgeRouteRequest
    {
        public int schemaVersion = 1;
        public string sessionId, layoutId, source = "unity-navigation", policy;
        public long sequence, stateRevision;
        public float simulationTimeS, stepCost = MineLayout.CellSize, warningPenalty;
        public int startX, startZ;
        public int[] cellsX, cellsZ, exitsX, exitsZ, devicesX, devicesZ, closedX, closedZ, warningX, warningZ;
        public string Topic => "safe-mining/v1/" + sessionId + "/navigation/request";
    }

    [Serializable]
    public sealed class EdgeRouteResponse
    {
        public int schemaVersion = -1, exitIndex = -2, startX, startZ;
        public string sessionId, layoutId, source, outcome, planner;
        public long sequence = -1, stateRevision = -1;
        public float simulationTimeS = -1, totalCost = -1, planningMs = -1;
        public int[] pathX, pathZ;
    }

    public static class MiningRouteContract
    {
        public const string Planner = "python-edge-safety-dijkstra-v2";
        const string JsonString = "\"(?:[^\"\\\\\\x00-\\x1F]|\\\\(?:[\"\\\\/bfnrt]|u[0-9a-fA-F]{4}))*\"";
        const string Number = "-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?";
        const string Integer = "-?(?:0|[1-9][0-9]*)";
        static readonly Regex Field = new Regex("\\G\\s*(?<key>" + JsonString + ")\\s*:\\s*(?<value>" +
            JsonString + "|\\[\\s*(?:" + Integer + "(?:\\s*,\\s*" + Integer + ")*)?\\s*\\]|" + Number +
            ")\\s*(?<end>[,}])", RegexOptions.CultureInvariant);
        static readonly HashSet<string> Strings = new HashSet<string> { "sessionId", "layoutId", "source", "outcome", "planner" };
        static readonly HashSet<string> Integers = new HashSet<string> { "schemaVersion", "exitIndex", "startX", "startZ", "sequence", "stateRevision" };
        static readonly HashSet<string> Numbers = new HashSet<string> { "simulationTimeS", "totalCost", "planningMs" };

        public static EdgeRouteRequest Create(string session, string layout, long sequence, long revision,
            float time, Vector2Int start, HashSet<Vector2Int> cells, IList<Vector2Int> devices,
            HashSet<Vector2Int> closed, Dictionary<Vector2Int, float> risk, float warningPenalty, bool adaptive)
        {
            if (float.IsNaN(warningPenalty) || float.IsInfinity(warningPenalty) || warningPenalty < 0 || warningPenalty > 1000000)
                throw new ArgumentException("Penalti peringatan harus finite antara 0 dan 1000000.");
            var request = new EdgeRouteRequest { sessionId = session, layoutId = layout, sequence = sequence,
                stateRevision = revision, simulationTimeS = time, startX = start.x, startZ = start.y,
                warningPenalty = warningPenalty, policy = adaptive ? "adaptive" : "static" };
            Split(Sorted(cells), out request.cellsX, out request.cellsZ);
            Split(MineLayout.Exits, out request.exitsX, out request.exitsZ);
            Split(devices, out request.devicesX, out request.devicesZ);
            Split(adaptive ? Sorted(closed) : new List<Vector2Int>(), out request.closedX, out request.closedZ);
            Split(adaptive ? Sorted(risk.Keys) : new List<Vector2Int>(), out request.warningX, out request.warningZ);
            return request;
        }
        static List<Vector2Int> Sorted(IEnumerable<Vector2Int> points)
        {
            var result = new List<Vector2Int>(points);
            result.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x)); return result;
        }
        static void Split(IList<Vector2Int> points, out int[] xs, out int[] zs)
        {
            xs = new int[points.Count]; zs = new int[points.Count];
            for (int i = 0; i < points.Count; i++) { xs[i] = points[i].x; zs[i] = points[i].y; }
        }
        // Flat scalar fields and two bounded integer arrays; reject permissive JsonUtility inputs first.
        public static EdgeRouteResponse Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 12288) return null;
            json = json.Trim(); if (json.Length < 2 || json[0] != '{') return null;
            var seen = new HashSet<string>(); int offset = 1;
            while (offset < json.Length)
            {
                var match = Field.Match(json, offset); if (!match.Success) return null;
                string key = match.Groups["key"].Value; key = key.Substring(1, key.Length - 2);
                string value = match.Groups["value"].Value;
                if (!seen.Add(key)) return null;
                if (Strings.Contains(key)) { if (value[0] != '"') return null; }
                else if (Integers.Contains(key))
                {
                    if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _) ||
                        ((key != "sequence" && key != "stateRevision") && !int.TryParse(value,
                            NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))) return null;
                }
                else if (Numbers.Contains(key))
                {
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) ||
                        float.IsNaN(n) || float.IsInfinity(n) || n < 0) return null;
                }
                else if (key == "pathX" || key == "pathZ")
                {
                    if (value[0] != '[') return null;
                    string inner = value.Substring(1, value.Length - 2).Trim();
                    if (inner.Length > 0)
                    {
                        var elements = inner.Split(','); if (elements.Length > 300) return null;
                        foreach (string element in elements)
                            if (!int.TryParse(element.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                                out int coordinate) || coordinate < -30 || coordinate > 30) return null;
                    }
                }
                else return null;
                offset = match.Index + match.Length;
                if (match.Groups["end"].Value == "}")
                {
                    if (offset != json.Length || seen.Count != 16) return null;
                    try { return JsonUtility.FromJson<EdgeRouteResponse>(json); }
                    catch (ArgumentException) { return null; }
                }
            }
            return null;
        }

        // Validate topology/cost, without recomputing a shortest path in Unity.
        public static bool Validate(string topic, EdgeRouteResponse result, EdgeRouteRequest request, out string reason)
        {
            reason = "invalid_route_contract";
            if (result == null || request == null || result.schemaVersion != 1 || result.source != "python-edge" ||
                result.sessionId != request.sessionId ||
                result.layoutId != request.layoutId || result.sequence != request.sequence || result.stateRevision != request.stateRevision ||
                result.startX != request.startX || result.startZ != request.startZ ||
                result.simulationTimeS != request.simulationTimeS || result.pathX == null || result.pathZ == null ||
                result.pathX.Length != result.pathZ.Length || result.pathX.Length > 300 ||
                topic != "safe-mining/v1/" + request.sessionId + "/navigation/response") return false;
            if (result.planner != Planner) { reason = "navigation_planner_version_mismatch"; return false; }
            if (result.outcome == "no_path")
                return result.exitIndex == -1 && result.pathX.Length == 0 && result.totalCost == 0;
            if (result.outcome != "route" || result.pathX.Length == 0 || result.exitIndex < 0 || result.exitIndex >= request.exitsX.Length)
                return false;
            var cells = Points(request.cellsX, request.cellsZ); var closed = Points(request.closedX, request.closedZ);
            var warning = Points(request.warningX, request.warningZ); var visited = new HashSet<Vector2Int>();
            var warnings = new Dictionary<Vector2Int, float>();
            foreach (var cell in warning) warnings[cell] = request.warningPenalty;
            var risk = MineLayout.HazardRisk(cells, closed, warnings, request.stepCost);
            Vector2Int previous = new Vector2Int(request.startX, request.startZ); float cost = 0;
            for (int i = 0; i < result.pathX.Length; i++)
            {
                var cell = new Vector2Int(result.pathX[i], result.pathZ[i]);
                if (!cells.Contains(cell) || closed.Contains(cell) || !visited.Add(cell) ||
                    (i == 0 && cell != previous) || (i > 0 && Math.Abs(cell.x - previous.x) + Math.Abs(cell.y - previous.y) != 1)) return false;
                if (i > 0) cost += request.stepCost + (risk.TryGetValue(cell, out float penalty) ? penalty : 0);
                previous = cell;
            }
            if (previous != new Vector2Int(request.exitsX[result.exitIndex], request.exitsZ[result.exitIndex]) ||
                Mathf.Abs(cost - result.totalCost) > Mathf.Max(.001f, cost * .00001f)) return false;
            reason = ""; return true;
        }
        static HashSet<Vector2Int> Points(int[] xs, int[] zs)
        {
            var points = new HashSet<Vector2Int>();
            for (int i = 0; i < xs.Length; i++) points.Add(new Vector2Int(xs[i], zs[i])); return points;
        }
    }
}
