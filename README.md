# SAFE-MINING EVAC

Simulasi evakuasi tambang Unity dalam **Mode Cerita otomatis**, dengan pembanding navigasi **adaptif** dan **statis**. Pembaruan: 23 September 2026.

1. Buka `Assets/Scenes/SafeMining_Experience.unity` di Unity 6000.3.23f1.
2. Tekan **Play**, pilih navigasi adaptif atau statis, lalu **Mulai Mode Cerita**.
3. **Esc** untuk jeda/lanjut; **R** untuk mengulang sesi. Pekerja bergerak otomatis.

Tunnel dibangun secara **prosedural deterministik**. Denah kini dapat diubah melalui daftar **Corridors** pada komponen `MiningSimulation` sebelum Play. Status longsor dan rute adaptif berubah saat simulasi berjalan. Topologi tidak diacak atau dibangun ulang di tengah sesi.

- [Panduan pengaturan tunnel, simulasi cerita, dan evaluasi](Documentation/SAFE_MINING.md)
- [Update terbaru: audit procedural/dinamis dan cara menjawab research problem](Documentation/UPDATE_RESEARCH_2026-09-23.md)

Untuk melihat denah tanpa Play, pilih **EDITOR PREVIEW** di Hierarchy. Setelah mengubah koridor, jalankan **SafeMining > Documentation > Rebuild Editor Preview** dan simpan scene. Gunakan kamera seluruh map atau kamera cerita untuk dokumentasi.

Scene `SafeMiningEvac_Demo.unity` adalah demo lama dengan alur berbeda. Gunakan `SafeMining_Experience.unity` untuk penelitian terbaru. Gambar menu/FPP serta log validasi lama dalam `Documentation` merupakan arsip sebelum pembatasan Mode Cerita; lihat dokumen update untuk bukti validasi terbaru.
