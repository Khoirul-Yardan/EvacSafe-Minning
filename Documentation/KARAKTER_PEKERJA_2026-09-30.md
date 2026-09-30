# Karakter pekerja: model, animasi, dan pemasangan

30 September 2026. Pekerja prosedural (kapsul dan bola) diganti karakter ber-rig dengan helm, pelindung telinga, coverall reflektif, kacamata pintar, dan lampu helm. Riset pipeline dan sumbernya ada di [Riset pipeline karakter Blender ke Unity](RISET_PIPELINE_KARAKTER_BLENDER_2026-09-30.md).

## Sumber dan lisensi

| Aset | Pembuat | Lisensi | Perubahan |
|---|---|---|---|
| "Man Miner Rig .FBX" (Sketchfab) | Dakta.Grower.Nzl | CC BY 4.0 | Rambut dan animasi bawaan dibuang, akhiran angka nama tulang dihapus, transform root diterapkan |
| "FUTURISTIC GLASSES" (Sketchfab) | MR EXPERT | CC BY 4.0 | Diskalakan ke wajah, lensa diwarnai teal transparan |
| "Duracell Headlamp" (Sketchfab) | Eric Wallbank | CC BY 4.0 | Tali dibuang, dipasang di depan helm, logo merek dihapus dari normal map |
| Idle, Walk, Jog, Run, RunToStop, TalkingToRadio | Adobe Mixamo | Bebas royalti untuk game | Tanpa skin, 30 fps, In Place kecuali RunToStop |

Animasi bawaan model Sketchfab tidak dipakai karena pola namanya (`MM_Idle`, `MM_Walk_Fwd`) sama dengan animasi Mannequin Unreal Engine, yang hanya boleh dipakai di Unreal. Kredit lengkap juga ada di `Assets/Models/Worker/Credits.txt`.

## Berkas

| Berkas | Isi |
|---|---|
| `Assets/Models/Worker/SafeMiningWorker.fbx` | Karakter hasil rakitan Blender; kacamata, lampu, dan `HeadlampSocket` menjadi anak tulang `head` |
| `Assets/Models/Worker/Animations/Mixamo/Worker_*.fbx` | Klip Mixamo, nama klip mengikuti nama berkas |
| `Assets/Models/Worker/Animations/SafeMiningWorker.controller` | Animator: Idle, Locomotion (blend Walk, Jog, Run), Run to stop, Radio |
| `Assets/Models/Worker/Materials/` | Material URP Lit |
| `Assets/Models/Worker/Editor/WorkerCharacterSetup.cs` | Aturan impor (Humanoid, Optimize Game Objects mati, klip loop dan In Place, normal map) dan menu pembangun aset |
| `Assets/Models/Worker/Editor/WorkerEditorPreview.cs` | Menampilkan karakter di EDITOR PREVIEW tanpa mengubah scene |
| `Assets/Resources/Mining/Worker.prefab` | Prefab yang dimuat saat Play |
| `Assets/Scripts/MiningWorkerAvatar.cs` | Memasang dan menganimasikan karakter saat Play |

## Cara kerja

Saat Play, `MiningWorkerAvatar` mencari `PPE worker model` milik `MiningSimulation`, mematikan tampilan bagian prosedural dengan `forceRenderingOff` (tidak tersimpan), lalu memasang prefab di dalamnya. Karena karakter berada di dalam objek tersebut, aturan tampil di Cerita dan sembunyi di FPP tetap diatur `MiningSimulation`. Tidak ada berkas simulasi atau scene yang diubah. Tanpa prefab, komponen ini tidak melakukan apa-apa dan pekerja prosedural tetap tampil.

Kecepatan animasi mengikuti perpindahan `Actor`, dibagi ukuran karakter, sehingga langkah kaki sesuai lantai:

| Keadaan simulasi | Animasi |
|---|---|
| Briefing (detik 0 sampai 4) dan pekerja berhenti untuk pemeriksaan jalur (`StoryHolding`) | Radio |
| Bergerak | Locomotion: Walk, Jog, dan Run dicampur sesuai kecepatan |
| Berhenti setelah berlari | Run to stop, lalu Idle |
| Diam | Idle |

Ukuran karakter diatur konstanta `MiningWorkerAvatar.Scale`, saat ini 1,3 (sekitar 2,4 m). Nilai 2 dicoba dan terlalu besar karena menutupi jalur di kamera Cerita.

EDITOR PREVIEW menampilkan karakter yang sama dalam pose Idle. Instance-nya bertanda `DontSave`, jadi scene tidak berubah dan menu Rebuild Editor Preview tetap bekerja seperti sebelumnya.

## Mengubah atau menambah

- Setelah mengganti FBX, tekstur, atau klip: jalankan **SafeMining > Worker > Build materials, animator and prefab**.
- Klip baru: taruh di `Animations/Mixamo/` dengan nama `Worker_<Nama>.fbx`, format FBX for Unity, Without Skin, 30 fps, In Place untuk gerak berjalan.
- Model dirakit dengan skrip Blender tanpa jendela (Blender 5.2). Skrip dan berkas `.blend` tidak masuk repo; minta ke RockHead07 bila perlu merakit ulang.

## Pengujian dan batasan

Diuji di Play mode Unity 6000.3.23f1, mode Cerita dengan edge lokal: Avatar Humanoid valid dengan 52 tulang terpetakan otomatis, animasi Radio saat briefing dan pemeriksaan jalur, Walk saat bergerak, tanpa error di console. Di editor, warna cyan polos sesaat pada karakter adalah placeholder kompilasi shader Unity, bukan kesalahan material.

Belum diuji: mode FPP (seharusnya tersembunyi mengikuti simulasi), resolusi 4:3 dengan karakter baru, dan build player.
