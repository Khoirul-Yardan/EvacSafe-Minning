# SAFE-MINING EVAC

Simulasi evakuasi tambang Unity dengan **Mode Cerita otomatis** dan **Mode FPP manual**, getaran virtual, keputusan edge, MQTT, serta navigasi **adaptif** dan **statis**. Pembaruan: 27 September 2026.

[Deskripsi lengkap simulasi dan perbedaan sebelum/sesudah MQTT](Documentation/Deskripsi%20lengkap.md).

Alur bawaan: **sensor getaran virtual di Unity → broker MQTT → edge Python (container terpisah) → broker MQTT → Unity → bahaya dan navigasi**. Sensor dan getaran tetap simulasi; planner rute tetap C# di Unity. Sensor tidak dipicu spawn point atau kedekatan pemain. HUD menampilkan alur deteksi dan panel kamera longsor kecil di atas dialog pada FPP maupun Cerita. [Panduan getaran, edge, MQTT, dan pengujian](Documentation/EDGE_MQTT.md).

Panel cutscene muncul saat lorong benar-benar tertutup, menampilkan lokasi perangkat selama lima detik, dan mengantre beberapa lokasi secara bergantian. Kamera utama dan kontrol tetap berjalan. [Integrasi feat/irawan, audit dokumentasi, dan panduan cutscene](Documentation/UPDATE_IRAWAN_CUTSCENE_2026-09-27.md).

Sebelum memulai mode MQTT, jalankan broker Mosquitto **dan** edge Python dari root proyek (Docker Desktop harus aktif):

```powershell
docker compose --profile simulated-edge -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d --build
```

Pastikan dua layanan berjalan, `broker` dan `python-edge`:

```powershell
docker compose --profile simulated-edge -p safe-mining-edge -f Tools/Mqtt/compose.yaml ps
```

Tanpa `--profile simulated-edge` hanya broker yang menyala. Unity tetap mengirim sampel getaran, tetapi tidak ada edge yang mengirim status balik, sehingga peringatan dan longsor **tidak pernah diterapkan**. Build pertama mengunduh image Python dan `paho-mqtt`, jadi jalankan sekali saat ada internet sebelum demo. Hentikan setelah selesai:

```powershell
docker compose --profile simulated-edge -p safe-mining-edge -f Tools/Mqtt/compose.yaml down
```

Tanpa Docker, pilih sumber **Edge lokal** di menu simulasi sebelum mulai (setara `LocalEdgeSimulation` pada `MiningSimulation > Hazard Source`). Mode MQTT tidak berpindah otomatis ke lokal ketika terputus. **Jadwal pembanding** (`LegacyTimeline`) tersedia untuk membandingkan dengan jadwal lama.

1. Buka `Assets/Scenes/SafeMining_Experience.unity` di Unity 6000.3.23f1.
2. Tekan **Play**, pilih navigasi dan skenario, lalu **Mulai Mode Cerita** atau **Mulai Mode FPP**.
3. FPP: **WASD** bergerak, **mouse** melihat, **Shift** berlari, **F** lampu helm, **G** kacamata navigasi. Cerita: pekerja bergerak otomatis.
4. **Esc** untuk jeda/lanjut dan mengatur sensitivitas mouse, FOV, serta ayunan kamera. **R** mengulang mode dan seed yang sama. Pilih **Acak skenario baru** di menu untuk kejadian lain.

Tampilan tambang menggunakan material batu, kerikil, kayu, dan besi dengan normal map serta variasi kekasaran, lampu kerja hangat, lampu helm berbayang, dan warna HUD yang lebih netral. Pengaturan kamera dan kecepatan FPP tersedia di Inspector `MiningSimulation`. Lihat [panduan FPP dan visual](Documentation/FPP_AND_VISUALS.md).

Tunnel dibangun secara **prosedural deterministik**. Denah kini dapat diubah melalui daftar **Corridors** pada komponen `MiningSimulation` sebelum Play. Status longsor dan rute adaptif berubah saat simulasi berjalan. Topologi tidak diacak atau dibangun ulang di tengah sesi.

- [Kesesuaian full paper, longsor acak, detektor, dan protokol eksperimen terbaru](Documentation/PAPER_ALIGNMENT_AND_RANDOM_HAZARDS.md)
- [Panduan pengaturan tunnel, simulasi cerita, dan evaluasi](Documentation/SAFE_MINING.md)
- [Update terbaru: audit procedural/dinamis dan cara menjawab research problem](Documentation/UPDATE_RESEARCH_2026-09-23.md)
- [Alur kerja tim, CI, dan aturan branch](Documentation/DEVELOPMENT_WORKFLOW.md)
- [Desain ulang UI: warna, huruf, ikon, menu, HUD, dan panel hasil](Documentation/UI_REDESIGN_2026-09-29.md)

Untuk melihat denah tanpa Play, pilih **EDITOR PREVIEW** di Hierarchy. Setelah mengubah koridor, jalankan **SafeMining > Documentation > Rebuild Editor Preview** dan simpan scene. Gunakan kamera seluruh map atau kamera cerita untuk dokumentasi.

Aset alat deteksi: `Assets/Resources/Mining/LandslideDetector.prefab`, dipasang hingga 12 titik pada denah bawaan. Hijau = normal, kuning = waspada, merah = tertutup; peringatan juga muncul di display perangkat, sirene lokal, HUD, dan minimap. Planner adaptif memperhitungkan penalti risiko saat waspada.

[Hasil validasi longsor acak dan detektor](Documentation/Validation/random-detectors.txt)

Scene `SafeMiningEvac_Demo.unity` adalah demo lama dengan alur berbeda. Gunakan `SafeMining_Experience.unity`. Dokumen audit 23 September menggambarkan versi Cerita saat itu; FPP kini tersedia untuk latihan manual. Hasil FPP dan Cerita dibedakan dalam ekspor evaluasi.
