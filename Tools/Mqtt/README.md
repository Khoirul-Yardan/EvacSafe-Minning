# Broker demo SAFE-MINING

Dari root proyek dengan Docker Desktop aktif:

```powershell
docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d
```

Scene utama menggunakan `MqttEdgeSimulation`, `127.0.0.1:1883`. Broker mengikat port host hanya pada loopback. Lihat [panduan implementasi](../../Documentation/EDGE_MQTT.md).

Amati pesan yang benar-benar melewati broker:

```powershell
docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml exec broker mosquitto_sub -h localhost -t 'safe-mining/v1/+/edge/+/status' -q 1 -v
```

Tekan Ctrl+C untuk menutup pengamatan. Uji putus/sambung tanpa mengubah sumber data:

```powershell
docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml stop broker
docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml start broker
```

Hentikan broker setelah demo:

```powershell
docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml down
```

Jika port 1883 sudah digunakan, sesuaikan port host pada compose dan `mqttSettings.port` pada Inspector sebelum sesi dimulai. Tidak ada layanan yang diinstal sebagai startup Windows. Broker ini tanpa autentikasi/TLS dan hanya untuk demo lokal.
