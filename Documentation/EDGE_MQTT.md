# Getaran, edge virtual, dan MQTT — implementasi Orang 1

25 September 2026. Scene: `Assets/Scenes/SafeMining_Experience.unity`.

Alur aktif adalah **getaran lingkungan → pembacaan sensor virtual → keputusan edge → broker MQTT → subscriber Unity → status lorong → navigasi**. Tidak ada trigger spawn point, jarak pemain, atau collider pemain untuk menyalakan sensor. Titik awal pekerja dan lokasi pemasangan detektor tetap diperlukan sebagai geometri. Jadwal skenario menentukan profil **input getaran**, bukan langsung menutup lorong pada mode edge.

Pekerjaan ini mencakup Orang 1: kontrak, generator, keputusan edge, transport, integrasi, lifecycle, ekspor, pengujian, dan konfigurasi scene. Panel cutscene, kamera close-up, animasi perangkat, serta prefab visual Orang 2 tidak termasuk.

## Menjalankan

1. Dari folder proyek, jalankan broker lokal dengan Docker Desktop aktif:

   ```powershell
   docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d
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
| `MqttEdgeSimulation` (bawaan) | Edge → publish QoS 1 → Mosquitto → subscribe → validasi → penerapan |
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

Topik: `safe-mining/v1/{sessionId}/edge/{deviceId}/status`. Field wajib:

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
  "source": "virtual-edge"
}
```

`sessionId` baru setiap Begin/reset. Sequence meningkat per perangkat; topik, eventId, dan payload harus cocok. Subscriber menolak JSON rusak, field hilang/tambahan/duplikat, tipe atau nilai tidak valid, perangkat/denah/sesi asing, waktu masa depan atau mundur, sequence lama/duplikat, event retained, serta pembukaan kembali status tertutup. Event UI dan penerapan hanya berjalan di main thread.

Adapter TCP mengimplementasikan bagian [MQTT 3.1.1](https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/os/mqtt-v3.1.1-os.html) yang diperlukan demo: clean session, publish/subscribe QoS 1, PUBACK, retransmisi DUP, keepalive, dan reconnect. Status dipublikasikan saat berubah dan sebagai snapshot setiap sekitar satu detik simulasi; sesudah subscribe ulang, snapshot dikirim kembali dengan sequence baru. ACK broker dicatat terpisah dari penerimaan dan penerapan Unity. Snapshot tidak mengulang sirene/reroute bila level sudah sama.

Broker Docker memakai Mosquitto 2.0.22; port host hanya terikat ke loopback. Adapter ini untuk demonstrasi TCP lokal tanpa TLS/autentikasi, bukan klien MQTT umum. Tidak menambahkan dependency Unity atau mengubah manifest paket. WebGL tidak mendukung jalur socket TCP ini; target yang diuji dicatat pada hasil validasi.

## Pause, penghentian, dan kualitas data

- Pause menghentikan clock/generator/penerapan. Worker MQTT tetap melayani koneksi dan mengantre pesan sampai resume.
- Antrean masuk/keluar dibatasi 256, notice jaringan 1024, pesan jaringan 16 KiB, payload JSON 8 KiB. Jika antrean meluap, `DataLoss` mengunci penerapan dan HUD meminta ulang sesi.
- Saat putus, keputusan lokal tetap dihitung tetapi belum diterapkan. Status lorong terakhir bertahan. `DataStale` menandai koneksi yang belum mengirim snapshot lengkap atau tidak ada penerimaan selama lebih dari 3 detik simulasi. `HadDisconnect` mencatat gangguan sepanjang sesi.
- Menu/hasil akhir/disable/destroy menghentikan transport; antrean sesi lama tidak dipakai sesi baru. Tidak ada pemulihan otomatis longsor.
- Trace dibatasi 100.000 entri untuk demo. Mencapai batas ini menandai kehilangan data dan menghentikan pipeline; reset untuk eksperimen berikutnya. Dengan 12 sensor pada 20 Hz, batas dapat tercapai sekitar beberapa menit.

## Event dan ekspor

`MiningSimulation.EdgeSession` menyediakan `VibrationSampled`, `EdgeStateChanged`, `TransportStateChanged`, dan `HazardApplied`. Ini adalah penghubung data yang siap dipakai visual lanjutan; handler harus memperlakukan payload sebagai data baca saja. Instance diganti setiap Begin, sehingga pelanggan event perlu melepas instance lama dan berlangganan ulang.

Ekspor lama tetap tersedia. `_config.json` ditambah sumber aktif, identitas sesi/denah, arti jadwal (`vibration_profile_onsets` untuk edge), parameter edge/broker, dan flag kualitas data. Kolom sumber/sesi/kualitas juga ditambahkan di akhir `_summary.csv`. `_edge.jsonl` mencatat `sample`, `decision`, `publish_queued`, `publish_wire`, `puback`, `receive`, `apply`, `route`, penolakan, dan gangguan koneksi. Keputusan, sampel pemicu, pengiriman, penerimaan, penerapan, dan route memakai eventId yang sama. Snapshot mempunyai sequence/eventId baru.

`simulationTimeS` adalah clock eksperimen; `monotonicMs` mengukur waktu lokal sesi, termasuk pause. Worker dan main thread memakai Stopwatch yang sama; urutkan menurut monotonicMs jika menganalisis transport karena beberapa antrean dicatat pada frame yang sama. Selisih publish_wire → receive mengukur putaran broker lokal, bukan latensi perangkat fisik. Hasil dengan DataLoss atau HadDisconnect harus dipisahkan dari eksperimen normal.

## Validasi

Runner `MiningEdgeValidation.RunBatch` memeriksa replay 32 seed, independensi FPS, spike/noise, hysteresis/latch, kontrak, Cerita/FPP, pemain diam/jarak, pause/reset, adaptif/statis, Mosquitto nyata, pesan rusak/duplikat, reconnect, dan broker tidak tersedia. Jalankan pada salinan proyek, dengan broker aktif:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -batchmode -nographics -projectPath '<salinan-proyek>' -executeMethod MiningEdgeValidation.RunBatch -logFile '<log-validasi>'
```

Hasil runner berada di `Validation/edge-results.txt`; trace integrasi di `Validation/mqtt-session_edge.jsonl`. Runner baseline `MiningExperienceValidation.RunBatch` secara eksplisit memilih LegacyTimeline. Lihat [bukti validasi edge/MQTT](Validation/edge-mqtt.txt), [satu trace longsor lengkap](Validation/edge-mqtt-trace.jsonl), dan [hasil regresi baseline](Validation/edge-baseline.txt). Uji batas antrean juga membuktikan sesi kehilangan data tidak menerapkan pesan saat dilanjutkan.

Edge dan sensor masih virtual dalam proses Unity. Broker MQTT berjalan sebagai proses terpisah. Ini bukan deployment perangkat edge fisik, pemrosesan Python, kalibrasi sensor, atau bukti keselamatan tambang.
