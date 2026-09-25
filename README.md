# SAFE-MINING EVAC

Simulasi evakuasi tambang Unity dengan **Mode Cerita otomatis** dan **Mode FPP manual**, longsor acak, jaringan detektor visual, serta navigasi **adaptif** dan **statis**. Pembaruan: 24 September 2026.

1. Buka `Assets/Scenes/SafeMining_Experience.unity` di Unity 6000.3.23f1.
2. Tekan **Play**, pilih navigasi dan skenario, lalu **Mulai Mode Cerita** atau **Mulai Mode FPP**.
3. FPP: **WASD** bergerak, **mouse** melihat, **Shift** berlari, **F** lampu helm, **G** kacamata navigasi. Cerita: pekerja bergerak otomatis.
4. **Esc** untuk jeda/lanjut dan mengatur sensitivitas mouse, FOV, serta ayunan kamera. **R** mengulang mode dan seed yang sama. Pilih **Acak skenario baru** di menu untuk kejadian lain.

Tampilan tambang menggunakan material batu, kerikil, kayu, dan besi dengan normal map serta variasi kekasaran, lampu kerja hangat, lampu helm berbayang, dan warna HUD yang lebih netral. Pengaturan kamera dan kecepatan FPP tersedia di Inspector `MiningSimulation`. Lihat [panduan FPP dan visual](Documentation/FPP_AND_VISUALS.md).

Tunnel dibangun secara **prosedural deterministik**. Denah kini dapat diubah melalui daftar **Corridors** pada komponen `MiningSimulation` sebelum Play. Status longsor dan rute adaptif berubah saat simulasi berjalan. Topologi tidak diacak atau dibangun ulang di tengah sesi.

- [Kesesuaian full paper, longsor acak, detektor, dan protokol eksperimen terbaru](Documentation/PAPER_ALIGNMENT_AND_RANDOM_HAZARDS.md)
- [Panduan pengaturan tunnel, simulasi cerita, dan evaluasi](Documentation/SAFE_MINING.md)
- [Update terbaru: audit procedural/dinamis dan cara menjawab research problem](Documentation/UPDATE_RESEARCH_2026-09-23.md)

Untuk melihat denah tanpa Play, pilih **EDITOR PREVIEW** di Hierarchy. Setelah mengubah koridor, jalankan **SafeMining > Documentation > Rebuild Editor Preview** dan simpan scene. Gunakan kamera seluruh map atau kamera cerita untuk dokumentasi.

Aset alat deteksi: `Assets/Resources/Mining/LandslideDetector.prefab`, dipasang hingga 12 titik pada denah bawaan. Hijau = normal, kuning = waspada, merah = tertutup; peringatan juga muncul di display perangkat, sirene lokal, HUD, dan minimap. Planner adaptif memperhitungkan penalti risiko saat waspada.

[Hasil validasi longsor acak dan detektor](Documentation/Validation/random-detectors.txt)

Scene `SafeMiningEvac_Demo.unity` adalah demo lama dengan alur berbeda. Gunakan `SafeMining_Experience.unity`. Dokumen audit 23 September menggambarkan versi Cerita saat itu; FPP kini tersedia untuk latihan manual. Hasil FPP dan Cerita dibedakan dalam ekspor evaluasi.
