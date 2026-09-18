# SAFE-MINING EVAC

Simulasi evakuasi tambang Unity dengan **Mode Cerita** dan **Mode FPP**, navigasi kacamata AR, longsor dinamis, dan pembanding rute statis sesuai abstrak PENS.

1. Buka `Assets/Scenes/SafeMining_Experience.unity` di Unity 6000.3.23f1.
2. Tekan **Play**, lalu pilih mode.
3. FPP: **WASD + mouse** atau **gamepad**. **Esc** untuk jeda, **R** untuk ulang, **G** untuk kacamata.

[Panduan, skenario, kesesuaian abstrak, dan WebSocket](Documentation/SAFE_MINING.md)

[Hasil pengujian gameplay](Documentation/Validation/final-gameplay.txt) · [Pengujian WebSocket](Documentation/Validation/websocket-and-gameplay.txt)

Model dan map menggunakan geometri prosedural bergaya sederhana. Denah diuji untuk konektivitas jalur dan collision; tampilannya bukan rekonstruksi fotorealistis ilustrasi referensi.

![Pilihan mode](Documentation/Previews/menu.png)

![Mode cerita](Documentation/Previews/story.png)

![Mode FPP dengan longsor aktif dalam pemeriksaan visual](Documentation/Previews/fpp.png)
