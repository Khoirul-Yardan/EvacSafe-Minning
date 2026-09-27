"""Decision-logic tests for edge_service.py. Standard library only; paho-mqtt is stubbed."""
import json
import os
import sys
import types
import unittest

# The service imports paho at module load; tests never open a socket, so a stub suffices.
_paho = types.ModuleType("paho")
_paho.mqtt = types.ModuleType("paho.mqtt")
_paho.mqtt.client = types.ModuleType("paho.mqtt.client")
sys.modules.update({"paho": _paho, "paho.mqtt": _paho.mqtt, "paho.mqtt.client": _paho.mqtt.client})
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import edge_service  # noqa: E402

SESSION = "session01"
LAYOUT = "A" * 64
STEP = 0.05  # Unity samples at 20 Hz.


class FakeClient:
    def __init__(self):
        self.published = []

    def publish(self, topic, payload, qos, retain):
        self.published.append((topic, json.loads(payload), qos, retain))


class Packet:
    def __init__(self, topic, payload):
        self.topic, self.payload = topic, payload


class EdgeServiceTest(unittest.TestCase):
    def setUp(self):
        edge_service.states.clear()
        self.client = FakeClient()
        self.sequence = {}

    def send(self, time_s, value, device="D01", **overrides):
        self.sequence[device] = self.sequence.get(device, 0) + 1
        sample = {"schemaVersion": 1, "sessionId": SESSION, "layoutId": LAYOUT, "deviceId": device,
                  "source": "unity-sensor-simulation", "sequence": self.sequence[device],
                  "simulationTimeS": round(time_s, 4), "vibrationNormalized": value,
                  "warningThreshold": 0.45, "dangerThreshold": 0.75, "clearThreshold": 0.30,
                  "minimumDurationS": 0.5, "clearDurationS": 1.0}
        sample.update(overrides)
        topic = "safe-mining/v1/" + SESSION + "/sensor/" + device + "/sample"
        edge_service.on_message(self.client, None, Packet(topic, json.dumps(sample).encode("utf-8")))

    def run_profile(self, start, seconds, value, device="D01"):
        steps = int(round(seconds / STEP))
        for i in range(steps):
            self.send(start + i * STEP, value, device)
        return start + steps * STEP

    def levels(self):
        return [status["level"] for _, status, _, _ in self.client.published]

    def test_first_sample_publishes_normal_status_with_unity_contract(self):
        self.send(0.0, 0.12)
        topic, status, qos, retain = self.client.published[0]
        self.assertEqual(topic, "safe-mining/v1/" + SESSION + "/edge/D01/status")
        self.assertEqual((qos, retain), (1, False))
        self.assertEqual(status["source"], "python-edge")
        self.assertEqual(status["eventId"], SESSION + "-D01-" + str(status["sequence"]))
        self.assertEqual(status["level"], 0)

    def test_sustained_warning_raises_level_one(self):
        self.run_profile(0.0, 1.0, 0.58)
        self.assertIn(1, self.levels())
        self.assertNotIn(2, self.levels())

    def test_short_spike_is_rejected(self):
        t = self.run_profile(0.0, 0.5, 0.12)
        t = self.run_profile(t, 0.2, 0.90)
        self.run_profile(t, 0.5, 0.12)
        self.assertEqual(set(self.levels()), {0})

    def test_danger_latches_after_vibration_drops(self):
        t = self.run_profile(0.0, 0.7, 0.90)
        self.run_profile(t, 3.0, 0.12)
        levels = self.levels()
        self.assertIn(2, levels)
        self.assertEqual(levels[levels.index(2):], [2] * (len(levels) - levels.index(2)))

    def test_warning_clears_after_stable_low_period(self):
        t = self.run_profile(0.0, 0.7, 0.58)
        self.run_profile(t, 1.5, 0.12)
        self.assertEqual(self.levels()[-1], 0)
        self.assertIn(1, self.levels())

    def test_sample_gap_resets_sustained_duration(self):
        # Two 0.3 s bursts separated by a 1 s gap must not add up to 0.5 s of warning.
        self.run_profile(0.0, 0.3, 0.58)
        self.run_profile(1.3, 0.3, 0.58)
        self.assertNotIn(1, self.levels())

    def test_heartbeat_repeats_unchanged_status_about_every_second(self):
        self.run_profile(0.0, 3.0, 0.12)
        self.assertEqual(len(self.client.published), 3)

    def test_old_or_duplicate_sequence_is_ignored(self):
        self.send(0.0, 0.12)
        count = len(self.client.published)
        self.send(1.5, 0.12, sequence=1)
        self.assertEqual(len(self.client.published), count)

    def test_invalid_samples_publish_nothing(self):
        self.send(0.0, 0.12, source="hardware")
        self.send(0.0, 0.12, device="D02", layoutId="not-a-hash")
        self.send(0.0, 1.5, device="D03")
        self.send(0.0, 0.12, device="D04", warningThreshold=0.2)  # clear < warning < danger violated
        self.assertEqual(self.client.published, [])

    def test_devices_are_processed_independently(self):
        self.run_profile(0.0, 1.0, 0.90, device="D01")
        self.run_profile(0.0, 1.0, 0.12, device="D02")
        by_device = {}
        for _, status, _, _ in self.client.published:
            by_device.setdefault(status["deviceId"], set()).add(status["level"])
        self.assertIn(2, by_device["D01"])
        self.assertEqual(by_device["D02"], {0})


if __name__ == "__main__":
    unittest.main()
