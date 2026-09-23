# Kesesuaian paper, longsor acak, dan perangkat deteksi

Pembaruan 23 September 2026. Dokumen ini menggantikan keterangan tentang tiga lokasi bahaya tetap, timeline tunggal, dan BFS tanpa bobot pada update sebelumnya.

## Acuan yang diperiksa

PDF lokal pengguna: `C:\Users\ACER NITRO\Downloads\Full Paper Mahasiswa EPW 17.docx.pdf`, enam halaman, berjudul **Towards SAFE-MINING EVAC: Perancangan Simulasi Navigasi Evakuasi Adaptif Berbasis Unity dan Edge Intelligence pada Area Tambang Bawah Tanah**. Pemeriksaan mencakup teks serta gambar tahapan penelitian di halaman 3 dan arsitektur empat lapisan di halaman 4. File PDF asli tidak diubah.

**Kesimpulan: implementasi sesuai dengan arah prototipe simulasi dalam paper, tetapi belum memenuhi seluruh rincian rancangan maupun evaluasi penelitian.** Paper memang menempatkan integrasi perangkat keras sebagai pengembangan berikutnya. Namun, modul Python untuk pemrosesan edge dan eksperimen penelitian lengkap yang disebut pada metode belum tersedia dalam implementasi saat ini.

| Bagian paper | Status setelah update | Batas / tindak lanjut |
|---|---|---|
| Hal. 1-3: tambang Unity, jaringan jalur, pekerja, zona aman | Tersedia; denah prosedural dapat diatur melalui Corridors | Satu pekerja otomatis, belum populasi pekerja dengan parameter perilaku |
| Hal. 1, 3: perubahan posisi dan tingkat bahaya | Tersedia; lokasi dari jaringan detektor, waktu dan urutan dipilih dengan seed; status normal/waspada/tertutup | Acak terkontrol pada titik kandidat, bukan distribusi probabilitas geologi yang dikalibrasi |
| Hal. 3-4: posisi virtual sebagai pengganti UWB | Posisi aktual aktor Unity menjadi input planner dan telemetry | Representasi koordinat virtual ideal; tidak mensimulasikan anchor, ranging, noise, atau ketelitian UWB |
| Hal. 3-4: graf dengan jarak, risiko dan keterlintasan | Planner berbobot jarak + penalti waspada; sel tertutup dilarang | Risiko bersifat diskret dan heuristik; belum model probabilistik atau optimasi multiobjektif waktu/risiko |
| Hal. 4: data ingestion, pembaruan lingkungan, risk assessment, route planner | Alur logis tersedia dalam proses Unity | Belum empat layanan/proses terpisah; penilaian risiko saat ini pemetaan status ke penalti |
| Hal. 3: Python untuk pemrosesan/pengambilan keputusan edge | Belum sesuai sepenuhnya: planner masih C# lokal | `Tools/telemetry_server.py` adalah server contoh, bukan planner Python |
| Hal. 1, 3: MQTT atau WebSocket | Adapter WebSocket tersedia; snapshot memuat lokasi detektor dan seed | MQTT tidak diperlukan jika memilih WebSocket, tetapi loop Unity -> planner Python -> rute belum diimplementasikan |
| Hal. 1-2: perbandingan statis vs adaptif | Kedua metode tersedia dengan replay jadwal yang sama | Hasil uji regresi belum menggantikan eksperimen lintas denah/seed dan analisis hasil penelitian |
| Hal. 1: waktu, keamanan jalur, reroute, respons | CSV summary/events/layout/scenario dan konfigurasi JSON | Paparan virtual bukan prediksi cedera; respons yang diukur masih waktu komputasi lokal, bukan end-to-end jaringan |

Halaman 4-5 masih memuat petunjuk template hasil/pembahasan, contoh tabel spesifikasi panel, dan petunjuk kesimpulan. Halaman 5-6 juga masih berisi petunjuk lampiran. Karena itu, PDF tersebut belum menyediakan hasil eksperimen final yang dapat dicocokkan angka demi angka.

Penomoran referensi di pendahuluan juga perlu diperiksa: pembahasan UWB menunjuk [5], sedangkan daftar pustaka menempatkan Ziegler/UWB pada [2]; pembahasan edge He menunjuk [7], sedangkan daftar pustaka menempatkannya pada [1]. Ini temuan konsistensi internal PDF, bukan verifikasi bibliografi eksternal.

## Longsor acak yang dapat diulang

Skenario default sekarang **Random**. Daftar lokasi detektor dibuat deterministik dari denah: tiga lokasi lama dipertahankan sebagai ID 0-2, lalu kandidat lain ditambahkan dengan jarak antartitik hingga maksimum 12 lokasi. Kandidat tambahan menjauhi spawn dan zona aman serta memiliki sisi dinding untuk pemasangan perangkat.

Sebelum aktor berjalan, generator memilih jadwal kejadian dengan `System.Random(seed)`. Setiap kejadian memakai lokasi berbeda. Lokasi pertama dipilih acak pada rute awal jika tersedia kandidat yang, ketika ditutup, masih menyisakan alternatif dari spawn. Kejadian berikutnya dipilih acak dari kandidat lain. Jadi ini **sampling terkontrol untuk menguji reroute**, bukan pemilihan seragam atas seluruh sel tambang.

Default: tiga kejadian, peringatan pertama pada 6-8 detik, jeda peringatan ke longsor 4 detik, dan peringatan berikutnya 6-8 detik setelah longsor sebelumnya. Kejadian yang dijadwalkan setelah pekerja mencapai hasil akhir tidak dieksekusi, tetapi tetap dicatat dalam file skenario.

Lokasi tidak dipilih ulang berdasarkan keputusan navigasi pekerja. Jadwal adaptif dan statis sama jika seed, denah, dan parameter sama. Longsor tidak berpindah atau hilang setelah terjadi; lokasi kejadian baru yang berbeda menambah penutupan pada map.

Tidak ada jaminan semua skenario dapat diselesaikan. Longsor berikutnya dapat memutus seluruh akses atau terjadi di posisi aktor. Hasil yang benar adalah `Blocked`, bukan menghapus longsor agar pekerja pasti berhasil. Menutup lokasi tetap atau perintah WebSocket eksplisit juga dapat menghasilkan kondisi tersebut.

### Kontrol skenario

| Pengaturan | Arti |
|---|---|
| Random Seed On Launch | Saat masuk Play, pilih seed baru sekali; default aktif |
| Scenario Seed | Seed yang digunakan untuk sesi berikutnya; matikan Random Seed On Launch untuk seed eksperimen tetap |
| Scenario Mode = Random | Lokasi dan waktu acak terkontrol |
| Scenario Mode = Scripted | Kontrol lama: D01 waspada pada 6 s/tertutup 10 s; D02 waspada 19 s/tertutup 23 s |
| Scenario Mode = NoHazards | Kontrol tanpa kejadian longsor otomatis |
| Random Event Count | Jumlah kejadian, dibatasi jumlah lokasi detektor tersedia |
| First Warning Seconds | Waktu dasar peringatan pertama, minimum efektif 4 s; jitter 0-2 s |
| Seconds Between Events | Jeda dasar dari longsor ke peringatan berikutnya, minimum efektif 2 s; jitter 0-2 s |
| Warning Duration Seconds | Jarak waktu peringatan ke longsor, minimum efektif 2 s |
| Warning Risk Penalty | Tambahan biaya planner ketika memasuki sel waspada; default 24, minimum efektif 0 |

Menu menyediakan **Acak skenario baru** dan pergantian **Acak / Tetap / Tanpa bahaya**. Seed terlihat di menu dan panel hasil. Tombol **R**, tombol ulang, serta mulai kembali dari menu memakai seed yang sama. Untuk variasi baru, kembali ke menu lalu pilih Acak skenario baru. Parameter dibaca saat sesi dimulai; mengubahnya di tengah sesi tidak mengganti jadwal aktif.

## Satu aset detektor, dipasang berulang di tunnel

Aset reusable: `Assets/Resources/Mining/LandslideDetector.prefab`. Model terdiri atas enclosure kuning, probe getaran, antena, display, kisi sirene, dan lampu indikator. Material pendukung disertakan bersama prefab; tidak membutuhkan unduhan aset luar.

Setiap titik kandidat memiliki satu instance di dinding, dengan ID **D01, D02, ...**. Penempatannya mengikuti denah sehingga beberapa bagian tunnel memiliki perangkat yang sama. Semua perangkat tetap terlihat meskipun kondisinya normal.

| Status | Peringatan yang terlihat / terdengar | Efek navigasi |
|---|---|---|
| Normal | Lampu hijau, display NORMAL | Biaya jarak biasa |
| Waspada | Lampu kuning berkedip, display AWAS LONGSOR, bunyi berkala dari posisi perangkat | Adaptif memperhitungkan penalti risiko dan mencari alternatif |
| Tertutup | Lampu merah berkedip, display JALUR TERTUTUP, sirene lebih sering, batu dan collider aktif | Sel dilarang untuk planner adaptif; baseline berhenti sebelum batu |

HUD menampilkan ID dan koordinat detektor terakhir yang berubah. Minimap memperlihatkan lokasi perangkat serta status kuning/merahnya. Radio tetap menjadi narasi pendamping. Sirene bersifat spasial: perangkat dekat terdengar lebih kuat. Audio peringatan berhenti pada jeda/hasil akhir; perubahan visual menggunakan waktu simulasi.

Detektor adalah **sensor virtual** yang membaca keadaan skenario, bukan algoritma prediksi longsor dari data getaran riil. Tidak ada klaim akurasi deteksi, percepatan getaran, ambang geoteknik, atau sertifikasi perangkat. Tampilan sengaja memakai status, bukan angka pengukuran fisik fiktif.

Pratinjau editor memakai model yang sama. **Rebuild Editor Preview** memperbarui posisi perangkat untuk denah yang diubah. Toggle contoh longsor menampilkan satu lokasi contoh, bukan mengaktifkan seluruh kandidat sekaligus.

## Planner dan cara pekerja menemukan tempat aman

Mode Cerita tetap menggerakkan pekerja otomatis. Pada status bahaya berubah, planner menggunakan posisi pekerja saat ini dan mencari rute menuju salah satu zona aman. Biaya transisi ke sel tetangga:

`cost = 6 meter + warningRiskPenalty jika sel tujuan waspada`

Sel tertutup tidak masuk pencarian. Dengan penalti aktif, pencarian menggunakan Dijkstra; jika tidak ada sel waspada, biaya setiap langkah sama dan BFS menghasilkan solusi setara. Penalti 24 merupakan bobot eksperimen setara biaya tambahan empat langkah, **bukan jarak fisik tambahan atau ukuran probabilitas cedera**.

Peringatan menaikkan biaya tetapi bukan larangan mutlak. Jika alternatif lebih mahal atau tidak ada, planner masih dapat memilih sel waspada. Setelah status tertutup, sel tidak boleh dilalui. Evaluasi harus mencatat jika pekerja gagal atau terpapar; penalti risiko tidak menjamin keselamatan semua skenario.

Baseline menyimpan jalur awal dan tidak merencanakan ulang saat peringatan/longsor. Kedua metode tetap menerima tampilan detektor yang sama. Definisi reroute diperbarui: jumlah perubahan **sisa urutan rute atau target exit** akibat pembaruan bahaya, mengabaikan bagian rute yang sudah dilalui. Angka ini tidak langsung sebanding dengan log lama yang memakai definisi lebih terbatas.

## Menjawab research problem dengan eksperimen

Pertanyaan utama tetap: apakah pembaruan jalur dari posisi pekerja berdasarkan kondisi terbaru meningkatkan kemampuan mencapai zona aman dibanding rute yang dipertahankan sejak awal?

1. Tetapkan beberapa denah dan daftar seed sebelum eksperimen. Gunakan Random Seed On Launch = false untuk replay antar Play.
2. Jalankan setiap pasangan denah/seed dua kali: adaptif dan statis, dengan kecepatan, penalti, serta jadwal identik. Jalankan juga kontrol NoHazards dan Scripted.
3. Ekspor setelah `Success` atau `Blocked`. Jangan membandingkan waktu baseline yang gagal seolah waktu evakuasi berhasil. Kumpulkan seluruh seed yang telah dipilih, termasuk kegagalan.
4. Bandingkan tingkat keberhasilan, kejadian masuk area bahaya, waktu dan jarak sesi berhasil, jumlah reroute, serta waktu komputasi. Untuk analisis efek penalti risiko, lakukan variasi bobot yang dicatat eksplisit.
5. Analisis event untuk memastikan warning mendahului longsor, detektor sesuai status, dan rute adaptif tidak memasuki sel tertutup. Pisahkan eksperimen offline dan jaringan.

Setiap ekspor kini menghasilkan:

- `_summary.csv`: metrik sesi, jenis skenario, dan seed.
- `_events.csv`: kejadian aktual, pembaruan rute, dan outcome.
- `_layout.csv`: sel denah aktual.
- `_scenario.csv`: jadwal penuh, indeks detektor, koordinat, waktu warning dan collapse.
- `_config.json`: seed, jenis skenario, versi Unity, identitas planner, penalti risiko, ukuran sel, kecepatan, radius paparan, spawn/exit, lokasi detektor, dan jadwal.

Jadwal hasil ekspor lebih kuat untuk audit daripada seed saja: perubahan generator atau versi runtime dapat mengubah hasil seed. Saat ini file konfigurasi adalah keluaran dokumentasi, belum memiliki tombol impor replay otomatis. Simpan juga versi kode/commit dan spesifikasi mesin pada catatan eksperimen.

Kalimat pembahasan yang sesuai dengan implementasi:

> Sistem mengubah data kondisi dari detektor virtual menjadi status dan biaya jalur, lalu menghitung ulang rute dari posisi pekerja ketika informasi bahaya berubah. Skenario acak yang dapat diulang memungkinkan pembandingan adaptif dan statis dengan input lingkungan yang sama. Pengujian mengevaluasi keberhasilan mencapai zona aman dan respons terhadap perubahan keterlintasan; hasil ini merupakan bukti simulasi, bukan validasi perangkat deteksi atau edge fisik.

## Pekerjaan yang masih diperlukan untuk memenuhi paper sepenuhnya

- Implementasi planner/modul pemrosesan Python jika arsitektur tersebut tetap diklaim di metode; alternatifnya, sesuaikan metode paper secara eksplisit menjadi prototipe pemrosesan lokal C#.
- Instrumen pengukuran end-to-end jika istilah waktu respons sistem mencakup komunikasi, penerimaan data, keputusan, dan tampilan.
- Eksperimen lintas denah/seed dengan data nyata dari sesi, tabel/grafik, analisis kegagalan, serta kesimpulan berdasarkan hasil tersebut.
- Penyelarasan istilah UWB virtual, bobot risiko, satu pekerja, dan validasi awal agar paper tidak mengklaim fitur fisik/model yang belum diuji.
- Penggantian seluruh isi template hasil, kesimpulan, dan lampiran, serta pembetulan nomor referensi.

## Hasil validasi update

[Log validasi terbaru](Validation/random-detectors.txt) dihasilkan oleh Unity 6000.3.23f1 pada salinan proyek dengan rendering. Pemeriksaan fungsi selesai dan proses keluar dengan kode 0:

- 64 seed: replay jadwal identik, lokasi/waktu bervariasi, lokasi kejadian tidak duplikat, serta warning mendahului penutupan.
- Empat pasangan seed 101-104: adaptif `Success` pada seluruh empat uji, statis `Blocked` pada seluruh empat uji, dan jadwal pasangan identik. Ini sampel regresi terkontrol, bukan estimasi keberhasilan pada semua skenario.
- Status perangkat sesuai status bahaya pada setiap frame uji acak; jalur adaptif tidak memuat sel tertutup. Prefab memiliki display, lampu, dan komponen audio lokal.
- Penalti waspada mengubah rute sebelum penutupan; mode NoHazards menghasilkan jadwal kosong.
- Pemeriksaan graf, geometri/collision, pembatasan Story, reset, ekspor, dan hasil tanpa rute tetap lulus.
- Ekspor terbaru diperiksa: seed 104, Random, 12 lokasi detektor, tiga kejadian, dan penalti risiko 24 tersimpan dalam konfigurasi JSON.

Gambar menu, cerita, dan status normal/waspada/tertutup diperiksa secara visual. Teks alat diperkecil agar muat di display serta memakai depth test agar tidak terlihat menembus dinding. Komponen audio diuji keberadaannya; kualitas suara belum dinilai melalui uji dengar. Pengujian WebSocket tidak diulang dalam update ini.

Log editor masih mencatat exception startup pada `UnityEditor.Search.SearchDatabase`; stack trace berasal dari indeks pencarian editor, bukan skrip simulasi. Semua pemeriksaan fungsi tetap selesai. Log ini tidak diklaim bebas seluruh exception editor.

![Detektor virtual dalam status waspada](Previews/detector-warning.png)

![Cerita dengan jaringan detektor, status bahaya, dan petunjuk rute](Previews/random-story.png)

Gambar cerita di atas diambil pada kontrol Scripted untuk memeriksa tampilan pada waktu yang tetap; mekanisme Random diuji pada pasangan seed di log.
