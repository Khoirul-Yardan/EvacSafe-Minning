# FPP dan tampilan tambang — 24 September 2026

Buka `Assets/Scenes/SafeMining_Experience.unity`, tekan Play, lalu pilih **Mulai Mode FPP**. Mode Cerita tetap tersedia untuk evakuasi otomatis. Pilihan navigasi adaptif/statis dan skenario berlaku untuk kedua mode.

| Kontrol FPP | Fungsi |
|---|---|
| WASD | Bergerak, termasuk langkah menyamping |
| Mouse | Melihat; sudut vertikal dibatasi agar kamera tidak terbalik |
| Shift | Berlari |
| F | Lampu helm hidup/mati |
| G | Kacamata AR hidup/mati: petunjuk arah, panah lantai, dan rute minimap |
| Esc | Jeda/lanjut; melepaskan pointer untuk menggunakan menu |
| R | Mulai ulang FPP dengan navigasi dan seed yang sama |
| Stik kiri / kanan gamepad | Bergerak / melihat |
| Klik stik kiri | Berlari |

Menu jeda memiliki tombol **Mouse −/+**, **FOV −/+**, dan **Ayunan**. Nilai aktif ditampilkan di bawahnya. Pengaturan berlaku selama Play dan bertahan saat mengulang sesi. Untuk menyimpan default antar Play, atur `Mouse Sensitivity`, `First Person Field Of View`, `Head Bob Amount`, `Walk Speed`, dan `Sprint Speed` pada komponen `MiningSimulation` lalu simpan scene. Ayunan dapat dinonaktifkan dengan nilai nol.

FPP tidak berjalan otomatis. Kamera berada 1,65 m di atas kaki; model kepala pekerja disembunyikan agar tidak menutup pandangan. Kehilangan fokus aplikasi menjeda sesi FPP. Detektor dan longsor memakai jadwal yang sama dengan Cerita; pemain harus menghindari area berbahaya. Navigasi adaptif menghitung ulang dari posisi pemain. Navigasi statis mempertahankan rute awal.

Material permukaan dibuat deterministik di `MineSurface`: tekstur warna, normal, metalness, dan smoothness yang dapat berulang. Batu memiliki lapisan mineral; lantai berupa kerikil; kayu memiliki serat memanjang; besi memiliki variasi karat dan pantulan. Detail tambahan mencakup pipa silinder, flange, kabel dinding, pelat penyangga, baut, serta lampu berpelindung. Garis lampu menandai batas zona aman. Bentuk denah dan collider utama tetap berasal dari `MineLayout`.

`Assets/Resources/MiningSurfaceVariants.mat` mempertahankan kombinasi keyword normal map dan metallic map untuk material yang dibuat saat runtime agar varian tersebut tersedia saat membangun player.

Pencahayaan memakai ambient tiga warna yang redup, lampu kerja hangat, lampu helm dengan bayangan lembut, dan kabut tipis. Kamera memakai ACES, bloom ringan, vignette ringan, serta antialiasing FXAA. HUD memakai warna arang dan aksen keselamatan yang lebih netral. Seluruhnya menggunakan generator lokal dan URP yang sudah terpasang.

Tampilan baru dibangun otomatis saat Play. Untuk memperbarui geometri **Editor Preview** yang tersimpan, jalankan **SafeMining > Documentation > Rebuild Editor Preview**, lalu simpan scene. Material preview kini menyimpan seluruh texture map, termasuk normal dan smoothness.

Ekspor CSV mencatat `FirstPerson` atau `Story`. Konfigurasi JSON juga mencatat mode dan kecepatan FPP. Pisahkan hasil latihan manual dari eksperimen Cerita karena keputusan dan kecepatan pemain memengaruhi waktu, jarak, serta paparan.

Validasi tersedia melalui `MiningExperienceValidation.RunBatch` pada salinan proyek: peluncuran FPP dari tombol menu, gerak WASD, kontrol F/G/Esc, pause, reset, collision, reroute dari posisi pemain, keberhasilan evakuasi, ekspor, serta regresi skenario Cerita adaptif/statis.

[Hasil validasi Unity](Validation/fpp-realistic.txt) — seluruh pemeriksaan lolos. Pratinjau: [menu](Previews/fpp-menu.png), [FPP](Previews/fpp-realistic.png), dan [pengaturan kamera](Previews/fpp-settings.png). Validasi ini berjalan di Editor; build player belum diuji.
