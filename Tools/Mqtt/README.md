# Broker demo SAFE-MINING

Dari root proyek dengan Docker Desktop aktif:

```powershell
docker compose --profile simulated-edge -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d --build
```

Perintah tersebut menyalakan Mosquitto dan layanan keputusan edge Python di container terpisah. Unity mengirim sampel sensor; layanan Python mengolah ambang/durasi dan mengirim status kembali ke Unity. Scene utama menggunakan `MqttEdgeSimulation`, `127.0.0.1:1883`. Broker mengikat port host hanya pada loopback. Lihat [panduan implementasi](../../Documentation/EDGE_MQTT.md).

Python juga menghitung rute. Jika hanya broker aktif, Unity akan berhenti menunggu rute setelah lima detik. Pastikan kedua layanan berstatus **Up**, lalu pilih **Ulangi skenario**:

```powershell
docker compose --profile simulated-edge -p safe-mining-edge -f Tools/Mqtt/compose.yaml ps
```

Setelah perubahan planner, gunakan kembali perintah `up -d --build` di atas agar Python dan Unity memakai versi yang sesuai.

Amati pesan yang benar-benar melewati broker:

```powershell
docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml exec broker mosquitto_sub -h localhost -t 'safe-mining/v1/+/+/+/+' -q 1 -v
```

Tekan Ctrl+C untuk menutup pengamatan. Uji putus/sambung tanpa mengubah sumber data:

Untuk perangkat edge fisik di mesin lain, jalankan Mosquitto saja dan buka listener LAN dengan `MQTT_BIND_ADDRESS=0.0.0.0 docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d broker`. Arahkan perangkat ke IP LAN komputer ini pada port 1883, lalu gunakan topik sampel/status sesuai kontrak panduan. Jangan jalankan layanan `python-edge` bersama perangkat yang menerbitkan status untuk sesi yang sama. Broker demo ini tanpa autentikasi/TLS; batasi akses firewall hanya ke perangkat eksperimen.

```powershell
docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml stop broker
docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml start broker
```

Hentikan broker setelah demo:

```powershell
docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml down
```

Jika port 1883 sudah digunakan, sesuaikan port host pada compose dan `mqttSettings.port` pada Inspector sebelum sesi dimulai. Tidak ada layanan yang diinstal sebagai startup Windows. Broker ini tanpa autentikasi/TLS dan hanya untuk demo lokal.
