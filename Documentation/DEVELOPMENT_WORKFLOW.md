# Alur kerja pengembangan, CI, dan aturan branch

Dibuat 27 September 2026. Berlaku untuk seluruh anggota tim sampai submission 30 September, lalu diperketat pada tahap 2.

## Alur kerja harian

1. `git pull` di `main` sebelum mulai. Anggota lain dapat push kapan saja.
2. Buat branch pendek per tugas, misalnya `feat/edge-indicator` atau `docs/t0-docker-command`.
3. Buka proyek hanya dengan Unity **6000.3.23f1**. Editor lain menulis ulang `ProjectVersion.txt` dan dapat mengubah `Packages/`; CI akan menolaknya.
4. Commit kecil yang sudah dites, lalu buka Pull Request ke `main` dengan template yang tersedia.
5. Tunggu CI hijau. Pemilik berkas (lihat `.github/CODEOWNERS`) otomatis diminta review.

Sampai 30 September, push langsung ke `main` masih diizinkan agar tim tidak terhambat menjelang deadline. Branch dan PR tetap disarankan sebagai kebiasaan.

## Pemeriksaan CI

Workflow `.github/workflows/ci.yml` berjalan pada setiap push dan PR ke `main`. Tidak membutuhkan lisensi Unity dan tidak mengunduh berkas Git LFS, sehingga kuota bandwidth LFS pemilik repo tidak terpakai.

| Job | Pemeriksaan | Alasan |
|---|---|---|
| Unity repo hygiene and docs | Setiap aset dan folder di `Assets/` punya `.meta`, tidak ada `.meta` yatim | `.meta` yang hilang memutus referensi scene/prefab |
| | `ProjectVersion.txt` tetap 6000.3.23f1 | Mencegah proyek tak sengaja di-upgrade/downgrade |
| | Tidak ada `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.sln`, `.csproj` | Berkas lokal dan hasil build |
| | Berkas ≥50 MB di luar LFS gagal, ≥10 MB diberi peringatan | Batas GitHub dan ukuran clone |
| | Tautan relatif antardokumen Markdown tidak rusak | Dokumentasi dibaca juri dan anggota tim |
| Python edge and broker config | Sintaks skrip Python | |
| | Unit test logika keputusan `Tools/Edge/edge_service.py` | Ambang, durasi minimum, lonjakan singkat, latch longsor, hysteresis, jeda sampel, heartbeat, validasi input |
| | `docker compose config` untuk broker dan edge Python | Compose rusak menggagalkan demo MQTT |

Jalankan pemeriksaan yang sama di komputer sendiri sebelum push. Hanya butuh Python 3, tanpa paket tambahan:

```powershell
python Tools/CI/check_unity_repo.py
python Tools/CI/check_doc_links.py
python -m unittest discover -s Tools/Edge/tests -v
docker compose --profile simulated-edge -f Tools/Mqtt/compose.yaml config --quiet
```

CI tahap ini **tidak** meng-compile C# dan tidak menjalankan runner validasi Unity. Keduanya tetap dijalankan manual di Unity Editor sesuai [panduan edge dan MQTT](EDGE_MQTT.md).

## Ruleset branch `main`

Ruleset hanya dapat diaktifkan oleh admin repo (pemilik: Khoirul-Yardan).

**Tahap 1 (sekarang sampai 30 September):** larang force-push dan penghapusan `main`. Riwayat bersama tidak dapat ditimpa, tetapi push biasa tetap berjalan.

Cara mengaktifkan, pilih salah satu:

- GitHub: **Settings → Rules → Rulesets → New ruleset → Import a ruleset**, pilih `.github/rulesets/main-protection.json`, lalu **Create**.
- Terminal admin: `gh api -X POST repos/Khoirul-Yardan/EvacSafe-Minning/rulesets --input .github/rulesets/main-protection.json`

**Tahap 2 (setelah submission):** edit ruleset yang sama dan tambahkan:

- *Require a pull request before merging*, 1 approval, *Require review from Code Owners*.
- *Require status checks to pass*: `Unity repo hygiene and docs` dan `Python edge and broker config`.
- Job CI Unity (compile, runner validasi, build Windows) memakai GameCI dengan lisensi Unity yang disimpan sebagai secret repo.

## Kepemilikan berkas

`.github/CODEOWNERS` mengikuti pembagian tugas tim:

| Area | Pemilik |
|---|---|
| Edge C#, MQTT client, `MiningSimulation`, skenario, telemetri, denah, scene utama | Khoirul-Yardan |
| Edge Python dan broker | Maaulln, Khoirul-Yardan |
| HUD, cutscene, detektor, resource visual | RockHead07, Maaulln |
| `ProjectSettings/`, `Packages/` | Khoirul-Yardan |
| CI dan aturan repo | Khoirul-Yardan, RockHead07 |

Perubahan pada berkas milik orang lain boleh dilakukan, tetapi dikabarkan dan direview oleh pemiliknya.
