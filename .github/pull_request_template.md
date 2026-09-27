## Ringkasan

<!-- Apa yang berubah dan kenapa. Sebut ID tugas bila ada (A1-A7, B1-B7, T0). -->

## Cara menguji

<!-- Mode (Cerita/FPP), navigasi, skenario, seed, sumber bahaya (MQTT/edge lokal/legacy), dan hasil yang diamati. -->

## Checklist

- [ ] Dibuka dan dites di Unity **6000.3.23f1**; `ProjectVersion.txt` tidak berubah.
- [ ] Tidak ada perubahan `ProjectSettings/` atau `Packages/` tanpa alasan yang dijelaskan di atas.
- [ ] Aset baru dibuat lewat Unity sehingga `.meta` ikut ter-commit; tidak ada `.meta` yang dihapus.
- [ ] Scene utama hanya diubah oleh integrator, atau perubahan scene dijelaskan.
- [ ] Tampilan tidak mengklaim hal yang tidak terjadi: getaran berlabel SIM, "diterapkan" hanya setelah `HazardApplied`, sumber bahaya terlihat, tidak ada klaim perangkat keras.
- [ ] Dokumentasi dan bukti di `Documentation/Validation/` atau `Documentation/Previews/` diperbarui bila perilaku berubah.
- [ ] CI hijau.
