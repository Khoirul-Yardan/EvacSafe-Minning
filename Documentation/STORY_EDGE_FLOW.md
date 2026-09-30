# Mode cerita: berhenti, periksa edge, petakan ulang

Pembaruan 30 September 2026, di atas `main` commit `dfc3caf`.

## Urutan yang terlihat

1. Briefing empat detik, lalu pekerja berjalan beberapa langkah. Pada skenario terkontrol, profil getaran mulai pada detik simulasi 6; posisi pekerja tidak memicu sensor.
2. Sampel getaran yang tinggi membuat pekerja **berhenti sebelum keputusan longsor**. Kamera bergetar ringan, pipa bergetar/berderak, gemuruh terdengar, serta debu dan retakan muncul. Dialog menjelaskan bahwa tanda lingkungan belum memastikan lokasi longsor.
3. Pekerja tetap diam sambil memeriksa laporan edge. Clock simulasi, sensor, transport, dan kejadian longsor terus berjalan; ini bukan pause seluruh simulasi. Status waspada belum dianggap konfirmasi longsor.
4. Setelah laporan tertutup diterima dan divalidasi, dialog menyebut ID perangkat dan grid. Kamera lokasi memperlihatkan longsor selama lima detik per perangkat, termasuk antrean beberapa lokasi. Pekerja tetap diam.
5. Setelah semua lokasi yang memicu pemeriksaan mendapatkan konfirmasi tertutup atau normal stabil, sistem menampilkan hasil pemeriksaan, lalu tahap pemetaan. Planner adaptif menghitung dari posisi pekerja yang berhenti dengan menghindari sel tertutup.
6. Setelah rute tersedia, tampilkan tahap jalur siap selama 1,5 detik, kemudian pekerja berjalan kembali. Getaran baru dapat menghentikannya lagi.

Petunjuk AR, garis rute minimap, dan jarak tujuan ditahan selama pemeriksaan agar rute sebelumnya tidak ditampilkan seolah sudah terverifikasi. Penanda detektor dan lorong tertutup tetap terlihat. Mode FPP tetap dikendalikan pengguna.

Tanda retakan, debu, dan derak pipa adalah **visualisasi lingkungan dalam simulasi**, bukan sensor retakan/pipa tambahan dan bukan pengukuran fisik. Hanya laporan status yang diterima melalui pipeline aktif yang dapat menutup lorong. Retakan dekat pekerja menunjukkan tanda lingkungan; lokasi longsor tetap ditentukan laporan perangkat.

## MQTT dibandingkan alur sebelumnya

| Pilihan sumber | Pembacaan/keputusan | Perhitungan rute |
|---|---|---|
| Jadwal pembanding (`LegacyTimeline`) | Jadwal langsung menetapkan waspada/tertutup; tidak ada pemeriksaan edge nyata | C# di Unity |
| Edge lokal (`LocalEdgeSimulation`) | Sensor virtual dan klasifikasi ambang/durasi dalam Unity, tanpa broker | C# di Unity |
| MQTT (`MqttEdgeSimulation`) | Unity mengirim sampel melalui broker; proses Python mengklasifikasikan dan mengirim status kembali melalui MQTT | Unity meminta rute lewat MQTT, planner Python menghitung dan mengirim hasil |

**MQTT adalah pengantar pesan.** Klasifikasi menentukan status bahaya; planner menghitung jalur. Sensor dan lingkungan masih virtual, termasuk pada mode MQTT. Tahapan cerita yang baru berlaku pada ketiga sumber; label sumber tetap membedakan simulasi lokal, Python lewat MQTT, dan jadwal pembanding.

Untuk mencoba urutan tanpa Docker: pilih **Edge lokal**, skenario **Scripted/terkontrol**, navigasi **Adaptif**, lalu **Mode Cerita**. Latihan terpandu memakai edge lokal dan mempertahankan pilihan skenario menu, termasuk Acak dan Tanpa bahaya. Lihat [perbaikan pilihan Random dan jarak dari bahaya](SAFE_ROUTING_RANDOM.md).

Untuk mencoba proses Python terpisah, aktifkan Docker Desktop lalu jalankan dari root proyek:

```powershell
docker compose --profile simulated-edge -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d --build
```

Pilih MQTT pada simulasi biasa. Broker dan layanan `python-edge` harus sama-sama berjalan. Lihat [kontrak dan transport](EDGE_MQTT.md).

## Kondisi khusus dan evaluasi

- Waspada yang kembali normal stabil dapat melepas pemeriksaan tanpa mengarang longsor. Snapshot normal yang lebih tua daripada getaran terakhir tidak melepas pekerja.
- Beberapa lokasi diperiksa bersama. Konfirmasi satu perangkat tidak melepas pekerja jika perangkat lain masih waspada.
- Data edge kedaluwarsa/hilang menahan pekerja. Tidak ada perpindahan diam-diam ke sumber lokal. Timeout planner MQTT tetap menghentikan sesi dengan alasan navigasi tidak tersedia.
- Navigasi statis menjalani pemeriksaan yang sama, lalu tetap memakai jalur awal. Jika jalur itu tertutup, sesi berakhir terhalang tanpa berjalan menuju longsor atau menghitung rute alternatif.
- Esc membekukan waktu dan tahapan; reset/menu membersihkan pemeriksaan, efek, dan pose pekerja.
- Waktu evakuasi sekarang mencakup waktu berhenti untuk cerita. Jangan membandingkannya langsung dengan hasil cerita sebelum pembaruan ini. `_config.json` menandai `storyPolicy=stop_check_confirm_map_resume_v1`; `_events.csv` mencatat stop, konfirmasi lokasi, pemeriksaan selesai, permintaan pemetaan, dan lanjut berjalan.
- Durasi tahap presentasi bukan latensi komputasi planner. Pada MQTT, `hazard_to_route` mencakup penundaan cerita sejak perubahan status; `planning_ms` tetap waktu pencarian planner.

## Validasi

Runner `MiningStoryValidation.RunBatch` memeriksa beberapa longsor, pemulihan normal, snapshot lama, data kedaluwarsa, getaran baru saat jalur siap, reset, statis/FPP, serta gerakan karakter melalui pipeline edge lokal. Jalankan pada salinan proyek:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -batchmode -nographics -projectPath '<salinan-proyek>' -executeMethod MiningStoryValidation.RunBatch -logFile '<log-validasi>'
```

Hasil ditulis ke `Validation/story-flow-results.txt` di proyek pengujian. Hilangkan `-nographics` untuk turut menyimpan tangkapan layar pemeriksaan. Pengujian ini tidak menggantikan pengujian broker/Python langsung.

[Hasil pengujian Unity 30 September](Validation/story-edge-flow-2026-09-30.txt) dan [tampilan saat pekerja memeriksa lokasi longsor](Previews/story-edge-inspection.png). Semua pemeriksaan alur lulus; 10 unit test pemroses Python juga lulus. Docker belum berjalan saat validasi ini, sehingga koneksi broker/Python tidak diuji langsung. Log editor memuat exception indeks pencarian internal Unity saat startup; kompilasi dan runner alur tetap selesai dengan sukses.
