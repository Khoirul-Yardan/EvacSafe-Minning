# Implementasi Orang 1: getaran, edge, MQTT

Lingkup 25 September 2026 mengikuti permintaan saat itu: kerjakan Orang 1 saja. Pada 27 September, lingkup diperluas untuk menggabungkan HUD `feat/irawan` dan panel cutscene longsor pada Cerita/FPP. Lihat [catatan integrasi terbaru](UPDATE_IRAWAN_CUTSCENE_2026-09-27.md). Tabel berikut tetap mencatat pekerjaan inti Orang 1.

Konsep: **getaran lingkungan virtual → sensor/edge → MQTT → penerapan bahaya → navigasi**. Aktivasi tidak menggunakan spawn point atau kedekatan pekerja. Lokasi awal pekerja dan titik pemasangan sensor hanya bagian denah.

| ID | Hasil implementasi |
|---|---|
| A1 | Kontrak `EdgeStatusMessage`, sumber bahaya eksklusif, session/device/layout/event ID, empat event penghubung data |
| A2 | Generator deterministik berinterval tetap, profil normal/warning/danger, parameter Inspector, replay seed |
| A3 | Ambang, durasi minimum, hysteresis, durasi stabil, status tertutup terkunci sampai reset |
| A4 | Adapter MQTT 3.1.1 TCP lokal, QoS 1, broker Mosquitto, subscribe/publish, koneksi ulang |
| A5 | Satu jalur penerapan internal pada MiningSimulation; timeline dan perintah WebSocket hanya pada LegacyTimeline |
| A6 | Pause, reset identitas, validasi field/urutan, antrean terbatas, penanda data basi/hilang, penghentian sesi |
| A7 | Ekspor trace getaran–keputusan–publish–receive–apply–route, konfigurasi eksperimen, runner pengujian |

Kode inti berada di `Assets/Scripts/Edge/` dan `Assets/Scripts/Networking/`. Integrasi ada pada `MiningSimulation.cs`, pembatasan perintah lama pada `MiningTelemetry.cs`, dan konfigurasi default MQTT pada scene utama. `MineLayout`, planner, gerakan, prefab detektor, panel HUD, serta scene demo lama tetap memakai implementasi yang ada.

Ikuti [panduan lengkap dan kontrak](EDGE_MQTT.md) untuk menjalankan broker, memilih sumber, memakai event, dan membaca hasil ekspor. [Catatan validasi](Validation/edge-mqtt.txt) menjelaskan pengujian yang benar-benar dijalankan dan batas buktinya.
