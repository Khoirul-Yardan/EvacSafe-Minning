"""Safety/distance ordering and clearance regression tests, without a broker."""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import edge_routing as routing


def mine():
    cells = {(0, z) for z in range(-5, 11)}
    for z in (0, 3, 6):
        cells.update((x, z) for x in range(-4, 5))
    for x in (-4, 4):
        cells.update((x, z) for z in range(9))
    exits = [(0, 10), (-4, 8), (4, 8)]
    for x, z in exits:
        cells.update((x + dx, z + dz) for dx in (-1, 0, 1) for dz in (0, 1))
    return cells, exits


def request(start=(0, -3)):
    return dict(sessionId="0" * 32, layoutId="A" * 64, sequence=1, stateRevision=1,
                simulationTimeS=25, startX=start[0], startZ=start[1], stepCost=6, warningPenalty=24)


class RoutingTest(unittest.TestCase):
    def plan(self, closed=(), warning=(), start=(0, -3)):
        cells, exits = mine()
        response = routing.plan_route(request(start), cells, exits, set(closed), set(warning))
        return response, list(zip(response["pathX"], response["pathZ"]))

    def test_center_and_left_closed_take_lower_right_junction(self):
        response, path = self.plan([(0, 4), (-4, 5)])
        self.assertEqual(response["exitIndex"], 2)
        self.assertIn((1, 0), path)
        self.assertNotIn((0, 2), path)
        self.assertNotIn((0, 3), path)
        self.assertNotIn((1, 3), path)
        self.assertEqual(response["totalCost"], (len(path) - 1) * 6)

    def test_worker_already_near_danger_retreats_to_clear_junction(self):
        _, path = self.plan([(0, 4), (-4, 5)], start=(0, 2))
        self.assertEqual(path[1], (0, 1))
        self.assertIn((1, 0), path)

    def test_mirrored_hazard_chooses_left(self):
        response, path = self.plan([(0, 4), (4, 5)])
        self.assertEqual(response["exitIndex"], 1)
        self.assertIn((-1, 0), path)

    def test_warning_also_gets_clearance(self):
        response, path = self.plan([(0, 4)], [(-4, 5)])
        self.assertEqual(response["exitIndex"], 2)
        self.assertIn((1, 0), path)

    def test_no_hazards_keeps_shortest_central_route(self):
        response, path = self.plan()
        self.assertEqual(response["exitIndex"], 0)
        self.assertTrue(all(x == 0 for x, z in path))

    def test_clearance_is_not_a_hard_block_when_only_exit_is_nearby(self):
        cells = {(x, 0) for x in range(4)} | {(2, 1)}
        result = routing.plan_route(request((0, 0)), cells, [(3, 0)], {(2, 1)}, set())
        self.assertEqual(result["outcome"], "route")
        self.assertGreater(result["totalCost"], 18)

    def test_risk_does_not_cross_missing_corridor(self):
        risk = routing.hazard_risk({(0, 0), (2, 0)}, {(0, 0)}, set(), 6, 24)
        self.assertNotIn((2, 0), risk)

    def test_closed_start_has_no_route(self):
        result, path = self.plan([(0, -3)])
        self.assertEqual(result["outcome"], "no_path")
        self.assertEqual(path, [])


if __name__ == "__main__":
    unittest.main()
