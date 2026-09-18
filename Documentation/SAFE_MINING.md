# SAFE-MINING EVAC — mode cerita dan FPP

Buka `Assets/Scenes/SafeMining_Experience.unity`, lalu tekan **Play**. Pilih **Mode Cerita** atau **Mode FPP**. Jika scene belum terlihat setelah Unity selesai mengimpor, gunakan menu **SafeMining → Open Story + FPP Experience**. Scene demo sebelumnya tetap tersedia.

## Kontrol

| Kontrol | Fungsi |
|---|---|
| WASD / analog kiri gamepad | Berjalan dalam FPP |
| Mouse / analog kanan | Melihat dalam FPP |
| Shift kiri | Berjalan cepat |
| Esc | Jeda / lanjut; membuka kursor |
| G | Mengaktifkan / menonaktifkan petunjuk kacamata |
| R | Memulai sesi baru dengan mode dan navigasi yang sama |
| Tab | Beralih cerita ↔ FPP dan memulai sesi baru |

Mode cerita menggerakkan pekerja otomatis, dengan kamera mengikuti dari belakang, animasi berjalan sederhana, APD, dan dialog tim. Mode FPP memakai gerakan manual dengan CharacterController. Kacamata hanya menyarankan jalur; pemain tetap memilih sendiri. Semua exit yang ditandai hijau dapat menyelesaikan evakuasi.

## Kesesuaian dengan abstrak

Acuan: **Towards SAFE-MINING EVAC: Perancangan Simulasi Navigasi Evakuasi Adaptif Berbasis Unity dan Edge Intelligence pada Area Tambang Bawah Tanah**, abstrak tim PENS yang diberikan pengguna (`INJECTION EPW 17_Abstrak_... (1).pdf`).

| Kebutuhan abstrak | Implementasi |
|---|---|
| Lingkungan tambang virtual | Galeri pusat, barat, timur, dua koridor penghubung dan tiga ruang aman |
| Posisi pekerja dan bahaya dinamis | Posisi aktor aktif; status normal, waspada, tertutup pada tiga lokasi longsor |
| Dynamic path planning | Pencarian jalur terpendek pada graf sel berbiaya sama, dihitung ulang saat bahaya berubah; dalam FPP dihitung dari posisi pemain |
| Pemrosesan edge | Planner lokal dalam proses Unity, tanpa ketergantungan cloud. Ini simulasi komputasi lokal, bukan perangkat edge fisik atau model ML |
| Komunikasi MQTT atau WebSocket | Adapter WebSocket opsional: mengirim snapshot dan menerima perintah bahaya; mode offline berfungsi tanpa server |
| Baseline statis | Jalur yang ditetapkan saat awal sesi dipertahankan; cerita berhenti sebelum menabrak longsor |
| Waktu, keamanan, reroute, respons | Timer simulasi, jarak, paparan perimeter bahaya, jumlah kontak, perubahan rute, waktu komputasi; hasil dan event dapat diekspor ke CSV |

Kacamata AR dan dua mode interaksi merupakan pengembangan sesuai gambar dan permintaan pengguna. Model karakter, batuan, dan perlengkapan dibuat secara prosedural dengan gaya sederhana; bukan aset fotorealistis seperti ilustrasi referensi. Sensor dan kedalaman tambang adalah representasi virtual, bukan pengukuran perangkat nyata.

## Skenario yang dapat diulang

| Waktu simulasi | Peristiwa |
|---|---|
| 0–4 s | Briefing; pekerja cerita menunggu instruksi |
| 6 s | Peringatan getaran galeri pusat; status waspada |
| 10 s | Longsor galeri pusat menutup rute awal; navigasi adaptif memilih alternatif |
| 19 s | Peringatan galeri barat |
| 23 s | Longsor barat; navigasi kembali mencari rute aman melalui jaringan penghubung |
| Tiba di zona aman | Skenario berhenti dan hasil ditampilkan; tidak restart otomatis |

Longsor tidak otomatis hilang. Peringatan merah, tumpukan batu, dan collider tetap aktif sampai sesi diulang atau ada pembaruan eksplisit. Tidak ada fallback planner yang mengarahkan pekerja melewati bahaya. Jika semua rute tidak tersedia, kacamata menampilkan tidak ada rute aman.

## Integritas map

`MineLayout.CreateCells()` menjadi sumber tunggal denah, geometri lantai, dinding, atap, planner, dan minimap. Lantai antar sel sedikit bertumpuk untuk menutup garis sambungan; dinding hanya dibuat di batas luar. Sampel mesh atap dan dinding menggunakan koordinat dunia yang sama di setiap sambungan. Tidak ada platform tersembunyi yang memungkinkan berjalan di luar tambang.

Struktur runtime terpisah menjadi UI, logika simulasi, lingkungan, dan terrain/collision. Collider lantai, dinding batu, penyangga, serta longsor menjaga gerakan tetap di lorong. Minimap digambar dari denah yang sama sehingga atap tidak menutupi jalur.

## Evaluasi

Untuk perbandingan yang konsisten, jalankan **Mode Cerita + Adaptif**, ekspor CSV dari panel hasil, lalu ulangi **Mode Cerita + Statis** dari menu. Jangan membandingkan waktu sukses adaptif dengan waktu baseline yang terhenti seolah keduanya berhasil; periksa kolom `outcome`. FPP memiliki variasi keputusan dan kecepatan pemain sehingga perlu dilaporkan sebagai uji interaksi terpisah.

CSV berada di `Application.persistentDataPath/Evaluasi`; lokasi lengkap ditampilkan setelah menekan **Ekspor hasil evaluasi (.csv)**. File summary mencatat mode, navigasi, hasil, waktu, jarak, paparan, kontak bahaya, reroute dan respons hitung maksimum. File events mencatat timeline perubahan bahaya/rute.

Paparan adalah durasi dalam radius 4,5 m dari pusat longsor aktif, bukan estimasi cedera atau keselamatan tambang nyata. Waktu respons adalah waktu komputasi planner lokal dengan Stopwatch, bukan latensi WebSocket atau pengukuran sensor. Semua metrik dihasilkan dari sesi berjalan; tidak ada angka hasil penelitian yang ditanam sebelumnya.

## WebSocket opsional

Komponen `MiningTelemetry` pada root scene menyediakan `endpoint` dan `connectOnStart`. Default tidak menghubungkan jaringan. Untuk demonstrasi lokal, jalankan:

```powershell
python Tools/telemetry_server.py
```

Server contoh membutuhkan paket Python `websockets` (`python -m pip install websockets` jika belum tersedia). Aktifkan `connectOnStart` di Inspector sebelum Play, atau saat Play pilih context menu komponen **Connect WebSocket**. Endpoint default `ws://127.0.0.1:8765`. Adapter mengirim snapshot setiap 0,5 detik; perintah diterapkan hanya saat sesi berjalan.

Contoh perintah:

```json
{"type":"hazard","index":2,"level":2}
```

`index`: 0 pusat, 1 barat, 2 timur; `level`: 0 normal, 1 waspada, 2 tertutup. Gunakan level 0 hanya untuk menguji pembaruan kondisi secara eksplisit. Server contoh hanya mencatat data secara default; opsi `--demo-hazard` mengirim penutupan timur setelah menerima snapshot waktu ≥30 detik. Timeline bawaan tetap berjalan.

## Validasi pengembang

Menu dan skrip editor `MiningExperienceValidation` mencakup seluruh kombinasi tiga bahaya, validitas rute, sambungan collision, cerita adaptif, baseline, FPP, jeda, restart, dan ekspor. Jalankan hanya di salinan proyek pengujian karena metode batch menutup editor setelah selesai:

```powershell
Unity.exe -batchmode -nographics -projectPath "PATH_SALINAN_PROYEK" -executeMethod MiningExperienceValidation.RunBatch -logFile validation.log
```

Hasil ditulis ke `Validation/results.txt` dalam proyek pengujian. Validasi ini memeriksa model map dan gameplay; inspeksi visual serta kenyamanan kontrol tetap memerlukan Game View.
