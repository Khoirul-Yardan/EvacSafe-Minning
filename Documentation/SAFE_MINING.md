# SAFE-MINING EVAC - panduan Mode Cerita

Pembaruan 23 September 2026. Simulasi penelitian menggunakan **Mode Cerita otomatis**, dengan navigasi **adaptif** atau **statis** pada denah dan kejadian yang sama. [Audit dan penjelasan research problem](UPDATE_RESEARCH_2026-09-23.md) menjelaskan dasar perbandingan dan batas klaim.

## Menjalankan simulasi

Buka `Assets/Scenes/SafeMining_Experience.unity`, tekan **Play**, pilih jenis navigasi, lalu **Mulai Mode Cerita**. Menu editor: **SafeMining > Open Story Experience**.

| Kontrol | Fungsi |
|---|---|
| Esc | Jeda / lanjut |
| R | Mengulang cerita dengan jenis navigasi yang sama |
| Menu simulasi pada panel hasil/jeda | Kembali untuk memilih adaptif atau statis |
| Ekspor hasil evaluasi (.csv) | Menyimpan data sesi saat ini |

Pekerja berjalan otomatis, kamera mengikuti dari belakang, dan dialog tim menerangkan perubahan kondisi. Tidak ada pilihan FPP atau pergantian mode melalui Tab. Enum dan fungsi internal FPP lama masih ada untuk kompatibilitas, tetapi `Begin(...)` selalu memulai Story. Kamera dokumentasi FPP yang tersimpan hanya merupakan sudut pratinjau editor.

## Mengubah tunnel tanpa mengedit kode

1. **Stop Play**, pilih objek yang memiliki komponen **MiningSimulation** pada scene utama (nama objek scene lama mungkin masih mengandung `Story + FPP`).
2. Buka daftar **Corridors**. Setiap elemen memiliki `From` dan `To`, berupa koordinat grid. `X` menjadi sumbu dunia X, `Y` menjadi sumbu dunia Z; satu sel berukuran 6 meter.
3. Ubah ujung koridor, tambah, atau hapus elemen. Kedua ujung harus berada pada satu baris atau kolom. Contoh tambahan penghubung: `From = (-4, 2)`, `To = (4, 2)`.
4. Pertahankan titik awal, tiga titik longsor, dan koneksi ke seluruh zona aman. Jalankan **SafeMining > Documentation > Rebuild Editor Preview** untuk memeriksa bentuknya, lalu simpan scene.
5. Tekan **Play**. Lantai, dinding, atap, collider, graf navigasi, dan minimap dibentuk dari himpunan sel yang sama. Uji ulang adaptif dan statis untuk setiap denah.

Denah awal terdiri atas:

| Koridor | From | To |
|---|---|---|
| Galeri pusat | (0, -5) | (0, 10) |
| Penghubung bawah | (-4, 0) | (4, 0) |
| Penghubung tengah | (-4, 3) | (4, 3) |
| Penghubung atas | (-4, 6) | (4, 6) |
| Galeri barat | (-4, 0) | (-4, 8) |
| Galeri timur | (4, 0) | (4, 8) |

Titik skenario tetap berada di `MineLayout.cs`: awal `(0, -4)`; zona aman `(0, 10)`, `(-4, 8)`, `(4, 8)`; longsor pusat `(0, 4)`, barat `(-4, 5)`, timur `(4, 1)`. Ruang aman diperlebar otomatis. Lokasi/jumlah titik skenario, ukuran sel, kecepatan pekerja, dan jadwal cerita belum menjadi pengaturan Inspector.

Generator menolak koridor diagonal, koordinat di luar -30 hingga 30, denah lebih dari 300 sel, titik awal/longsor yang hilang, serta komponen lorong/ruang aman yang terputus. Error tampil di Console; simulasi tidak dimulai. Saat rebuild, validasi dilakukan sebelum pratinjau lama diganti. Konektivitas awal tidak menjamin rute tetap tersedia setelah longsor.

Pengubahan ini berlaku **antar sesi**. Mengubah Corridors ketika Play tidak mengubah geometri sesi aktif; hentikan dan mulai Play lagi. Tombol R mereset kejadian dan pekerja pada denah yang sudah dibangun. Denah tersimpan di scene, bukan generator acak berbasis seed.

## Apa yang prosedural dan dinamis?

`MineLayout.CreateCells(corridors)` menghasilkan occupancy map; `MineLayout.Build(...)` membentuk geometri dan collision. Tekstur mineral, variasi permukaan batu, penyangga, rel, lampu, dan properti dibuat oleh kode. Input yang sama menghasilkan denah yang sama.

Saat sesi berjalan, `MiningSimulation.SetHazard(...)` mengubah status normal/waspada/tertutup. Status tertutup menambah sel ke `Blocked` serta mengaktifkan longsor dan collider. Navigasi adaptif menjalankan kembali BFS dari sel pekerja menuju zona aman terdekat menurut jumlah langkah graf. Semua langkah berbobot sama; status waspada belum menambah bobot risiko dan belum menutup sel.

Artinya, **status keterlintasan dan rute dinamis**, sedangkan **bentuk tunnel tetap selama sesi**. Tidak ada simulasi penggalian, deformasi geologi, pengacakan denah, atau editor tunnel langsung saat Play.

## Alur cerita bawaan

| Waktu simulasi | Peristiwa |
|---|---|
| 0-4 detik | Briefing; pekerja menunggu |
| Setelah 4 detik | Pekerja mengikuti rute awal dengan kecepatan nominal 2,8 m/s |
| 6 detik | Peringatan galeri pusat |
| 10 detik | Longsor pusat; adaptif menghitung ulang, statis mempertahankan rute awal |
| 19 detik | Peringatan galeri barat, bila sesi masih berlangsung |
| 23 detik | Longsor barat; adaptif memeriksa jalur alternatif, bila sesi masih berlangsung |
| Tiba di zona aman | Outcome `Success`; hasil ditampilkan |
| Jalur statis tertutup / tidak ada rute adaptif | Outcome `Blocked`; hasil ditampilkan |

Tidak ada restart otomatis. Longsor tetap aktif sampai sesi diulang atau menerima pembaruan eksplisit. Dialog menjelaskan kejadian, sedangkan pilihan rute berasal dari planner; hasil tidak dipaksakan untuk selalu berhasil. Denah baru dapat mengubah exit terpilih, waktu tiba, jumlah reroute, maupun kejadian yang sempat dialami.

Scene demo lama memakai `ScenarioRunner` dengan timeline dan restart berbeda; skrip tersebut bukan pengendali scene penelitian ini. Timeline aktif ada di `MiningSimulation.RunTimeline()`.

## Dokumentasi tanpa Play

Hierarchy **EDITOR PREVIEW | Documentation (excluded from Play)** menyimpan map, pekerja, dan kamera. Pilih root tersebut untuk tombol **Seluruh map / atap terbuka**, **Sudut kamera cerita**, contoh longsor, dan ekspor PNG 1920 x 1080. Resource hasil generator berada di `Assets/Generated/MiningDocumentation/PreviewResources.asset`.

Setelah mengubah Corridors, gunakan **Rebuild Editor Preview** dan simpan scene. Rebuild memakai pengaturan komponen simulasi pada scene yang sama. Menggeser objek pratinjau secara manual tidak mengubah denah runtime. Rebuild mengganti hierarchy/resource hasil generator.

Pratinjau dinonaktifkan sebelum runtime membuat map dan diberi tag `EditorOnly`. PNG pratinjau tidak memuat HUD atau label Gizmos. Kamera seluruh map menyesuaikan batas denah; minimap runtime juga menyesuaikan rentang sel. Kamera FPP editor tetap tersedia sebagai sudut dokumentasi lama, bukan mode simulasi penelitian.

## Evaluasi dan ekspor

Jalankan **Cerita + Adaptif**, ekspor hasil, lalu **Cerita + Statis** pada konfigurasi sama. Gunakan outcome sebagai pembanding utama; waktu berhenti baseline bukan waktu evakuasi berhasil. Rincian protokol, skenario kontrol, dan rumusan jawaban penelitian ada di [dokumen update](UPDATE_RESEARCH_2026-09-23.md).

Ekspor tersimpan pada `Application.persistentDataPath/Evaluasi` dan lokasi lengkap tampil di panel. Setiap ekspor menghasilkan tiga file dengan awalan waktu UTC, mode, dan navigasi yang sama:

| File | Isi |
|---|---|
| `_summary.csv` | Mode, navigasi, outcome, waktu, jarak, paparan, kontak, reroute, respons hitung maksimum |
| `_events.csv` | Waktu kejadian, perubahan bahaya/rute, hasil akhir |
| `_layout.csv` | Koordinat seluruh sel denah runtime untuk mencocokkan pasangan percobaan |

`elapsed_s` mencakup briefing tetapi tidak bertambah ketika jeda. Nilainya memakai delta waktu simulasi yang dibatasi 0,05 detik/frame, sehingga tidak selalu sama dengan waktu dinding pada frame rate rendah. `distance_m` adalah akumulasi perpindahan horizontal aktor.

`exposure_s` menghitung durasi dalam radius 4,5 m dari pusat longsor berstatus tertutup. `hazard_contacts` menghitung masuknya aktor ke perimeter tersebut dan kejadian longsor tepat di sel aktor; ini bukan hitungan benturan fisika atau prediksi cedera. `reroutes` bertambah ketika pembaruan bahaya mengubah target exit atau membuat rute lama melintasi sel tertutup; bukan jumlah seluruh panggilan BFS.

`max_planning_ms` merupakan waktu maksimum komputasi lokal pada pembaruan bahaya, termasuk peringatan. Pengukuran mencakup pencarian dan penyesuaian rute dengan Stopwatch, bukan latensi sensor, WebSocket, rendering, atau perangkat edge fisik. Ekspor saat Paused/Running/Menu bukan hasil akhir; gunakan hanya `Success`/`Blocked` untuk membandingkan hasil akhir.

## WebSocket opsional

`MiningTelemetry` menyediakan `endpoint` dan `connectOnStart`, default offline. Demonstrasi lokal:

```powershell
python Tools/telemetry_server.py
```

Server membutuhkan Python `websockets`. Aktifkan `connectOnStart` sebelum Play atau gunakan context menu komponen **Connect WebSocket** saat Play. Endpoint default `ws://127.0.0.1:8765`. Snapshot dikirim setiap 0,5 detik; perintah diterapkan hanya saat sesi Running.

```json
{"type":"hazard","index":2,"level":2}
```

Index 0/1/2 = pusat/barat/timur; level 0/1/2 = normal/waspada/tertutup. Level 0 membuka kembali lokasi secara eksplisit. Server hanya mencatat data secara default; `--demo-hazard` menutup timur setelah snapshot waktu >=30 detik. Timeline bawaan tetap aktif. Untuk pasangan eksperimen, gunakan input jaringan yang sama atau nonaktifkan koneksi.

## Validasi pengembang

`MiningExperienceValidation` menguji graf, konfigurasi koridor, geometri/collision, cerita adaptif, baseline statis, pembatasan Story, jeda, reset, ekspor, dan hasil tanpa rute. Jalankan di salinan proyek karena runner menutup editor dan mengubah pengaturan Play untuk pengujian:

```powershell
Unity.exe -batchmode -nographics -projectPath "PATH_SALINAN_PROYEK" -executeMethod MiningExperienceValidation.RunBatch -logFile validation.log
```

Hasil berada di `Validation/results.txt` pada salinan pengujian. Log lama `final-gameplay.txt`, `websocket-and-gameplay.txt`, dan `editor-preview.txt` merupakan bukti versi sebelumnya, bukan pengujian terbaru. Uji batch tanpa grafis tidak memverifikasi tampilan visual menu/kamera.
