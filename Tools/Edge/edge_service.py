"""MQTT edge decision service for the SafeMining simulation wire contract."""
import json
import math
import os
import re
import threading
import time

import paho.mqtt.client as mqtt
from edge_routing import handle_route


BROKER = os.getenv("MQTT_HOST", "broker")
PORT = int(os.getenv("MQTT_PORT", "1883"))
ROOT = "safe-mining/v1/"
DEVICE = re.compile(r"D\d{2}")
lock = threading.Lock()
states = {}


def finite_range(value, low, high):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) and low <= value <= high


def on_connect(client, userdata, flags, reason_code, properties):
    if reason_code == 0:
        client.subscribe(ROOT + "+/sensor/+/sample", qos=1)
        client.subscribe(ROOT + "+/navigation/request", qos=1)
        print("Connected; subscribed to sensor samples and route requests", flush=True)
    else:
        print(f"Broker rejected connection: {reason_code}", flush=True)


def on_message(client, userdata, packet):
    if packet.topic.endswith("/navigation/request"):
        handle_route(client, packet)
        return
    try:
        sample = json.loads(packet.payload.decode("utf-8"))
        if not isinstance(sample, dict) or set(sample) != {
            "schemaVersion", "sessionId", "layoutId", "deviceId", "source", "sequence",
            "simulationTimeS", "vibrationNormalized", "warningThreshold", "dangerThreshold",
            "clearThreshold", "minimumDurationS", "clearDurationS"
        }:
            return
        session, layout, device = sample["sessionId"], sample["layoutId"], sample["deviceId"]
        if (sample["schemaVersion"] != 1 or sample["source"] != "unity-sensor-simulation"
                or not isinstance(session, str) or not session or len(session) > 64
                or not isinstance(layout, str) or not re.fullmatch(r"[0-9A-F]{64}", layout)
                or not isinstance(device, str) or not DEVICE.fullmatch(device)
                or packet.topic != f"{ROOT}{session}/sensor/{device}/sample"
                or not isinstance(sample["sequence"], int) or isinstance(sample["sequence"], bool) or sample["sequence"] <= 0
                or not finite_range(sample["simulationTimeS"], 0, 86400)
                or not finite_range(sample["vibrationNormalized"], 0, 1)
                or not finite_range(sample["clearThreshold"], 0, 1)
                or not finite_range(sample["warningThreshold"], 0, 1)
                or not finite_range(sample["dangerThreshold"], 0, 1)
                or not finite_range(sample["minimumDurationS"], 0.01, 30)
                or not finite_range(sample["clearDurationS"], 0.01, 30)):
            return
        clear, warning, danger = sample["clearThreshold"], sample["warningThreshold"], sample["dangerThreshold"]
        if not clear < warning < danger:
            return
        key = (session, layout, device)
        now = sample["simulationTimeS"]
        value = sample["vibrationNormalized"]
        with lock:
            state = states.setdefault(key, {"sequence": 0, "sampleSequence": 0, "time": None,
                "level": 0, "warningHeld": 0.0, "dangerHeld": 0.0, "clearHeld": 0.0, "lastPublish": -1e9})
            if sample["sequence"] <= state["sampleSequence"] or (state["time"] is not None and now < state["time"]):
                return
            dt = 0 if state["time"] is None else now - state["time"]
            # A long gap cannot count as sustained vibration; require fresh contiguous samples.
            if dt > 0.25:
                state["warningHeld"] = state["dangerHeld"] = state["clearHeld"] = 0.0
                dt = 0
            state["sampleSequence"], state["time"] = sample["sequence"], now
            if state["level"] != 2:
                state["warningHeld"] = state["warningHeld"] + dt if value >= warning else 0.0
                state["dangerHeld"] = state["dangerHeld"] + dt if value >= danger else 0.0
                state["clearHeld"] = state["clearHeld"] + dt if value <= clear else 0.0
                if state["dangerHeld"] + 1e-6 >= sample["minimumDurationS"]:
                    state["level"] = 2
                elif state["warningHeld"] + 1e-6 >= sample["minimumDurationS"]:
                    state["level"] = 1
                elif state["clearHeld"] + 1e-6 >= sample["clearDurationS"]:
                    state["level"] = 0
            changed = state["level"] != state.get("publishedLevel", -1)
            heartbeat = now - state["lastPublish"] >= 1.0
            if not changed and not heartbeat:
                return
            state["sequence"] += 1
            status = {"schemaVersion": 1, "sessionId": session, "layoutId": layout,
                "eventId": f"{session}-{device}-{state['sequence']}", "deviceId": device,
                "sequence": state["sequence"], "simulationTimeS": now,
                "vibrationNormalized": value, "level": state["level"], "source": "python-edge"}
            state["publishedLevel"], state["lastPublish"] = state["level"], now
        out = f"{ROOT}{session}/edge/{device}/status"
        client.publish(out, json.dumps(status, separators=(",", ":")), qos=1, retain=False)
    except (UnicodeDecodeError, json.JSONDecodeError, TypeError, ValueError, KeyError) as error:
        print(f"Rejected sample: {error}", flush=True)


def main():
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id="safe-mining-python-edge", clean_session=True)
    client.on_connect = on_connect
    client.on_message = on_message
    while True:
        try:
            client.connect(BROKER, PORT, keepalive=15)
            client.loop_forever(retry_first_connection=True)
        except OSError as error:
            print(f"Broker unavailable: {error}; retrying", flush=True)
            time.sleep(2)


if __name__ == "__main__":
    main()
