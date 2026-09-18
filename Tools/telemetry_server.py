"""Optional local WebSocket receiver for SAFE-MINING EVAC. No hazard commands by default."""
import argparse
import asyncio
import json
from pathlib import Path
from datetime import datetime, timezone

async def main():
    from websockets.asyncio.server import serve
    from websockets.exceptions import ConnectionClosed
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', type=int, default=8765)
    parser.add_argument('--demo-hazard', action='store_true')
    args = parser.parse_args()
    output = Path('Logs/telemetry.jsonl')
    output.parent.mkdir(parents=True, exist_ok=True)

    async def receive(socket):
        sent = False
        try:
            async for message in socket:
                try:
                    snapshot = json.loads(message)
                    if snapshot.get('type') != 'telemetry':
                        continue
                    with output.open('a', encoding='utf-8') as stream:
                        stream.write(json.dumps({'receivedUtc': datetime.now(timezone.utc).isoformat(), 'data': snapshot}) + '\n')
                    if snapshot.get('simulationTime', 0) < 1:
                        sent = False
                    if args.demo_hazard and not sent and snapshot.get('state') == 'Running' and snapshot.get('simulationTime', 0) >= 30:
                        await socket.send(json.dumps({'type': 'hazard', 'index': 2, 'level': 2}))
                        sent = True
                except (ValueError, TypeError, AttributeError) as error:
                    print('Invalid snapshot:', error)
        except ConnectionClosed:
            pass  # Unity may close the transport immediately when leaving Play Mode.

    async with serve(receive, '127.0.0.1', args.port, max_size=8192):
        print(f'Listening ws://127.0.0.1:{args.port}; logging {output.resolve()}')
        await asyncio.get_running_loop().create_future()

if __name__ == '__main__':
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
