# SAFE-MINING EVAC

Simulasi evakuasi tambang Unity dalam **Mode Cerita otomatis**, dengan longsor acak, jaringan detektor visual, dan pembanding navigasi **adaptif** dan **statis**. Pembaruan: 23 September 2026.

1. Buka `Assets/Scenes/SafeMining_Experience.unity` di Unity 6000.3.23f1.
2. Tekan **Play**, pilih navigasi adaptif atau statis dan jenis skenario, lalu **Mulai Mode Cerita**.
3. **Esc** untuk jeda/lanjut; **R** untuk mengulang seed yang sama. Pilih **Acak skenario baru** di menu untuk kejadian lain. Pekerja bergerak otomatis.

Tunnel dibangun secara **prosedural deterministik**. Denah kini dapat diubah melalui daftar **Corridors** pada komponen `MiningSimulation` sebelum Play. Status longsor dan rute adaptif berubah saat simulasi berjalan. Topologi tidak diacak atau dibangun ulang di tengah sesi.

- [Kesesuaian full paper, longsor acak, detektor, dan protokol eksperimen terbaru](Documentation/PAPER_ALIGNMENT_AND_RANDOM_HAZARDS.md)
- [Panduan pengaturan tunnel, simulasi cerita, dan evaluasi](Documentation/SAFE_MINING.md)
- [Update terbaru: audit procedural/dinamis dan cara menjawab research problem](Documentation/UPDATE_RESEARCH_2026-09-23.md)

Untuk melihat denah tanpa Play, pilih **EDITOR PREVIEW** di Hierarchy. Setelah mengubah koridor, jalankan **SafeMining > Documentation > Rebuild Editor Preview** dan simpan scene. Gunakan kamera seluruh map atau kamera cerita untuk dokumentasi.

Aset alat deteksi: `Assets/Resources/Mining/LandslideDetector.prefab`, dipasang hingga 12 titik pada denah bawaan. Hijau = normal, kuning = waspada, merah = tertutup; peringatan juga muncul di display perangkat, sirene lokal, HUD, dan minimap. Planner adaptif memperhitungkan penalti risiko saat waspada.

[Hasil validasi longsor acak dan detektor](Documentation/Validation/random-detectors.txt)

Scene `SafeMiningEvac_Demo.unity` adalah demo lama dengan alur berbeda. Gunakan `SafeMining_Experience.unity` untuk penelitian terbaru. Gambar FPP dan log validasi lama merupakan arsip. Gambar terbaru tersedia di `Documentation/Previews/random-menu.png`, `detector-warning.png`, dan `random-story.png`.
