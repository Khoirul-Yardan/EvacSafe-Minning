# Probe Unity untuk pengujian dan gambar

Skrip C# sekali pakai yang dijalankan di Unity Editor lewat Coplay MCP (`execute_script`), bukan bagian proyek. Folder ini berada di luar `Assets/`, jadi Unity tidak meng-compile-nya. Tidak ada yang menyimpan perubahan ke scene.

Konstanta `Dir` dan `Log` menunjuk ke folder scratchpad sesi lama; ganti ke folder lokal sebelum dipakai. Setiap pemanggilan `execute_script` meng-compile assembly baru, jadi nilai statis tidak terbawa antarpanggilan; probe yang berjalan lama menulis hasil ke berkas.

| Berkas | Fungsi |
|---|---|
| `Refresh.cs` | `AssetDatabase.Refresh()` setelah berkas diubah di luar Unity |
| `ResProbe.cs` | Mengganti resolusi Game view (1280×720, 1366×768, 1280×800, 1024×768, 1920×1080), membekukan waktu saat status tertentu, mengambil screenshot |
| `UiProbe.cs` | Memulai sesi Cerita atau FPP, jeda, menekan tombol UI berdasarkan nama |
| `PaperShots.cs` | Sesi MQTT skenario Terkontrol adaptif dan statis; screenshot otomatis pada rute awal, waspada, tertutup, rute baru, hasil |
| `CustomProximity.cs` | Ilustrasi Gambar 5: menutup detektor di samping agen lewat jadwal pembanding |
| `SeedSearch.cs` | Mencari seed acak dengan kontak bahaya (edge lokal, waktu dipercepat 8×) |
| `AnimTrace.cs` | Mencatat perubahan state Animator karakter per tahap simulasi dan mengambil screenshot tiap pose |
| `WorkerCloseup.cs` | Render kamera sementara (samping atau posisi kamera utama) untuk mengecek karakter tanpa UI |

Catatan: nama `SessionState` bentrok dengan `UnityEditor.SessionState`; tulis `SafeMining.SessionState`. Coplay tidak menampilkan pesan error compile; jika gagal tanpa pesan, salin sementara ke `Assets/.../Editor/` untuk membaca error, lalu hapus lagi.
