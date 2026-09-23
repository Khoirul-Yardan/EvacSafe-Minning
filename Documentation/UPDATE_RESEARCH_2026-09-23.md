# Update simulasi dan penjelasan research problem

**Arsip tahap sebelumnya.** Implementasi terbaru memakai longsor acak, detektor virtual, dan biaya risiko. Lihat [audit full paper dan update terbaru](PAPER_ALIGNMENT_AND_RANDOM_HAZARDS.md). Hasil/fitur dalam dokumen ini menggambarkan versi sebelum penambahan tersebut.

Tanggal: 23 September 2026. Acuan pemeriksaan: kode scene `SafeMining_Experience.unity`, generator, planner, UI, dan runner validasi dalam repositori ini. Rumusan penelitian di bawah merupakan rumusan operasional berdasarkan implementasi, bukan kutipan verbatim abstrak.

## Kesimpulan audit tunnel

**Mine tunnel sudah prosedural, kini dapat dikonfigurasi melalui Inspector, dan memiliki keterlintasan dinamis akibat longsor.** Ketiganya memiliki arti yang berbeda:

| Aspek | Sebelum update | Setelah update ini |
|---|---|---|
| Pembuatan tunnel | Geometri dibuat oleh kode dari enam garis koridor tetap | Tetap prosedural, tetapi daftar koridor menjadi input `MiningSimulation.corridors` |
| Mengubah denah | Mengedit `MineLayout.CreateCells()` | Mengubah From/To koridor di Inspector sebelum Play |
| Bentuk saat sesi berjalan | Tetap | Tetap; perubahan denah diterapkan pada Play berikutnya |
| Bahaya | Normal/waspada/tertutup dapat berubah | Tetap dinamis melalui timeline atau WebSocket |
| Navigasi | Adaptif menghitung ulang; statis mempertahankan rute awal | Perilaku dipertahankan untuk perbandingan Mode Cerita |
| Minimap dan preview | Mengasumsikan ukuran denah bawaan | Mengikuti input dan batas denah yang dikonfigurasi |
| Konfigurasi tidak valid | Tidak tersedia jalur input Inspector | Koridor diagonal, terlalu besar, kehilangan titik penting, atau terputus ditolak |

Tidak tepat menyebutnya generator tambang acak, deformasi batuan real-time, atau editor denah saat Play. Istilah yang sesuai: **lingkungan tambang prosedural deterministik dengan denah yang dapat dikonfigurasi antar sesi dan status keterlintasan dinamis**.

Ukuran sel tetap 6 m. Titik awal, tiga zona aman, dan tiga lokasi longsor masih ditetapkan di kode. Jadwal kejadian dan kecepatan pekerja juga masih ditetapkan di `MiningSimulation`. Mengubah ukuran sel saja belum cukup karena dimensi mesh, properti, dan collider memiliki ukuran yang harus disesuaikan bersama.

Cara mengubah koridor dan daftar koordinat bawaan ada di [panduan tunnel](SAFE_MINING.md#mengubah-tunnel-tanpa-mengedit-kode). Contoh variasi yang valid adalah menambah penghubung `(-4, 2)` ke `(4, 2)`. Perubahan ini harus diuji kembali; hasil denah bawaan tidak otomatis berlaku untuk denah baru.

## Penyesuaian simulasi menjadi Mode Cerita

Menu simulasi kini hanya menawarkan **Mulai Mode Cerita**, dengan pilihan navigasi **Adaptif** atau **Statis**. Shortcut Tab untuk berpindah ke FPP dihapus. Pemanggilan lama `Begin(FirstPerson, ...)` diarahkan ke Story agar tidak membuka sesi manual melalui tool lama.

Pekerja bergerak otomatis dengan briefing, peringatan, longsor, perubahan petunjuk, dan hasil akhir. Gerakan otomatis mengendalikan variasi input pengguna pada perbandingan algoritma. Ini tidak mengukur perilaku manusia atau manfaat pelatihan pengguna.

Cerita memberi konteks kejadian; BFS tetap memilih rute berdasarkan graf aktual. Dialog setelah longsor disesuaikan dengan navigasi adaptif/statis dan tidak menjanjikan jalur timur selalu aman pada denah yang diubah. Jika planner tidak memperoleh rute, sesi berakhir `Blocked` dan dapat diekspor, sehingga tidak terus menunggu tanpa hasil.

Kode FPP lama dan kamera editor FPP masih tersimpan, tetapi bukan bagian dari alur simulasi penelitian. Scene demo lama `SafeMiningEvac_Demo.unity` juga masih merupakan arsip dengan pengendali berbeda.

## Research problem yang dijawab

**Masalah utama:** rute evakuasi yang ditentukan pada awal sesi dapat kehilangan keterlintasannya ketika longsor menutup koridor. Sistem perlu memperbarui rute dari posisi pekerja berdasarkan kondisi terbaru, sambil menyatakan secara eksplisit ketika tidak tersedia jalur menuju zona aman.

Rumusan pertanyaan utama:

> Dalam simulasi tambang bawah tanah dengan penutupan koridor yang berubah terhadap waktu, bagaimana navigasi adaptif yang menghitung ulang rute secara lokal dibandingkan navigasi statis dalam mencapai zona aman, menghindari sel tertutup, dan merespons perubahan bahaya pada skenario yang sama?

| Pertanyaan operasional | Mekanisme yang diuji | Bukti yang dibutuhkan |
|---|---|---|
| Apakah pekerja dapat dialihkan ketika rute awal tertutup? | Update `Blocked`, BFS dari posisi pekerja, pergantian petunjuk dan rute gerak | Timeline bahaya/rute, validitas setiap langkah, outcome adaptif vs statis |
| Apakah adaptif meningkatkan penyelesaian evakuasi pada skenario dengan alternatif yang tersedia? | Sesi berpasangan pada denah, titik awal, kecepatan, dan jadwal sama | Jumlah Success/Blocked per metode serta waktu dan jarak pada sesi berhasil |
| Apakah sistem menahan pekerja ketika tidak ada rute? | Rute kosong menghasilkan `Blocked`; tidak ada fallback melewati sel tertutup | Uji semua akses relevan terputus; rute kosong dan outcome gagal yang benar |
| Seberapa cepat komputasi lokal bereaksi? | Stopwatch di `Plan(...)` setiap perubahan bahaya | `planning_ms` pada event route_update dan `max_planning_ms` per sesi, beserta spesifikasi mesin |
| Apakah hasil bergantung pada bentuk jaringan tunnel? | Mengubah daftar koridor antar sesi | Perbandingan beberapa denah yang disimpan beserta `_layout.csv` dan konfigurasi skenarionya |

Hipotesis kerja: pada skenario yang menutup rute awal tetapi masih menyisakan alternatif yang terjangkau, navigasi adaptif dapat menyelesaikan evakuasi ketika baseline statis terhenti. Hipotesis ini tidak menyatakan adaptif selalu lebih cepat, selalu lebih pendek, atau selalu berhasil. Jika semua akses terputus, hasil yang benar adalah kegagalan terdeteksi.

## Kesesuaian dengan arah abstrak SAFE-MINING EVAC

| Unsur | Implementasi yang dapat ditunjukkan | Batas klaim |
|---|---|---|
| Lingkungan tambang virtual | Tunnel, ruang aman, pekerja, collision, dan minimap dengan satu occupancy map | Model sederhana, bukan rekonstruksi tambang nyata |
| Posisi pekerja dan bahaya dinamis | Posisi aktor dan tiga status lokasi longsor | Posisi/sensor virtual, bukan lokalisasi perangkat fisik |
| Dynamic path planning | BFS deterministik pada graf berbobot sama, dihitung ulang saat bahaya berubah | Adaptasi jalur terpendek dalam graf; bukan algoritma baru atau optimasi risiko berbobot |
| Edge intelligence | Planner berjalan lokal di proses Unity tanpa ketergantungan cloud | Demonstrasi pengambilan keputusan lokal; belum deployment edge, ML, atau benchmark perangkat edge |
| Komunikasi | Adapter WebSocket opsional mengirim snapshot dan menerima status bahaya | Belum membuktikan latensi end-to-end atau ketahanan jaringan lapangan; MQTT belum diimplementasikan |
| Perbandingan | Cerita adaptif vs cerita statis dengan gerak aktor otomatis yang sama | Baseline ini jalur awal tetap, bukan pembanding algoritma canggih lain |
| Evaluasi | Outcome, waktu, jarak, paparan virtual, kontak, reroute, waktu komputasi, event dan denah | Bukti simulasi, bukan bukti keselamatan atau efektivitas AR untuk pekerja nyata |

Kacamata dan petunjuk AR merupakan visualisasi di Unity. Kontribusi yang dapat dipertanggungjawabkan saat ini adalah **rancangan dan prototipe simulasi terintegrasi untuk mengevaluasi navigasi evakuasi adaptif**, bukan kebaruan BFS atau keberhasilan implementasi perangkat wearable.

## Protokol untuk menjawabnya dengan data

1. Beri setiap denah dan skenario ID, misalnya L01/S01. Simpan scene, daftar koridor, versi kode, jadwal bahaya, titik awal/exit, kecepatan 2,8 m/s, radius paparan 4,5 m, spesifikasi mesin, dan pengaturan waktu/frame rate. ID eksperimen dan spesifikasi mesin dicatat pada manifest penelitian terpisah; kolom ini belum otomatis masuk CSV.
2. Jalankan Cerita Adaptif dan Cerita Statis secara berpasangan dari kondisi awal yang sama. Gunakan timeline yang sama; nonaktifkan WebSocket untuk uji offline, atau gunakan urutan perintah dengan waktu simulasi yang sama untuk kedua metode. Cerita statis boleh berhenti lebih awal sehingga tidak semua kejadian berikutnya sempat dieksekusi.
3. Ekspor setelah sesi berakhir. Simpan tiga file dengan prefix yang sama: summary, events, dan layout. `_layout.csv` merekam sel aktual; catatan skenario diperlukan karena file tersebut tidak merekam seluruh parameter, lokasi exit, atau konfigurasi perangkat.
4. Periksa apakah rute hanya melintasi sel terhubung yang tidak tertutup. Pisahkan kegagalan karena jalur statis terhalang, tidak ada rute adaptif, dan longsor tepat di posisi aktor melalui event akhir. Jangan mengubah kegagalan menjadi waktu sukses.
5. Sajikan tabel hasil per pasangan dan per denah. Tingkat keberhasilan = jumlah sesi `Success` / jumlah sesi akhir yang valid. Tampilkan jumlah `Blocked` dan alasan; data Paused/Running bukan hasil akhir. Jika kedua metode berhasil, bandingkan waktu/jarak secara berpasangan. Jika hanya satu berhasil, laporkan perbedaan outcome dan metrik masing-masing tanpa persentase percepatan palsu.
6. Untuk waktu komputasi, ambil event `route_update`, laporkan jumlah sampel dan distribusinya. Kolom maksimum summary saja tidak cukup untuk menghitung rata-rata atau persentil semua panggilan planner. Pisahkan uji pemanasan dan uji terukur dengan prosedur yang konsisten.
7. Ulangi pada beberapa denah dan skenario. Pengulangan identik pada sistem deterministik berguna untuk memeriksa konsistensi serta variasi waktu komputasi, tetapi bukan pengganti variasi skenario atau sampel pekerja manusia.

Matriks eksperimen yang disarankan:

| Skenario | Tujuan | Ketersediaan saat ini |
|---|---|---|
| Denah bawaan, dua longsor bertahap | Membandingkan adaptif yang beralih rute dengan baseline terhalang | Langsung tersedia melalui menu |
| Tambahan/pengurangan koridor yang valid, jadwal sama | Menguji pengaruh topologi terhadap outcome, jarak, dan reroute | Daftar Corridors tersedia; setiap varian perlu diuji |
| Tanpa longsor | Kontrol untuk memeriksa bahwa kedua metode memiliki perilaku dasar sebanding | Belum ada preset menu; perlu pengaturan kode/harness timeline terpisah |
| Seluruh akses pekerja terputus | Memastikan kegagalan aman dan hasil `Blocked` | Uji sintetis tersedia di runner; bukan preset cerita menu. Tiga longsor bawaan tidak otomatis memutus semua akses pada denah bawaan |
| Pembaruan bahaya lewat jaringan | Memeriksa integrasi penerimaan status dan reroute | Adapter tersedia; eksperimen latensi end-to-end memerlukan instrumentasi tambahan |

Tabel laporan sebaiknya memuat `layout_id`, `scenario_id`, metode, outcome, waktu, jarak, paparan, kontak, reroute, dan waktu komputasi. Isi angka dari file hasil; jangan memakai angka uji regresi sebagai rata-rata penelitian atau menggeneralisasikan satu denah ke semua jaringan tambang.

## Cara menjawab saat presentasi atau menulis pembahasan

> Research problem kami adalah perubahan keterlintasan koridor yang membuat rute evakuasi awal tidak lagi dapat digunakan. Kami membangun lingkungan tambang prosedural yang denahnya dapat dikonfigurasi, lalu membandingkan navigasi adaptif dan statis dalam Mode Cerita dengan gerakan pekerja dan jadwal bahaya yang sama. Pada metode adaptif, pembaruan longsor memperbarui graf dan memicu pencarian ulang dari posisi pekerja. Keberhasilan dinilai dari tercapainya zona aman, validitas rute terhadap sel tertutup, paparan virtual, serta waktu komputasi lokal. Jika tidak ada jalur, sistem melaporkan kegagalan. Hasilnya menjawab kemampuan adaptasi pada skenario simulasi yang diuji; pengujian perangkat edge, sensor fisik, dan efektivitas AR pada manusia berada di luar bukti prototipe saat ini.

Untuk hasil sementara, tambahkan hanya temuan yang sudah diuji: pada denah bawaan, uji regresi cerita adaptif mencapai zona aman dengan dua reroute; baseline berhenti tanpa reroute. Kesimpulan kuantitatif lintas skenario menunggu eksperimen berpasangan sesuai protokol di atas.

## Validasi update ini

Unity 6000.3.23f1 dijalankan dalam batch tanpa grafis pada salinan proyek. [Log hasil terbaru](Validation/story-configurable-2026-09-23.txt) mencatat:

- Perubahan koridor menghasilkan topologi berbeda; input diagonal, kosong, dan terputus ditolak.
- Sebanyak 524 kombinasi asal/status bahaya pada denah bawaan memiliki rute valid; pemutusan akses sintetis menghasilkan rute kosong.
- Collision denah bawaan: 67 sel lantai/atap, 152 koneksi yang diuji pada tiga posisi melintang, dan 116 batas luar tertutup.
- Menu tidak memiliki tombol FPP; permintaan FPP melalui API lama memulai Story.
- Cerita adaptif berhasil dengan 2 reroute, sekitar 45,6 detik simulasi, dan paparan 0 pada satu uji regresi ini. Baseline terhenti sebelum longsor dengan 0 reroute.
- Jeda/lanjut, reset sesi, ekspor, collision pekerja, dan hasil terminal tanpa rute lulus.

Angka di atas adalah hasil uji fungsi otomatis pada denah bawaan, bukan hasil penelitian multi-skenario. Validasi topologi varian belum merupakan pengujian collision/gameplay untuk semua kemungkinan input Inspector. Batch tanpa grafis tidak menguji kualitas visual atau kenyamanan kamera. WebSocket tidak diuji ulang pada pembaruan ini; log koneksi sebelumnya tetap menjadi arsip versi sebelumnya.

Log editor juga mencatat `ArgumentOutOfRangeException` pada pengindeksan `UnityEditor.Search.SearchDatabase` saat startup. Stack trace tersebut berada pada pencarian editor, bukan skrip simulasi; runner tetap menyelesaikan seluruh pemeriksaan dan proses keluar dengan kode 0. Karena itu, hasil di atas menyatakan pemeriksaan fungsi lulus, bukan klaim bahwa seluruh log editor bebas exception.

## Berkas implementasi utama

| Berkas | Peran dalam update |
|---|---|
| `Assets/Scripts/MineLayout.cs` | Definisi koridor serializable, generator sel, validasi, BFS, dan geometri |
| `Assets/Scripts/MiningSimulation.cs` | Input koridor, pembatasan Story, dialog, hasil tanpa rute, dan ekspor denah runtime |
| `Assets/Scripts/MiningHUD.cs` | Menu cerita dan minimap dengan ukuran mengikuti denah |
| `Assets/Scripts/Editor/MiningDocumentationPreview.cs` | Preview memakai konfigurasi scene dan kamera overview mengikuti batas map |
| `Assets/Scripts/Editor/MiningExperienceBuilder.cs` | Menu scene cerita dan uji regresi terbaru |
| `Assets/Scripts/MiningTelemetry.cs` | Mencegah snapshot aktor kosong ketika konfigurasi denah ditolak |

Pengembangan berikutnya yang belum diimplementasikan: preset skenario tanpa bahaya, konfigurasi timeline/titik skenario via Inspector, manifest eksperimen otomatis, pengukuran latensi end-to-end, dan deployment pada perangkat edge fisik. Ini adalah pekerjaan lanjutan, bukan fitur yang diklaim tersedia dalam update ini.
