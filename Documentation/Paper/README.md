# Naskah EPW 17

Sumber naskah LaTeX dan gambarnya. Isi teks mengikuti naskah Google Docs tim ([dokumen](https://docs.google.com/document/d/11WLj6MrWutV_Ag-3Kdc5unds1T4jp1-tRFhsmWWU8Os/edit)) per 3 Oktober 2026.

## Berkas

| Berkas | Isi |
|---|---|
| `document.tex` | Naskah |
| `figures/` | Gambar 3 sampai 5 (JPEG); Gambar 1 dan 2 adalah TikZ di dalam `document.tex` |
| `build/` | Hasil build (diabaikan git) |

## Sinkronisasi dengan Google Docs

Revisi 3 Oktober yang sudah diterapkan: paragraf penutup Pendahuluan ditulis ulang (fokus pada alur deteksi di sisi edge, perhitungan ulang rute, dan pembaruan arah; kontribusi berupa kelayakan), paragraf algoritme ("shortest-path algorithm yang sudah umum digunakan"), "antar komponen", kata kunci tanpa titik, caption Gambar 3, judul "Referensi", pustaka [8] sampai [11] diganti (Saha dkk., Haam dkk., Fonseca i Casas, Huang dan Cetinkaya), nomor terbitan Ziegler dkk., dan subbagian "Identitas paket eksperimen" di Lampiran dihapus. Angka, tabel, dan persamaan tidak berubah.

Yang sengaja **tidak** diikuti dari Google Docs:

| Bagian | Google Docs | LaTeX | Alasan |
|---|---|---|---|
| Caption Gambar 4 | "pada seed 101 … mengalihkan rute ke zona aman 2" | Skenario terkontrol, waktu panel bukan data tabel | Panel di Google Docs adalah tangkapan versi terbaru (sub-caption menunjukkan 62,7 s dan zona aman 3), bukan seed 101 |
| Caption Gambar 5 | "pada seed 106 … penutupan D02 … 0,264 s" | Ilustrasi penutupan D12 yang diatur manual | Panel di Google Docs adalah ilustrasi D12 (30,4 s dan 57,6 s), bukan seed 106 |

**Perlu dicek tim (belum diubah, sama dengan Google Docs):**
- Bagian Cakupan interpretasi masih menulis "validasi simulasi oleh Sargent [10]" dan "pedoman STRESS [11]", padahal pustaka [10] dan [11] kini Fonseca i Casas serta Huang dan Cetinkaya.
- Isi naskah masih menyebut Gambar 4 sebagai dokumentasi seed 101 dan merujuk Gambar 5 dari contoh seed 106.

## Asal gambar

| Gambar | Asal | Sama dengan data tabel? |
|---|---|---|
| 3 | (a) denah dari naskah Google Docs, (b) tangkapan karakter baru | Tidak memuat angka |
| 4 | Diambil 30 September dengan sumber MQTT (broker dan edge Python di Docker), skenario Terkontrol, kode setelah `main2` (`Tools/UnityProbes/PaperShots.cs`) | Tidak: waktu panel 3,0; 10,5; 62,7; 17,1 s |
| 5 | Ilustrasi: D12 ditutup manual lewat jadwal pembanding saat agen sekitar 4 m darinya (`Tools/UnityProbes/CustomProximity.cs`) | Tidak |

Kode sekarang (Yardan, `main2`) memakai generator skenario baru dan jeda pemeriksaan di Mode Cerita, sehingga seed 101 dan 106 tidak lagi menghasilkan kejadian eksperimen; commit eksperimen `62be1a5a` tidak ada di repo. Pada 26 seed acak (101 sampai 126) tidak ada sesi adaptif dengan kontak, karena pekerja selalu berhenti sebelum longsor.

## Build

PDF (MiKTeX, sekali online untuk mengunduh paket):

```bash
cd Documentation/Paper
pdflatex document.tex
pdflatex document.tex
```

DOCX (Times New Roman, A4, persamaan Word, tabel booktabs, gambar dengan sub-caption):

```bash
python Tools/Paper/render_tikz.py
python Tools/Paper/tex2docx.py Documentation/Paper/build/document.docx
```

Nomor gambar, tabel, dan sitasi di DOCX adalah teks biasa, bukan field Word, jadi buat ulang DOCX setiap `document.tex` berubah. Untuk menyinkronkan lagi dengan Google Docs, ekspor `.../export?format=docx` dari tautan dokumen dan bandingkan per paragraf sebelum mengubah `document.tex`.
