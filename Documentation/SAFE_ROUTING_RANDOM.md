# Rute menjauh dari bahaya dan pilihan Random

Pembaruan 30 September 2026.

## Rute aman

Sebelumnya planner melarang sel tertutup tetapi tidak memperhitungkan kedekatan dengan longsor. Akibatnya, belokan dekat longsor dapat dipilih meski ada belokan lebih awal yang lebih aman.

Planner adaptif sekarang meminimalkan **akumulasi risiko terlebih dahulu, kemudian jarak**. Risiko menyebar sampai dua langkah sel koridor yang terhubung, bukan menembus dinding. Pada sel waspada, risiko awal mengikuti `Warning Risk Penalty` (bawaan 24), turun setengah per langkah. Pada longsor, risiko awal 16 kali biaya langkah (96), sehingga sel tetangganya bernilai 48 dan sel berikutnya 24. Jika beberapa bahaya berdekatan, gunakan nilai tertinggi per sel. Sel tertutup tetap tidak boleh dilalui; sel penyangga bukan penutupan tambahan sehingga tidak memutus satu-satunya jalur yang masih tersedia.

Pada denah bawaan, jika D01 `(0,4)` dan D02 `(-4,5)` tertutup sementara pekerja masih di bawah persimpangan, rute menuju zona aman kanan berbelok pada `(0,0)`, tanpa mendekati `(0,3)` di sebelah longsor. Jika pekerja sudah berada di `(0,2)`, planner dapat mengarahkan mundur ke persimpangan yang lebih aman. Kondisi cermin di kanan akan memilih jalur kiri, bukan memaksa kanan pada semua skenario.

Keputusan hanya memakai status yang sudah diterima, bukan melihat kejadian masa depan dari jadwal Random. Tanpa bahaya yang diketahui, planner tetap memilih jalur terpendek. Mode statis mempertahankan rute awal sebagai pembanding. Alur Cerita tetap berhenti, memeriksa edge, dan baru memetakan ulang.

Implementasi C# dan Python memakai kebijakan yang sama. Nama planner ekspor menjadi `unity-local-safety-dijkstra-v2` / `python-edge-safety-dijkstra-v2`. `totalCost` pada kontrak tetap jumlah jarak dan penalti untuk audit, sedangkan urutan optimisasi memakai pasangan `(risiko, jarak)`; jangan menafsirkan `totalCost` sebagai satu-satunya nilai yang diminimalkan. Data eksperimen v1 dan v2 perlu dipisahkan.

Untuk MQTT, bangun ulang layanan Python agar versinya sesuai:

```powershell
docker compose --profile simulated-edge -p safe-mining-edge -f Tools/Mqtt/compose.yaml up -d --build
```

## Random

Terdapat dua penyebab pola yang berulang: latihan terpandu mengganti pilihan menu menjadi `Scripted`, dan generator Random mengutamakan kejadian pertama pada rute tengah. Keduanya diperbaiki:

- Pilihan **Acak / Terkontrol / Tanpa bahaya** tetap berlaku pada latihan terpandu maupun simulasi biasa. Latihan terpandu tetap memakai edge lokal; sumber untuk simulasi biasa mengikuti menu.
- Random memilih dari semua lokasi detektor sejak kejadian pertama, tanpa pengulangan lokasi dalam satu jadwal. Waktu mulai juga memiliki variasi deterministik dari seed. Dua sesi masih dapat kebetulan memiliki lokasi awal yang sama.
- Sesi baru dari menu mendapatkan seed baru jika `Random Seed On Launch` aktif. **R/Ulang/Bandingkan** tetap memakai seed dan jadwal yang sama. Seed yang baru dipilih lewat **Acak ulang** dipakai saat mulai, tidak diacak lagi.
- Matikan `Random Seed On Launch` untuk eksperimen dengan seed tetap. HUD menampilkan skenario yang benar-benar aktif; panel hasil dan ekspor menyimpan seed.
- Terkontrol tetap memakai D01 lalu D02. Tanpa bahaya tetap tanpa kejadian otomatis.

## Pengujian

`MiningStoryValidation.RunBatch` kini juga menjalankan `MiningRoutingValidation.Core`: regresi belokan kanan awal, mundur, kasus cermin, zona waspada, kontrak biaya, 128 seed, dan tombol latihan terpandu yang sebenarnya. Sebanyak 32 pasangan request/rute C# diekspor ke `Validation/safety-route-parity.jsonl` untuk diperiksa dengan Python. Pengujian Python ada di `Tools/Edge/tests/test_edge_routing.py`; seluruh suite dijalankan dengan:

```powershell
python -m unittest discover -s Tools/Edge/tests -v
```

[Hasil validasi 30 September](Validation/safe-routing-random-2026-09-30.txt): seluruh pemeriksaan Unity, 18 unit test Python, dan 32 perbandingan request/rute/biaya C#–Python lulus. Pengujian jaringan broker/Python tidak dijalankan ulang pada perubahan ini.
