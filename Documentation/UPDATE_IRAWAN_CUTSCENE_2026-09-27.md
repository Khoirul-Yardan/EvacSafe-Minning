# Integrasi feat/irawan dan cutscene longsor — 27 September 2026

## Integrasi dan audit

Branch lokal `main` sebelumnya berada di `d142507`. `origin/feat/irawan` menambahkan satu commit, `8b9ccd9` (`Show live edge-to-evacuation flow in HUD`), hanya pada `MiningHUD.cs`. Integrasi dilakukan dengan `git merge --ff-only origin/feat/irawan`; tidak ada perubahan lokal tertunda atau konflik. Implementasi getaran, MQTT, planner, kontrol, dan denah dari commit sebelumnya tetap dipertahankan.

| Acuan dokumentasi | Hasil audit |
|---|---|
| Getaran → edge → MQTT → penerapan → navigasi | HUD branch berlangganan event sampel/penerapan dan menampilkan sumber transport serta exit. Sesuai kontrak baca saja. |
| Identitas sesi berubah setiap Begin | HUD melepas langganan sesi lama dan memasang sesi baru. |
| Intensitas virtual 0..1, bukan satuan fisik | Label HUD tetap `GETARAN SIM`; tidak mengklaim pembacaan sensor fisik. |
| Sumber eksklusif, tanpa fallback MQTT ke lokal | Panel cutscene membaca `HazardLevels` hasil penerapan. Keputusan edge yang belum diterima dari MQTT tidak memunculkan cutscene. |
| LegacyTimeline sebagai pembanding | Diperjelas: HUD menampilkan `JADWAL LEGACY`, bukan pesan menunggu sensor yang tidak dipakai. |
| Cutscene belum termasuk lingkup 25 September | Permintaan terbaru menambahkan panel kamera; README, panduan edge, dan pembagian lingkup diperbarui. |

Koreksi HUD setelah penggabungan: panel alur dan label perlengkapan diposisikan agar tidak bertumpuk dengan panel misi atau satu sama lain pada layout acuan 1600×900; pembacaan dipilih dari intensitas terbaru tertinggi di seluruh perangkat, agar sampel terakhir perangkat normal tidak menutupi getaran perangkat berbahaya. Baris pembacaan dan baris status penerapan dapat menyebut perangkat berbeda: sampel tertinggi dan pesan terapan terbaru memang dua informasi berbeda.

## Perilaku panel

- Panel 380×264 berada tepat di atas dialog radio pada kedua mode. Kamera tambahan menampilkan dunia simulasi ke RenderTexture 640×360, dengan ID perangkat dan koordinat grid.
- Status `2` yang sudah diterapkan memicu satu tayangan per perangkat per sesi. Status normal/waspada tidak memicu panel. Snapshot/duplikat tidak mengulang tayangan.
- Satu lokasi ditampilkan selama lima detik **waktu simulasi**. Beberapa lokasi mengantre; jumlah lokasi menunggu ditampilkan. Kejadian yang diterapkan dalam satu frame ditampilkan berdasarkan urutan ID perangkat.
- Kamera lokasi bersifat **live**: kejadian pertama dapat memperlihatkan batu jatuh; lokasi yang menunggu antrean dapat sudah berupa timbunan ketika ditampilkan. Tidak memutar ulang fisika atau menunda longsor agar terlihat dramatis.
- Pause menyembunyikan panel, mematikan rendering kamera tambahan, serta mempertahankan waktu dan antrean. Resume melanjutkan. Menu, hasil akhir, dan restart menghapus tayangan/antrean. Restart mengizinkan perangkat yang sama muncul pada sesi baru.
- Kamera utama, input FPP, gerakan otomatis Cerita, planner, collider, sirene, serta ekspor eksperimen tetap menggunakan jalur semula. Tidak ada AudioListener tambahan. Kacamata AR yang dimatikan tidak mematikan panel radio/cutscene.
- Kamera/RenderTexture dibuat saat longsor pertama, rendering hanya aktif saat panel tampil, dan resource dilepas ketika komponen dihancurkan.

`MiningLandslideCutscene.cs` mengelola kamera dan antrean. `MiningHUD.cs` membangun panel. `MiningSimulation.SessionRevision` memberi identitas reset UI yang juga bekerja untuk LegacyTimeline tanpa instance edge.

## Mencoba versi terbaru

1. Jalankan `docker compose -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d` dari root proyek.
2. Buka `Assets/Scenes/SafeMining_Experience.unity`, tekan Play, pilih skenario **Scripted**, lalu mulai FPP. Tetap diam untuk mengamati bahwa sensor tidak bergantung posisi pemain.
3. D01 menerima profil danger pada detik 10; sesudah durasi ambang dan penerimaan broker, lorong tertutup dan panel tampil. D02 menyusul dari profil danger detik 23. ID/grid panel harus cocok dengan detektor merah di lokasi tersebut.
4. Tekan Esc saat panel tampil, lanjutkan, lalu R untuk menguji reset. Gunakan skenario dan seed sama pada Cerita untuk membandingkan.
5. Pilih **Random** dan atur `Random Event Count` sebelum mulai untuk beberapa lokasi longsor. Pilih **NoHazards** untuk memastikan panel tidak muncul.
6. Tanpa broker, pilih `LocalEdgeSimulation` pada Inspector sebelum mulai. Tidak ada fallback otomatis dari MQTT.

## Pengujian

`MiningCutsceneValidation.RunBatch` menguji tiga penutupan serentak melalui entry point LegacyTimeline, antrean/durasi, pause/resume, duplikat, pergantian mode, reset, menu, kamera utama, dan screenshot FPP/Cerita. `MiningEdgeValidation.RunBatch` juga memeriksa cutscene pada penerapan local/MQTT, Cerita, pause/reset, serta ketiadaan cutscene saat broker tidak tersedia.

Runner dijalankan pada `.tools/ValidationProject`, salinan terpisah yang dibuat oleh `.tools/prepare_validation.py`. Hasil dicatat di [validasi cutscene](Validation/cutscene-2026-09-27.txt) dan [regresi edge/MQTT](Validation/edge-cutscene-2026-09-27.txt). Bukti ini untuk Unity Editor Windows; build player dan rasio layar lain memerlukan pengujian terpisah.

Pratinjau pengujian: [FPP](Previews/cutscene-fpp.png), [Cerita](Previews/cutscene-story.png), dan [perpindahan ke D02](Previews/cutscene-second-device.png). Gambar ini memakai LegacyTimeline untuk menerapkan tiga kejadian serentak secara terkontrol; jalur MQTT nyata diuji oleh runner edge secara terpisah.

Pada runner `-nographics`, antrean/status panel tetap diuji tetapi rendering kamera tambahan dinonaktifkan karena tidak ada perangkat grafis. Validasi tampilan menggunakan runner cutscene dengan grafis aktif.
