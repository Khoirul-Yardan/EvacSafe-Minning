"""Bounded, deterministic route planning executed by the Python edge process.

Requests carry immutable graph/status snapshots. Unity applies only the result
for its current request, position and hazard revision; no Unity path search is
used on this MQTT route path.
"""
import hashlib
import heapq
import json
import math
import re
import time


ROOT = "safe-mining/v1/"
PLANNER = "python-edge-dijkstra-v1"
DIRECTIONS = ((0, 1), (-1, 0), (1, 0), (0, -1))
FIELDS = {
    "schemaVersion", "sessionId", "layoutId", "source", "sequence", "stateRevision",
    "simulationTimeS", "policy", "startX", "startZ", "stepCost", "warningPenalty",
    "cellsX", "cellsZ", "exitsX", "exitsZ", "closedX", "closedZ", "warningX", "warningZ",
    "devicesX", "devicesZ",
}


def integer(value, low, high):
    return type(value) is int and low <= value <= high


def number(value, low, high):
    return type(value) in (int, float) and math.isfinite(value) and low <= value <= high


def unique_fields(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("duplicate JSON field")
        result[key] = value
    return result


def coordinates(request, prefix, maximum, allow_empty=False):
    xs, zs = request[prefix + "X"], request[prefix + "Z"]
    if (not isinstance(xs, list) or not isinstance(zs, list) or len(xs) != len(zs)
            or len(xs) > maximum or (not xs and not allow_empty)
            or not all(integer(v, -30, 30) for v in xs + zs)):
        raise ValueError("invalid coordinate arrays")
    points = list(zip(xs, zs))
    if len(set(points)) != len(points):
        raise ValueError("duplicate coordinates")
    return points


def parse_request(topic, payload, retained=False):
    if retained or len(payload) > 12288:
        raise ValueError("retained or oversized request")
    request = json.loads(payload, object_pairs_hook=unique_fields)
    if not isinstance(request, dict) or set(request) != FIELDS:
        raise ValueError("invalid route schema")
    session, layout = request["sessionId"], request["layoutId"]
    if (not isinstance(session, str) or not re.fullmatch(r"[a-fA-F0-9]{32}", session)
            or not isinstance(layout, str) or not re.fullmatch(r"[A-F0-9]{64}", layout)
            or topic != f"{ROOT}{session}/navigation/request"
            or type(request["schemaVersion"]) is not int or request["schemaVersion"] != 1
            or request["source"] != "unity-navigation"
            or request["policy"] not in ("adaptive", "static")
            or not integer(request["sequence"], 1, 2**53 - 1)
            or not integer(request["stateRevision"], 0, 2**53 - 1)
            or not number(request["simulationTimeS"], 0, 86400)
            or not number(request["stepCost"], 0.001, 1000)
            or not number(request["warningPenalty"], 0, 1000000)
            or not integer(request["startX"], -30, 30)
            or not integer(request["startZ"], -30, 30)):
        raise ValueError("invalid route metadata")
    cells = coordinates(request, "cells", 300)
    exits = coordinates(request, "exits", 3)
    devices = coordinates(request, "devices", 12)
    closed = coordinates(request, "closed", 300, True)
    warning = coordinates(request, "warning", 300, True)
    occupied = set(cells)
    if (not set(exits + devices + closed + warning) <= occupied
            or set(closed) & set(warning)
            or (request["policy"] == "static" and (closed or warning))):
        raise ValueError("invalid graph status snapshot")
    # Matches MiningEdgeSession.LayoutHash, including ordered detector locations.
    layout_text = "".join(f"{x}:{z};" for x, z in sorted(cells)) + "|"
    layout_text += "".join(f"{x}:{z};" for x, z in devices)
    if hashlib.sha256(layout_text.encode("utf-8")).hexdigest().upper() != layout:
        raise ValueError("layout hash mismatch")
    return request, occupied, exits, set(closed), set(warning)


def plan_route(request, cells, exits, closed, warning):
    started = time.perf_counter_ns()
    start = request["startX"], request["startZ"]
    path, target, total = [], -1, 0.0
    if start in cells and start not in closed:
        # Insertion ordinal fixes tie ordering to up, left, right, down.
        queue = [(0.0, 0, start)]
        distance, previous, ordinal = {start: 0.0}, {start: start}, 0
        while queue:
            cost, _, cell = heapq.heappop(queue)
            if cost != distance[cell]:
                continue
            if cell in exits:
                target, total = exits.index(cell), cost
                path = [cell]
                while cell != start:
                    cell = previous[cell]
                    path.append(cell)
                path.reverse()
                break
            for dx, dz in DIRECTIONS:
                neighbor = cell[0] + dx, cell[1] + dz
                if neighbor not in cells or neighbor in closed:
                    continue
                candidate = cost + request["stepCost"]
                if neighbor in warning:
                    candidate += request["warningPenalty"]
                if candidate >= distance.get(neighbor, math.inf):
                    continue
                distance[neighbor], previous[neighbor] = candidate, cell
                ordinal += 1
                heapq.heappush(queue, (candidate, ordinal, neighbor))
    planning_ms = (time.perf_counter_ns() - started) / 1000000.0
    return {
        "schemaVersion": 1, "sessionId": request["sessionId"], "layoutId": request["layoutId"],
        "source": "python-edge", "sequence": request["sequence"], "stateRevision": request["stateRevision"],
        "simulationTimeS": request["simulationTimeS"], "outcome": "route" if path else "no_path",
        "planner": PLANNER, "exitIndex": target, "startX": start[0], "startZ": start[1],
        "pathX": [x for x, _ in path], "pathZ": [z for _, z in path],
        "totalCost": total, "planningMs": planning_ms,
    }


def handle_route(client, packet):
    try:
        request, cells, exits, closed, warning = parse_request(packet.topic, packet.payload, packet.retain)
        response = plan_route(request, cells, exits, closed, warning)
        client.publish(f"{ROOT}{request['sessionId']}/navigation/response",
                       json.dumps(response, separators=(",", ":")), qos=1, retain=False)
    except (UnicodeDecodeError, json.JSONDecodeError, TypeError, ValueError, KeyError) as error:
        print(f"Rejected route request: {error}", flush=True)
