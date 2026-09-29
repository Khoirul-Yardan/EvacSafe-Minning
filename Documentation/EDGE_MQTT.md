# Getaran, edge virtual, dan MQTT — implementasi Orang 1

## Pembaruan planner edge 29 September 2026

Pada `MqttEdgeSimulation`, klasifikasi bahaya **dan pencarian rute** kini ditempatkan
di proses Python terpisah. Unity mengirim snapshot graf, lokasi detektor, posisi
agen, exit, sel tertutup, sel waspada, serta penalti melalui
`safe-mining/v1/{sessionId}/navigation/request`. Python (`Tools/Edge/edge_routing.py`)
menjalankan Dijkstra dengan biaya langkah 6 dan penalti peringatan, lalu mengirim
jalur lewat `safe-mining/v1/{sessionId}/navigation/response` (QoS 1, tanpa retain).
Request dan response memakai `sequence`, `stateRevision`, `sessionId`, dan
`layoutId`. Python memeriksa hash graf/detektor; Unity memeriksa korelasi,
kontiguitas jalur, exit, biaya, sel tertutup, serta posisi dan revisi bahaya terkini.
Pemeriksaan Unity ini tidak menjalankan ulang pencarian jalur terpendek.

Rute awal adaptif maupun statis dihitung di edge. Adaptif meminta rute baru saat
status bahaya berubah atau agen FPP berpindah sel; statis mempertahankan rute
awal setelah diterapkan. Gerak dan petunjuk ditahan saat menunggu rute terbaru.
Timeout navigasi lima detik waktu nyata di luar jeda dihentikan sebagai
`navigation_timeout`, bukan `no_safe_route`. Tidak ada fallback otomatis ke
planner Unity. `LocalEdgeSimulation` (termasuk latihan terpandu) dan
`LegacyTimeline` tetap menggunakan planner lokal dan **bukan bukti planner edge**.

Bangun ulang layanan agar modul baru masuk container:

```sh
docker compose --profile simulated-edge -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d --build
```

Pilih **MQTT edge** pada menu, lalu jalankan Cerita/FPP. Ekspor sebelum mengganti
sesi. Ekspor menambahkan `_navigation.jsonl`, metadata lokasi planner, alasan
terminasi, jumlah hasil diterapkan, serta flag kegagalan, jeda, dan disconnect.
Jejak navigasi juga ditautkan ke `_edge.jsonl` menggunakan id request dan revisi.

Arti pengukuran baru:

| Kolom/ukuran | Batas pengukuran |
|---|---|
| `max_planning_ms` | Maksimum waktu pencarian Python pada respons perubahan bahaya yang diterapkan; untuk sumber lokal tetap durasi fungsi Plan Unity |
| `max_request_to_route_ms` | Waktu nyata dari enqueue request terbaru sampai hasil diterapkan di Unity; mencakup broker, edge, antrean main thread, dan jeda jika ada |
| `max_hazard_to_route_ms` | Waktu nyata dari perubahan status di Unity sampai penerapan rute; jika perubahan bertumpuk, dimulai dari perubahan pertama yang belum terlayani |
| `terminal_reason` | Membedakan mencapai exit, jalur tidak tersedia, jalur statis terhalang, longsor di posisi agen, dan kegagalan navigasi/konfigurasi |

Ketiga waktu tersebut **bukan latensi sensor fisik sampai tampilan**. Pencarian
Python memakai `perf_counter_ns`; durasi aplikasi memakai Stopwatch di Unity,
tanpa mengurangkan timestamp dari dua proses. Sesi yang dijeda/terputus perlu
dipisahkan dari benchmark normal. `max_planning_ms = 0` pada statis tidak berarti
komputasi rute awal nol: kolom itu khusus komputasi ulang akibat bahaya; respons
rute awal beserta durasinya tersedia di `_navigation.jsonl`.

**Status bukti:** perubahan planner ini belum mempunyai batch evaluasi baru.
Data 30 pasangan seed 101–130 terdahulu berasal dari `LegacyTimeline` dan planner
C# sebelum perubahan; angka 29/30 dan 0,0585 ms tidak boleh dilabel ulang sebagai
hasil Python/MQTT. Bagian validasi dan trace historis di bawah mendokumentasikan
versi klasifikasi edge sebelumnya, bukan verifikasi planner Python baru.

### Penyelarasan naskah

Metode untuk versi MQTT baru dapat menyatakan: “Sampel virtual diproses oleh
layanan Python untuk klasifikasi status bahaya. Unity meneruskan snapshot graf,
status bahaya, dan posisi agen ke layanan tersebut untuk perhitungan rute.
Hasil perencanaan dikirim melalui MQTT dan diterapkan setelah pemeriksaan
identitas sesi, revisi kondisi, serta kelayakan lintasan.” Klaim ini menjelaskan
implementasi kode; efektivitas dan angka kinerja tetap memerlukan data baru.

`[PLACEHOLDER GAMBAR: screenshot Unity dengan sumber MQTT edge, seed, waktu,
status detektor, dan rute dari Python. PLACEHOLDER HASIL: evaluasi berpasangan
melalui sumber MQTT, dipisahkan dari batch LegacyTimeline terdahulu.]`

25 September 2026. Scene: `Assets/Scenes/SafeMining_Experience.unity`.

Alur aktif adalah **getaran lingkungan → pembacaan sensor virtual → keputusan edge → broker MQTT → subscriber Unity → status lorong → navigasi**. Tidak ada trigger spawn point, jarak pemain, atau collider pemain untuk menyalakan sensor. Titik awal pekerja dan lokasi pemasangan detektor tetap diperlukan sebagai geometri. Jadwal skenario menentukan profil **input getaran**, bukan langsung menutup lorong pada mode edge.

Implementasi 25 September mencakup Orang 1: kontrak, generator, keputusan edge, transport, integrasi, lifecycle, ekspor, pengujian, dan konfigurasi scene. Pembaruan 27 September menambahkan HUD alur dari `feat/irawan` serta panel kamera longsor di atas dialog untuk Cerita/FPP. Panel membaca status lorong yang sudah diterapkan, tanpa mengubah kontrak atau jalur penerapan edge. Lihat [audit dan panduan cutscene](UPDATE_IRAWAN_CUTSCENE_2026-09-27.md).

## Menjalankan

1. Dari folder proyek, jalankan broker lokal dengan Docker Desktop aktif:

   ```powershell
   docker compose --profile simulated-edge -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d --build
   ```

2. Buka scene utama. Pada komponen `MiningSimulation`, `Hazard Source` bawaan adalah **MqttEdgeSimulation**, host `127.0.0.1`, port `1883`. Parameter disalin saat Begin; perubahan Inspector saat sesi berjalan berlaku pada sesi berikutnya.
3. Tekan Play lalu mulai Cerita atau FPP. HUD detektor yang sudah ada menampilkan sumber/koneksi dan status bahaya terakhir. MQTT baru dibuka ketika sesi dimulai.
4. Untuk pembuktian sederhana pilih skenario **Scripted** sebelum memulai. Tetap diam di FPP: D01 mulai menerima profil warning pada detik 6 dan danger pada detik 10. Keputusan terjadi setelah 0,5 detik sampel yang memenuhi ambang, lalu diterapkan setelah diterima dari broker. Waktu ini merupakan waktu simulasi.
5. Pilih **NoHazards** dan berdiri di dekat detektor: kedekatan tidak menimbulkan alarm.
6. Gunakan Esc untuk pause; R untuk reset sesi dan ulang seed. Ekspor melalui menu jeda/hasil. Matikan broker setelah selesai:

   ```powershell
   docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml down
   ```

Pilihan sumber saling eksklusif:

| Sumber | Jalur penerapan |
|---|---|
| `MqttEdgeSimulation` (bawaan) | Sensor Unity → sampel MQTT QoS 1 → edge Python di container terpisah → status MQTT → validasi Unity → penerapan |
| `LocalEdgeSimulation` | Edge → validasi → penerapan lokal; HUD menyatakan tanpa MQTT |
| `LegacyTimeline` | Jadwal lama langsung memperbarui bahaya; perintah WebSocket lama hanya di sini |

Cerita/FPP dan adaptif/statis tetap terpisah dari pilihan sumber. `SetHazard()` publik tidak dapat melewati pipeline pada kedua mode edge. WebSocket tetap bisa mengirim telemetri, tetapi perintah bahayanya diabaikan di mode edge. Tidak ada fallback otomatis ke lokal jika MQTT gagal.

## Pembacaan dan keputusan

`MiningVibrationGenerator` menghasilkan sampel deterministik dari seed, indeks perangkat, indeks sampel, dan profil skenario. Sampel berinterval tetap 0,05 detik; hasil keputusan tidak mengikuti FPS atau posisi pekerja. Nilai adalah **intensitas simulasi 0..1**, bukan percepatan fisik.

| Parameter bawaan | Nilai |
|---|---|
| Profil normal / warning / danger | 0,12 / 0,58 / 0,90 |
| Noise deterministik | ±0,04 |
| Ambang warning / danger | ≥0,45 / ≥0,75 |
| Durasi minimum warning/danger | 0,5 detik berturut-turut |
| Ambang turun / durasi stabil | ≤0,30 selama 1 detik |
| Panjang pulsa danger | 2 detik |

Saat pulsa berakhir getaran kembali normal, tetapi status `2` tetap tertutup sampai reset sesi. Status `0 = normal`, `1 = waspada`, `2 = tertutup`. Memetakan danger menjadi longsor merupakan aturan demonstrasi. Parameter divalidasi sebelum sesi; konfigurasi tidak valid menghentikan sesi dengan pesan kesalahan.

## Kontrak dan transport

Sampel masuk ke edge memakai topik `safe-mining/v1/{sessionId}/sensor/{deviceId}/sample`:

```json
{
  "schemaVersion": 1,
  "sessionId": "id-sesi-unik",
  "layoutId": "hash-SHA256-sel-dan-urutan-detektor",
  "deviceId": "D01",
  "source": "unity-sensor-simulation",
  "sequence": 17,
  "simulationTimeS": 0.85,
  "vibrationNormalized": 0.58,
  "warningThreshold": 0.45,
  "dangerThreshold": 0.75,
  "clearThreshold": 0.30,
  "minimumDurationS": 0.5,
  "clearDurationS": 1.0
}
```

Edge menerbitkan hasil ke topik `safe-mining/v1/{sessionId}/edge/{deviceId}/status`. Field wajib:

```json
{
  "schemaVersion": 1,
  "sessionId": "id-sesi-unik",
  "layoutId": "hash-SHA256-sel-dan-urutan-detektor",
  "eventId": "id-sesi-unik-D01-17",
  "deviceId": "D01",
  "sequence": 17,
  "simulationTimeS": 6.5,
  "vibrationNormalized": 0.58,
  "level": 1,
  "source": "hardware-edge"
}
```

`source` harus `python-edge` atau `hardware-edge` untuk hasil MQTT. `sessionId` baru setiap Begin/reset. Sequence meningkat per perangkat; topik, eventId, dan payload harus cocok. Subscriber menolak JSON rusak, field hilang/tambahan/duplikat, tipe atau nilai tidak valid, perangkat/denah/sesi asing, waktu masa depan atau mundur, sequence lama/duplikat, event retained, serta pembukaan kembali status tertutup. Event UI dan penerapan hanya berjalan di main thread.

Adapter TCP Unity mengimplementasikan bagian [MQTT 3.1.1](https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/os/mqtt-v3.1.1-os.html) yang diperlukan demo: clean session, publish/subscribe QoS 1, PUBACK, retransmisi DUP, keepalive, dan reconnect. Unity tidak memutuskan bahaya pada mode MQTT: sampel mengalir ke `Tools/Edge/edge_service.py`, yang mengolah ambang, durasi, hysteresis, dan latch longsor, lalu menerbitkan status saat berubah serta heartbeat sekitar satu detik simulasi. Unity merekam sampel dan antrean lokal; layanan Python dan broker memiliki log sendiri. Saat reconnect Unity mengirim ulang sampel terakhir setiap sensor. Status berulang tidak mengulang sirene/reroute bila level sudah sama.

Broker Docker memakai Mosquitto 2.0.22; port host hanya terikat ke loopback. Adapter ini untuk demonstrasi TCP lokal tanpa TLS/autentikasi, bukan klien MQTT umum. Tidak menambahkan dependency Unity atau mengubah manifest paket. WebGL tidak mendukung jalur socket TCP ini; target yang diuji dicatat pada hasil validasi.

## Pause, penghentian, dan kualitas data

- Pause menghentikan clock/generator/penerapan. Worker MQTT tetap melayani koneksi dan mengantre pesan sampai resume.
- Antrean masuk/keluar dibatasi 256, notice jaringan 1024, pesan jaringan 16 KiB, payload JSON 8 KiB. Jika antrean meluap, `DataLoss` mengunci penerapan dan HUD meminta ulang sesi.
- Saat putus, sampel sensor antrean sesi dapat hilang dan status tidak diterapkan; tidak ada fallback keputusan lokal. Status lorong terakhir bertahan. `DataStale` menandai koneksi yang belum mengirim status untuk semua detektor atau tidak ada penerimaan selama lebih dari 3 detik simulasi. `HadDisconnect` mencatat gangguan sepanjang sesi.
- Menu/hasil akhir/disable/destroy menghentikan transport; antrean sesi lama tidak dipakai sesi baru. Tidak ada pemulihan otomatis longsor.
- Trace dibatasi 100.000 entri untuk demo. Mencapai batas ini menandai kehilangan data dan menghentikan pipeline; reset untuk eksperimen berikutnya. Dengan 12 sensor pada 20 Hz, batas dapat tercapai sekitar beberapa menit.

## Event dan ekspor

`MiningSimulation.EdgeSession` menyediakan `VibrationSampled`, `EdgeStateChanged`, `TransportStateChanged`, dan `HazardApplied`. Ini adalah penghubung data yang siap dipakai visual lanjutan; handler harus memperlakukan payload sebagai data baca saja. Instance diganti setiap Begin, sehingga pelanggan event perlu melepas instance lama dan berlangganan ulang.

Ekspor lama tetap tersedia. `_config.json` ditambah sumber aktif, identitas sesi/denah, arti jadwal (`vibration_profile_onsets` untuk edge), parameter edge/broker, dan flag kualitas data. Kolom sumber/sesi/kualitas juga ditambahkan di akhir `_summary.csv`. `_edge.jsonl` mencatat sampel dan antrean publish Unity, status edge yang diterima, penerapan, route, penolakan, serta gangguan koneksi. `decision` adalah tahap edge lokal; jejak keputusan Python tersedia dari pesan status di broker dan log container. Event status memakai `eventId` sendiri yang meningkat per sensor; `simulationTimeS` menghubungkannya ke sampel.

`simulationTimeS` adalah clock eksperimen; `monotonicMs` mengukur waktu lokal sesi, termasuk pause. Worker dan main thread memakai Stopwatch yang sama; urutkan menurut monotonicMs jika menganalisis transport karena beberapa antrean dicatat pada frame yang sama. Rentang `sample_publish_queued` → `receive` mencakup edge Python dan jaringan/broker pada setup lokal; itu bukan latensi sensor fisik. Hasil dengan DataLoss atau HadDisconnect harus dipisahkan dari eksperimen normal.

## Validasi

Runner `MiningEdgeValidation.RunBatch` memeriksa replay 32 seed, independensi FPS, spike/noise, hysteresis/latch, kontrak, Cerita/FPP, pemain diam/jarak, pause/reset, adaptif/statis, Mosquitto dan edge Python nyata, pesan rusak/duplikat, reconnect, dan broker tidak tersedia. Jalankan pada salinan proyek, dengan broker serta edge Python aktif:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -batchmode -nographics -projectPath '<salinan-proyek>' -executeMethod MiningEdgeValidation.RunBatch -logFile '<log-validasi>'
```

Hasil runner berada di `Validation/edge-results.txt`; trace integrasi di `Validation/mqtt-session_edge.jsonl`. Runner baseline `MiningExperienceValidation.RunBatch` secara eksplisit memilih LegacyTimeline. Lihat [bukti validasi edge/MQTT](Validation/edge-mqtt.txt), [satu trace longsor lengkap](Validation/edge-mqtt-trace.jsonl), dan [hasil regresi baseline](Validation/edge-baseline.txt). Uji batas antrean juga membuktikan sesi kehilangan data tidak menerapkan pesan saat dilanjutkan.

Sensor dan profil getaran masih simulasi di Unity; keputusan edge Python berjalan di proses/container terpisah melalui MQTT. Untuk memakai perangkat edge fisik di komputer lain, jalankan broker saja dengan `MQTT_BIND_ADDRESS=0.0.0.0`, set host MQTT Unity ke `127.0.0.1`, lalu hubungkan edge ke IP LAN komputer host. Terapkan kontrak sampel/status di atas dan jangan jalankan `python-edge` bersama perangkat yang menerbitkan status untuk sesi yang sama. Broker contoh tanpa autentikasi/TLS harus dibatasi lewat firewall. Ini belum memvalidasi sensor terkalibrasi atau keselamatan tambang.
