using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SafeMining;
using UnityEngine;

public static class MiningRoutingValidation
{
    static void Check(bool value, string detail) { if (!value) throw new Exception(detail); }
    public static void Core(List<string> results)
    {
        var cells = MineLayout.CreateCells();
        var sites = MiningHazardScenario.DetectorSites(cells);
        var closed = new HashSet<Vector2Int> { new Vector2Int(0, 4), new Vector2Int(-4, 5) };
        var empty = new Dictionary<Vector2Int, float>();
        var start = new Vector2Int(0, -3);
        var path = MineLayout.FindRiskAwarePath(cells, start, closed, empty, out int exit);
        Check(exit == 2 && path.Contains(new Vector2Int(1, 0)) && !path.Contains(new Vector2Int(0, 2)), "Did not use early right detour");
        var retreat = MineLayout.FindRiskAwarePath(cells, new Vector2Int(0, 2), closed, empty, out _);
        Check(retreat[1] == new Vector2Int(0, 1), "Worker approached danger instead of retreating");
        var mirrored = new HashSet<Vector2Int> { new Vector2Int(0, 4), new Vector2Int(4, 5) };
        var left = MineLayout.FindRiskAwarePath(cells, start, mirrored, empty, out exit);
        Check(exit == 1 && left.Contains(new Vector2Int(-1, 0)), "Planner is hard-coded to the right");
        var warnings = new Dictionary<Vector2Int, float> { [new Vector2Int(-4, 5)] = 24 };
        MineLayout.FindRiskAwarePath(cells, start, new HashSet<Vector2Int> { new Vector2Int(0, 4) }, warnings, out exit);
        Check(exit == 2, "Warning clearance ignored");
        var baseline = MineLayout.FindRiskAwarePath(cells, start, new HashSet<Vector2Int>(), empty, out exit);
        Check(exit == 0 && baseline.All(c => c.x == 0), "No-hazard shortest route changed");
        results.Add("PASS safety routing: early right detour for center/left closures, retreat from danger, mirrored left detour, warning clearance, unchanged no-hazard route.");

        var firstSites = new HashSet<int>();
        for (int seed = 0; seed < 128; seed++)
        {
            var schedule = MiningHazardScenario.Create(cells, sites, HazardScenarioMode.Random, seed, 3, 6, 6, 4);
            var replay = MiningHazardScenario.Create(cells, sites, HazardScenarioMode.Random, seed, 3, 6, 6, 4);
            Check(schedule.Count == 3 && schedule.Select(s => s.detectorIndex).Distinct().Count() == 3, "Random duplicated sites");
            Check(schedule.Zip(replay, (a, b) => a.detectorIndex == b.detectorIndex && a.warningTime == b.warningTime && a.collapseTime == b.collapseTime).All(v => v), "Seed replay changed");
            firstSites.Add(schedule[0].detectorIndex);
        }
        Check(firstSites.Count == sites.Count, "First random event excludes detectors");
        results.Add("PASS 128 random seeds: every detector eligible as first event; no duplicate sites per run; same seed reproduces locations/timing.");

        // Save actual C# paths and contract snapshots for independent Python parity checks.
        Directory.CreateDirectory("Validation");
        var fixtures = new List<string>();
        for (int sample = 0; sample < 32; sample++)
        {
            var blocks = new HashSet<Vector2Int> { sites[sample % sites.Count] };
            var warn = new Dictionary<Vector2Int, float>();
            warn[sites[(sample + 1) % sites.Count]] = 24;
            var route = MineLayout.FindRiskAwarePath(cells, start, blocks, warn, out int target);
            using (var session = new MiningEdgeSession(HazardSource.LocalEdgeSimulation, 1, new List<ScheduledRockfall>(), cells, sites,
                new MiningEdgeSettings(), new MiningMqttSettings(), (index, level) => { }))
            {
                var req = MiningRouteContract.Create(session.SessionId, session.LayoutId, 1, 1, 0, start, cells, sites, blocks, warn, 24, true);
                var risk = MineLayout.HazardRisk(cells, blocks, warn);
                float cost = 0;
                foreach (var cell in route.Skip(1)) cost += 6 + (risk.TryGetValue(cell, out float value) ? value : 0);
                var response = new EdgeRouteResponse { schemaVersion = 1, sessionId = req.sessionId, layoutId = req.layoutId,
                    source = "python-edge", planner = "python-edge-safety-dijkstra-v2", sequence = 1, stateRevision = 1,
                    simulationTimeS = 0, startX = start.x, startZ = start.y, exitIndex = target,
                    outcome = route.Count == 0 ? "no_path" : "route", totalCost = cost, planningMs = 0,
                    pathX = route.Select(c => c.x).ToArray(), pathZ = route.Select(c => c.y).ToArray() };
                Check(MiningRouteContract.Validate("safe-mining/v1/" + req.sessionId + "/navigation/response", response, req, out _), "Safety cost rejected by contract");
                fixtures.Add("{\"request\":" + JsonUtility.ToJson(req) + ",\"response\":" + JsonUtility.ToJson(response) + "}");
                response.planner = "python-edge-dijkstra-v1";
                Check(!MiningRouteContract.Validate("safe-mining/v1/" + req.sessionId + "/navigation/response", response, req, out _), "Old planner silently accepted");
            }
        }
        File.WriteAllLines("Validation/safety-route-parity.jsonl", fixtures);
        results.Add("PASS 32 route contracts with clearance costs; outdated planner version rejected; parity fixtures exported.");
    }
}
