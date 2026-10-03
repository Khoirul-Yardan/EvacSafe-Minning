# Skrip Blender untuk karakter pekerja

Dijalankan tanpa jendela dengan Blender 5.2 (versi Steam di laptop Bagus: `D:/Steam/steamapps/common/Blender/blender.exe`):

```bash
blender -b --factory-startup --python Tools/Blender/<skrip>.py -- <argumen>
```

| Berkas | Fungsi |
|---|---|
| `assemble_worker.py` | Merakit `SafeMiningWorker.fbx`: model miner, kacamata, dan lampu helm di tulang `head`, membuang rambut dan animasi bawaan, menghapus akhiran angka nama tulang, menerapkan transform root setelah pose di-reset, mengekspor FBX untuk Unity Humanoid, dan me-render preview. Path sumber (`miner_src`, berkas unduhan Sketchfab) menunjuk ke scratchpad sesi lama; sesuaikan sebelum dipakai |
| `inspect_any.py` | Ringkasan FBX atau GLB: mesh, jumlah segitiga, ukuran, material, tekstur, render preview |
| `anim_cycles.py` | Untuk klip Mixamo: panjang, drift, sambungan loop, dan frame injakan kaki kiri |
| `jog_window.py` | Mencari potongan satu langkah dengan sambungan loop terkecil (dipakai untuk memotong Jog ke frame 24 sampai 49 Blender, 23 sampai 48 Unity) |

Pilihan ekspor FBX yang penting (hasil riset, lihat `Documentation/RISET_PIPELINE_KARAKTER_BLENDER_2026-09-30.md`): `apply_scale_options="FBX_SCALE_ALL"`, `add_leaf_bones=False`, `bake_anim=False`, `use_mesh_modifiers=False`, `mesh_smooth_type="OFF"`. Reset pose bone ke identitas sebelum menerapkan transform armature, atau mesh akan terpelintir. Blender 5.x tidak lagi membaca `.dae`; unduh glTF atau FBX.
