# Pendekatan pengembangan dan keputusan tim

Catatan keputusan 27 sampai 29 September 2026, menjelang submission EPW 17 tanggal 30 September. Setiap bagian menjelaskan apa yang diputuskan, alasannya, dan dokumen rinciannya. Dokumen ini ditulis agar anggota tim yang melanjutkan pekerjaan tidak perlu menebak arah yang sudah disepakati.

## 1. Pembagian peran dan batas berkas

| Peran | Anggota (akun GitHub) | Area |
|---|---|---|
| Orang 1: getaran, edge, MQTT, integrasi | Khoirul-Yardan | `Edge/`, `Networking/`, `MiningSimulation.cs`, `MineLayout.cs`, skenario, scene utama, runner validasi |
| Edge Python | Maaulln | `Tools/Edge/` |
| Orang 2: tampilan | RockHead07, sebelumnya Maaulln | Menu, HUD, panel hasil, sistem desain UI, font dan ikon |

Keputusan: pekerjaan tampilan tidak mengubah berkas milik Orang 1. Kebutuhan dari berkas tersebut dibaca lewat API publik yang sudah ada (misalnya `DirectionHint()`, `ActiveDeviceIndex`, event sesi edge), bukan dengan mengubah kodenya. Kepemilikan dicatat di `.github/CODEOWNERS`.

Alasan: tim berdiri di atas satu scene dan satu repo menjelang deadline; perubahan silang tanpa koordinasi paling berisiko menimbulkan konflik dan regresi.

## 2. Alur git, CI, dan aturan branch

- Pekerjaan baru di branch lalu masuk lewat Pull Request ke `main`. Push langsung ke `main` masih diizinkan sampai 30 September agar tim tidak terhambat. Pada 3 Oktober push langsung ke `main` dipakai sekali untuk commit lanjutan PR #2 yang sudah di-merge (lihat bagian 8).
- CI tahap 1 berjalan di setiap push dan PR, tanpa lisensi Unity dan tanpa mengunduh Git LFS: pasangan `.meta`, kunci versi Unity 6000.3.23f1, berkas lokal yang tidak boleh ter-commit, ukuran berkas, tautan dokumentasi, sintaks skrip Python, unit test edge Python, dan validasi `docker compose`.
- Ruleset tahap 1 hanya melarang force-push dan penghapusan `main`; diaktifkan oleh admin repo. Tahap 2 (setelah submission) mewajibkan PR, review CODEOWNERS, status check hijau, dan CI Unity dengan GameCI.
- Commit dipisah per perubahan yang bermakna, tanpa atribusi AI, dan berkas yang bukan hasil kerja sendiri tidak ikut di-stage.

Rincian: [Alur kerja tim, CI, dan aturan branch](DEVELOPMENT_WORKFLOW.md).

Alasan: CI penuh dengan compile Unity membutuhkan lisensi dan waktu penyiapan yang tidak tersedia sebelum deadline. Pemeriksaan ringan menangkap kesalahan paling umum di tim Unity dengan biaya hampir nol.

## 3. Lingkungan pengembangan

- Semua anggota memakai Unity **6000.3.23f1**. Editor lain menulis ulang `ProjectVersion.txt` dan dapat mengubah `Packages/`; CI menolak perubahan versi.
- Broker dan edge Python dijalankan lewat Docker dengan profil `simulated-edge`. Tanpa profil itu hanya broker yang menyala dan bahaya tidak pernah diterapkan pada mode MQTT. Image Python perlu di-build sekali saat ada internet sebelum demo. Rincian di [README](../README.md) dan [panduan edge dan MQTT](EDGE_MQTT.md).
- Alat bantu AI di editor (misalnya Coplay) boleh dipasang **hanya di komputer masing-masing**. Perubahan `Packages/manifest.json` dan `packages-lock.json` dikecualikan lokal dengan `git update-index --skip-worktree`, dan folder data lokal alat tersebut dimasukkan ke `.git/info/exclude`. Dengan begitu paket pribadi tidak ikut ter-commit ke tim.

## 4. Aturan kejujuran tampilan

Tampilan tidak boleh mengklaim sesuatu yang tidak terjadi. Enam aturan yang dipakai di seluruh UI:

1. Nilai getaran selalu disebut simulasi dengan skala 0 sampai 1, tanpa satuan fisik.
2. Tahapan deteksi hanya yang benar-benar diamati Unity. Pada MQTT, keputusan di dalam edge Python tidak terlihat, sehingga yang ditampilkan adalah "status diterima", bukan "edge memutuskan".
3. Koneksi MQTT yang putus atau data yang terlambat selalu ditandai; status terakhir dipertahankan, tidak tampil seolah normal.
4. Visual getaran hanya berasal dari nilai sampel yang sebenarnya.
5. Sumber deteksi (MQTT, edge lokal, jadwal pembanding) selalu terlihat, agar panel kamera longsor tidak disangka bukti MQTT.
6. Tidak ada klaim perangkat keras. Sumber `hardware-edge` hanya disebut sebagai kontrak masa depan.

Alasan: paper dan presentasi dinilai juri; tampilan yang berlebihan melemahkan kredibilitas hasil simulasi.

## 5. Sistem desain UI

Masukan tim menyebut UI lama terasa seperti hasil generator: semua kotak sama, label kapital amber di mana-mana, teks teknis panjang, informasi berulang, dan tombol tidak konsisten. Pendekatan yang dipilih adalah sistem desain kecil di atas UI yang dibangun lewat kode, bukan penulisan ulang ke UI Toolkit (terlalu besar untuk deadline) atau poles warna saja (tidak menyelesaikan akar masalah).

- **Warna**: makna warna keselamatan ISO 3864 / ISO 7010, sama dengan lampu di dunia 3D. Hijau aman dan rute, kuning waspada, merah tertutup, biru hanya data sistem. Status tidak pernah hanya bergantung pada warna.
- **Huruf**: Barlow, Barlow Semi Condensed untuk judul, JetBrains Mono untuk angka yang berubah. Minimum 16 satuan.
- **Ikon**: Tabler Icons (MIT) sebagai font yang dipangkas menjadi 43 ikon, dibangun ulang dengan `Tools/UI/build_icon_font.py`. Ikon hanya dipasang bila mempercepat pengenalan.
- **Komponen**: satu kit bersama (`MiningUiKit`) untuk menu, HUD, dan panel hasil, dengan tata letak otomatis agar panel yang muncul dan hilang tidak bertumpuk.
- Font ikon dipilih karena UI dibangun lewat kode dengan komponen `Text`. Praktik terbaik jangka panjang adalah migrasi ke TextMeshPro dengan sprite atlas; dicatat sebagai pekerjaan setelah submission.

Rincian: [Desain ulang UI](UI_REDESIGN_2026-09-29.md).

## 6. Cara menguji tampilan

Setiap perubahan UI diuji langsung di Unity Editor melalui Play mode dengan skenario terkontrol dan edge lokal, lalu diperiksa dari screenshot 1920×1080: menu, HUD Cerita dan FPP pada keadaan waspada dan tertutup, panel kamera longsor, panel jeda, hasil berhasil, hasil terhalang, dan perbandingan adaptif lawan statis. Nilai kontras dan kecerahan diukur, bukan ditebak dari gambar.

## 7. Batasan dan pekerjaan tersisa

| Hal | Status | Pemilik |
|---|---|---|
| Runner `MiningExperienceValidation` mencari tombol lama `"Mulai Mode FPP  >"` (rusak sejak commit `5bbfac1`) | Belum diperbaiki; menu baru menyediakan nama stabil `FPP mode` dan `Start simulation` | Khoirul-Yardan |
| Uji UI pada jalur MQTT dengan broker nyata | Belum; butuh Docker dan image edge Python | Tim |
| Uji resolusi 1280×720, 1366×768, 16:10, dan 4:3 | Selesai 30 September; tiga masalah tata letak di 4:3 diperbaiki | Orang 2 |
| Uji build player | Build Windows 30 September berhasil (sebelum karakter baru); belum diuji ulang dengan karakter | Orang 2 |
| Karakter pekerja ber-rig menggantikan pekerja prosedural | Selesai 30 September dan 1 Oktober, lihat [Karakter pekerja](KARAKTER_PEKERJA_2026-09-30.md); mode FPP belum diuji | Orang 2 |
| Uji UI pada jalur MQTT dengan broker nyata | Dijalankan 30 September (Docker) saat mengambil Gambar 4; HUD menampilkan "MQTT · planner Python edge" dengan benar | Orang 2 |
| Kamera Cerita masuk ke badan karakter saat pekerja berhenti di lorong sempit (skala 1,3) | Belum diperbaiki; pilihan cepat: `MiningWorkerAvatar.Scale` sekitar 1,1 | Orang 2 |
| Isi naskah masih menyebut Gambar 4 seed 101 dan Gambar 5 seed 106 | Menunggu keputusan tim, lihat [README naskah](Paper/README.md) | Tim |
| Tulisan dunia 3D (papan, layar detektor) masih font bawaan | Di luar lingkup, berkas milik Orang 1 | Khoirul-Yardan |
| `Deskripsi lengkap.md` bagian 10 masih menggambarkan HUD lama | Perlu diperbarui pemilik dokumen | Khoirul-Yardan |
| Batas trace MQTT sekitar 3,5 menit karena setiap sampel dicatat dua kali | Belum diubah; sesi Cerita aman, sesi FPP panjang berisiko | Khoirul-Yardan, Maaulln |
| Migrasi ke TextMeshPro dan CI Unity (GameCI) | Setelah submission | Tim |

## 8. Catatan 30 September sampai 3 Oktober

**Git.** PR #1 di-merge Irawan ke `main` (29 September). Branch lokal di-fast-forward ke `main`, lalu ke `origin/main2` milik Yardan (commit `936dd98`: alur Cerita dengan jeda pemeriksaan, routing aman, validasi). Pekerjaan berikutnya dibuat di branch `feat/worker-character` dan PR #2, yang di-merge ke `main` pada 2 Oktober (`ae3932e`) tanpa review dari anggota tim; review otomatis Copilot kosong karena kuota habis. Karena branch berangkat dari `main2`, `main` kini juga memuat `936dd98` milik Yardan. Empat commit lanjutan (animasi, `Tools/`, naskah, catatan ini) masuk langsung ke `main` pada 3 Oktober lewat cherry-pick agar riwayat tetap lurus, atas keputusan Bagus karena PR sudah tertutup dan tidak ada respons tim. CI hijau di setiap langkah.

Alur yang dipakai selama periode ini, untuk diulang bila perlu:

1. `git fetch` dan bandingkan `main` serta branch tim (`main2`, `feat/*`) sebelum mulai; anggota tim juga mengubah berkas UI.
2. Perubahan lokal yang belum di-commit disimpan dengan `git stash push -- <berkas sendiri>`, lalu `git merge --ff-only`, lalu `git stash pop`.
3. Scene utama, `Assets/Settings`, `ProjectSettings`, `Assets/_Recovery`, dan `.vsconfig` tidak pernah di-stage; Unity mengubahnya sendiri saat Play atau build.
4. Sebelum push: `python Tools/CI/check_doc_links.py` dan `python Tools/CI/check_unity_repo.py`.
5. Commit dipisah per unit yang sudah diverifikasi di Play mode, tanpa atribusi AI.

**Karakter pekerja.** Model Sketchfab CC BY (miner, kacamata futuristik, lampu kepala) dirakit di Blender tanpa jendela, diekspor ke FBX untuk Unity Humanoid, lalu dipasang saat Play oleh `MiningWorkerAvatar` tanpa mengubah berkas simulasi atau scene. Animasi bawaan model (pola Mannequin Unreal) dibuang demi lisensi; animasi dari Mixamo. Format terbaik untuk karakter Unity adalah FBX; GLB butuh glTFast yang hanya terpasang lokal lewat Coplay. Rincian: [Karakter pekerja](KARAKTER_PEKERJA_2026-09-30.md), [Riset pipeline](RISET_PIPELINE_KARAKTER_BLENDER_2026-09-30.md), `Tools/Blender/`.

**Animasi.** Pose berdiri dipilih dari tahap Cerita milik Yardan (`StoryStep`): Radio saat briefing dan saat rute dipetakan, Look Around saat getaran dan pemeriksaan jalur serta saat statis terhalang, Injured setelah kontak dengan area tertutup. Jog Mixamo berisi tiga langkah sehingga blend dengan Walk tersendat; dipotong ke satu langkah dan fasenya diselaraskan. Ukuran karakter 2 ditolak karena menutupi jalur, dipakai 1,3.

**Naskah.** Naskah LaTeX disimpan di `Documentation/Paper/` dengan gambar pengganti placeholder, dan isinya disinkronkan dengan revisi Google Docs 3 Oktober (Pendahuluan, pustaka [8] sampai [11], Lampiran). Data pada Gambar 4 dan 5 tidak sama dengan tabel karena logika simulasi sekarang berbeda dari versi eksperimen; caption menyebutnya, isi naskah belum. Versi DOCX dibuat dengan `Tools/Paper/tex2docx.py`. Rincian dan pilihan: [README naskah](Paper/README.md).

**Sinkronisasi naskah.** Google Docs tetap menjadi tempat tim menulis; `document.tex` disamakan dengan cara mengekspor dokumen (`.../export?format=docx`), membandingkan per paragraf dengan isi LaTeX, lalu menerapkan hanya perubahan teks. Caption gambar yang tidak sesuai dengan isi gambar tidak diikuti dan dicatat di README naskah. Yang masih perlu diputuskan Irawan: teks "Sargent [10]" dan "STRESS [11]" yang tidak cocok dengan pustaka barunya, serta rujukan seed 101 dan 106 untuk Gambar 4 dan 5.

**Pengujian tampilan.** Setiap perubahan karakter dan animasi diuji di Play mode lewat Coplay: impor Humanoid diperiksa (52 tulang terpetakan), urutan state Animator dicatat per tahap simulasi (`Tools/UnityProbes/AnimTrace.cs`), dan setiap pose difoto. Jalur MQTT diuji dengan broker dan edge Python di Docker. Build Windows berhasil sebelum karakter baru; build dengan karakter dan mode FPP belum diuji.

**Alat.** Probe Unity untuk resolusi, gambar naskah, dan jejak animasi ada di `Tools/UnityProbes/`. Coplay `execute_script` meng-compile assembly baru setiap panggilan dan tidak menampilkan pesan error compile. Unity dapat mengubah scene utama saat Play atau build; berkas itu (dan `Assets/Settings`, `ProjectSettings`) dikembalikan dengan `git restore`, tidak di-commit. Folder `Assets/_Recovery/` adalah cadangan scene dari Unity setelah crash dan tidak di-commit.
