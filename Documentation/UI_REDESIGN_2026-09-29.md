# Desain ulang UI: fondasi, menu, HUD, dan panel hasil

29 September 2026. Lanjutan tugas Orang 2 (tampilan): fondasi visual, menu simulasi, HUD saat bermain (termasuk bar getaran dan tahapan deteksi B3/B4 serta panel kamera longsor), dan panel jeda/hasil.

## Alasan

Masukan tim: UI lama terasa seperti hasil generator. Pola penyebabnya: semua informasi dibungkus kotak yang sama tanpa hierarki, label kapital kecil berwarna amber di setiap panel, teks teknis panjang dengan pemisah `·`, `|`, `>` untuk penonton, informasi yang sama tampil berulang, font bawaan Unity (Arial), serta tombol dengan gaya berbeda-beda.

## Bahasa visual

**Warna** mengikuti makna warna keselamatan ISO 3864 / ISO 7010, sama dengan lampu di dunia 3D:

| Token | Warna | Arti |
|---|---|---|
| `Safe` | `#2FD27F` | Aman, rute evakuasi, aksi utama |
| `Warning` | `#FFC21A` | Waspada |
| `Danger` | `#FF5A4E` | Tertutup, terhalang |
| `Info` | `#5AA9FF` | Data sistem: sumber deteksi, koneksi |
| Netral | `#0E1113`, `#161B1F`, `#2C353B`, `#9AA5AD`, `#E8ECEF` | Latar, panel, garis, teks sekunder dan utama |

Aturan: warna status hanya dipakai untuk makna, tidak pernah sebagai hiasan; status selalu disertai ikon dan teks agar terbaca oleh penonton buta warna; pilihan yang aktif memakai kontras netral, bukan hijau. Seluruh pasangan teks dan latar memenuhi kontras WCAG AA (terendah 5,6:1).

**Huruf**: Barlow untuk teks, Barlow Semi Condensed untuk judul, JetBrains Mono untuk angka yang berubah agar tidak bergoyang. Ukuran minimum 16 pada resolusi acuan 1600×900.

**Ikon**: Tabler Icons 3.48.0 (MIT) yang dipangkas menjadi 43 ikon (12,5 KB). Ikon hanya dipasang bila mempercepat pengenalan: status, arah, aksi, sumber data, dan jenis angka.

## Berkas

| Berkas | Isi |
|---|---|
| `Assets/Scripts/MiningUiStyle.cs` | Token warna, ukuran, font, sprite sudut membulat |
| `Assets/Scripts/MiningUiKit.cs` | Komponen bersama: panel, label, ikon, chip, tombol, kontrol segmen |
| `Assets/Scripts/MiningMenuPanel.cs` | Menu simulasi |
| `Assets/Scripts/MiningHudView.cs` | HUD saat bermain: status, banner bahaya, panel deteksi, peta dan waktu, kamera longsor, radio, arah, petunjuk tombol |
| `Assets/Scripts/MiningResultPanel.cs` | Panel jeda, hasil, dan perbandingan adaptif/statis |
| `Assets/Scripts/MiningIcons.cs` | Nama ikon, dihasilkan skrip, jangan diedit manual |
| `Assets/Resources/Fonts/` | Font dan lisensi OFL/MIT |
| `Tools/UI/build_icon_font.py` | Membangun ulang font ikon; tambah nama ikon ke daftar lalu jalankan |

`MiningHUD.cs` kini hanya membuat canvas, berpindah antara menu, HUD, dan panel hasil, serta menyimpan peta mini `MineMapGraphic`. Perilaku simulasi, planner, edge, dan MQTT tidak berubah: menu menulis pilihan skenario dan sumber ke `MiningSimulation` lalu memanggil `Begin`, sama seperti sebelumnya. Logika perbandingan dengan seed sama dari `feat/irawan` dipertahankan.

## HUD saat bermain

| Area | Isi |
|---|---|
| Kiri atas | Mode dan navigasi, status utama (misalnya "Waspada · D01"), sumber deteksi dan kondisi koneksi, perlengkapan FPP |
| Tengah atas | Banner bahaya dengan garis warna status dan rute saat ini; hanya muncul bila ada lorong waspada atau tertutup |
| Kiri, di bawah status | Panel deteksi (B3/B4). Selalu tampil di Cerita; di FPP dibuka lewat "Detail sistem" |
| Kanan | Peta mini dengan legenda ikon, waktu simulasi, jumlah perubahan rute, lalu panel kamera longsor saat tampil |
| Bawah | Radio tim, petunjuk arah dengan panah, petunjuk tombol berbentuk keycap |

Panel deteksi memilih perangkat yang paling relevan: tertutup, lalu waspada, lalu getaran di atas ambang waspada, dengan perubahan terbaru didahulukan. Isinya bar getaran simulasi (skala 0 sampai 1, tanpa satuan fisik) dengan penanda ambang waspada dan tertutup, serta tiga tahapan yang benar-benar diamati Unity:

| Sumber | Tahap 1 | Tahap 2 | Tahap 3 |
|---|---|---|---|
| MQTT edge | Sampel getaran dikirim ke broker | Status diterima dari edge Python | Diterapkan ke lorong dan rute |
| Edge lokal | Sampel dibaca edge lokal | Edge lokal memutuskan | Diterapkan ke lorong dan rute |
| Jadwal pembanding | Jadwal mencapai waktu kejadian | Status ditetapkan jadwal | Diterapkan ke lorong dan rute |

Keputusan di dalam edge Python tidak terlihat dari Unity, sehingga tahap 2 pada MQTT hanya dicentang setelah statusnya diterima. Bila broker terputus atau data terlambat, panel menampilkan peringatan dan status terakhir dipertahankan. Setelah pulsa getaran selesai, bar dapat kembali rendah sementara lorong tetap tertutup; panel menjelaskan hal ini secara eksplisit.

Panel kamera longsor tetap dikendalikan `MiningLandslideCutscene` tanpa perubahan; HUD hanya menggambar ulang judul dan keterangan dari `ActiveDeviceIndex` dan `PendingCount`.

## Perilaku yang perlu diketahui

- Panel hasil menampilkan alasan hasil (misalnya "Rute statis tertutup longsor"), tiga angka utama, dan badge kualitas data yang selalu terlihat bila MQTT sempat putus atau data tidak lengkap.
- Seed, sumber, ID sesi, dan waktu hitung rute ada di "Lihat detail sistem". Waktu hitung tetap diberi keterangan sebagai komputasi lokal, bukan latensi jaringan.
- Menu menjelaskan setiap pilihan dalam satu kalimat, termasuk bahwa mode MQTT membutuhkan broker dan edge Python di Docker.
- Latar di belakang menu dan panel diredupkan sekitar 75 persen. Proyek memakai ruang warna linear, sehingga nilai alpha 0,95 diperlukan untuk efek tersebut.

## Pengujian

Diuji di Unity 6000.3.23f1, Game view 1920×1080, melalui Play mode: menu (pilih FPP dan edge lokal, lalu mulai), HUD Cerita saat waspada dan tertutup dengan kamera longsor, HUD FPP dengan detail sistem, panel jeda Cerita dan FPP termasuk pengaturan kamera, hasil berhasil, hasil terhalang, dan perbandingan adaptif lawan statis. Tidak ada error compile atau runtime. Jalur MQTT dengan broker nyata, resolusi 1280×720 dan 16:10, serta build player belum diuji.

## Catatan untuk runner validasi

`Assets/Scripts/Editor/MiningExperienceBuilder.cs` baris 211 mencari tombol `"Mulai Mode FPP  >"`. Tombol itu sudah tidak ada sejak menu diubah pada commit `5bbfac1`, sehingga uji "FPP menu button" gagal sebelum pekerjaan ini. Berkas tersebut milik Khoirul-Yardan dan tidak diubah di sini. Menu baru menyediakan nama objek yang stabil untuk perbaikannya: klik tombol bernama `FPP mode`, lalu `Start simulation`. Tombol panel hasil bernama `Primary action` dan `Secondary action`.
