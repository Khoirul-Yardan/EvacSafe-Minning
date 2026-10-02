# Riset pipeline karakter pekerja: Blender 5.2 ke Unity 6 Humanoid

30 September 2026. Riset berbasis sumber primer untuk karakter pekerja baru (Sketchfab "Man Miner Rig .FBX", CC BY) yang diproses headless di Blender 5.2.2 LTS lalu diimpor ke Unity 6000.3.23f1 (URP) sebagai Humanoid dengan animasi Mixamo. Setiap klaim diberi nomor sumber `[n]` yang merujuk ke bagian **Sumber** di akhir. Klaim yang tidak bisa dipastikan dari sumber primer ditandai **belum terverifikasi**.

Selain membaca sumber, dijalankan eksperimen headless di Blender 5.2.2 LTS (hash `d13f752e3b9c`) pada rig sintetis yang meniru kondisi karakter: empty induk skala 0,01 dan rotasi X 90°, armature dalam satuan cm, mesh ter-skin dengan custom normals, kacamata dan empty socket yang di-parent ke bone `head`. Hasil eksperimen ditandai **[uji lokal]**. Skrip uji hanya ada di scratchpad sesi, tidak masuk repo.

## Ringkasan

- **Penyebab mesh rusak sudah terbukti.** `transform_apply` pada armature hanya mengubah rest bone, tidak mengubah pose, kurva animasi, atau constraint [8][5][7]. Nilai `location` pose yang tersimpan dalam satuan lama (cm) tidak ikut diskalakan, jadi setelah apply skala 0,01 pose yang sama bergeser 4,95 m pada rig uji **[uji lokal]**. Rencana "clear animation_data dan reset pose dulu" memang benar dan menghasilkan selisih 0,0 m pada bentuk rest **[uji lokal]**.
- **Ada dua jebakan lain yang belum ada di rencana.** (1) `transform_apply` hanya menerapkan transform *lokal* objek [5], jadi skala 0,01 milik empty induk tidak hilang kalau armature tidak di-unparent dulu (`parent_clear` tipe `CLEAR_KEEP_TRANSFORM`). (2) Objek yang di-parent ke bone (kacamata, headlamp, socket) melompat sekitar 69 m saat armature di-apply **[uji lokal]**. Solusinya: lepas dulu, apply rig, lalu pasang lagi ke bone. Paling aman, buat attachment *setelah* rig bersih.
- **Opsi exporter FBX untuk Unity sudah dipastikan dari source add-on 5.15.0** [1][2]: `apply_scale_options='FBX_SCALE_ALL'` (menulis `UnitScaleFactor=100`, objek tanpa skala), `axis_forward='-Z'`, `axis_up='Y'`, `add_leaf_bones=False` (default operator justru `True`), `bake_anim=False` (default `True` dan mengekspor semua action yang cocok, termasuk sisa action Sketchfab), `mesh_smooth_type='OFF'` (hanya normal, custom normals ditulis per corner), `use_mesh_modifiers=False` bila ada shape key.
- **Objek yang di-parent ke bone keluar di FBX sebagai anak node bone** (`SmartGlasses -> head`, `HeadlampSocket -> head`) [3] **[uji lokal]**, jadi di Unity semestinya menjadi child transform `head` dan ikut animasi Humanoid. Bagian Unity-nya **belum terverifikasi** di editor proyek ini.
- **Perubahan Blender 5.x yang relevan:** Collada `.dae` dihapus total [10] (operator `wm.collada_export` tidak ada lagi **[uji lokal]**). Importer FBX default sekarang C++ (`bpy.ops.wm.fbx_import`), sedangkan add-on Python menjadi "Legacy" [10][1]. API action lama dihapus [11]. `Material.use_nodes` deprecated sampai 6.0 **[uji lokal]**. Auto Smooth sudah hilang sejak 4.1 [12][13], nama socket Principled berubah sejak 4.0 [14], dan `blend_method` diganti `surface_render_method` sejak 4.2 [15].
- **Di Unity, URP hanya memetakan sebagian material FBX.** `FBXMaterialDescriptionPreprocessor` memetakan Base Map, Normal Map, Emission, dan Smoothness (hasilnya sama dengan 1 dikurangi roughness Blender), tetapi tidak memetakan metallic atau roughness texture [18][2]. Tekstur itu harus dipasang manual. glTFast tidak dibutuhkan untuk FBX; paket itu masuk hanya sebagai dependensi Coplay [19][20].
- **Tooling:** headless `blender -b --python` tetap jalur utama karena deterministik dan bisa ditinjau. Ada Blender MCP resmi dari Blender Lab (v1.0.3, butuh Blender 5.1+) [23][24] dan versi pihak ketiga ahujasid [22]. Keduanya menjalankan Python dari LLM tanpa pengaman bawaan. context7 sudah mengindeks bpy (`/websites/blender_api_current`, isinya era 5.x) dan Unity 6000.0, tetapi juga mewarisi teks manual yang usang (misalnya "scale default 10").
- **Lisensi:** FAQ Adobe mengizinkan karakter dan animasi Mixamo dipakai royalty free, termasuk untuk video game [25]. Model Sketchfab CC BY wajib diberi atribusi dan keterangan perubahan [27]. Opsi unduhan Mixamo ("FBX for Unity", "Without Skin", "In Place", fps) tidak punya halaman bantuan resmi yang bisa ditemukan, jadi **belum terverifikasi**.

## 1. Exporter FBX Blender 5.2 (io_scene_fbx 5.15.0): opsi untuk Unity

Versi add-on yang terpasang: `"version": (5, 15, 0)`, `"blender": (5, 0, 0)` [1, baris 5-18]. Operator ekspor tetap `bpy.ops.export_scene.fbx` (Python). Tidak ada `wm.fbx_export` versi C++ **[uji lokal]**.

| Opsi | Default operator | Yang dilakukan kode | Rekomendasi Unity |
|---|---|---|---|
| `apply_unit_scale` | `True` [1, 351-358] | `unit_scale = units_blender_to_fbx_factor(scene)`, yaitu 100 × `scale_length` untuk sistem Metric [2, 3542][3, 216-217] | `True` |
| `apply_scale_options` | item pertama, `FBX_SCALE_NONE` [1, 359-373] | `NONE`: skala unit (100) dan skala custom dimasukkan ke transform objek, `UnitScaleFactor=1` (cm). `UNITS`: skala unit masuk ke `UnitScaleFactor`. `CUSTOM`: kebalikannya. `ALL`: keduanya masuk ke `UnitScaleFactor` [2, 3541-3552]. Nilai ditulis ke GlobalSettings `UnitScaleFactor` [2, 3312-3322] | `FBX_SCALE_ALL`. Uji: `UnitScaleFactor=100.0` dan node Armature/Body tanpa `Lcl Scaling`. Dengan `NONE`, node root mendapat `Lcl Scaling` 100 **[uji lokal]** |
| `global_scale` | `1.0` [1, 344-350] | Dikalikan ke matrix global atau ke `UnitScaleFactor`, tergantung opsi di atas [2, 3543-3552] | `1.0` |
| `axis_forward` / `axis_up` | `-Z` / `Y` (dekorator `orientation_helper`) [1, 311] | `axis_conversion(...)` menjadi `global_matrix` bila `use_space_transform=True` [1, 609-619]. Ditulis sebagai `UpAxis`/`FrontAxis` [2, 3302-3318] | `-Z`, `Y`. Uji: `UpAxis=1`, `FrontAxis=2` **[uji lokal]**. Preset internal `defaults_unity3d()` memakai nilai yang sama [2, 3649-3687], tetapi fungsi itu tidak dipanggil di mana pun (dead code, hanya menunjukkan niat maintainer) |
| `bake_space_transform` | `False` | Deskripsinya sendiri: "experimental option, use at own risk, known to be broken with armatures/animations" [1, 381-387]. Kode hanya berlaku untuk mesh-like dan Empty [3, 1748-1752] | `False`. Akibatnya node root Armature dan Body di FBX membawa `Lcl Rotation` −90° X **[uji lokal]** |
| `add_leaf_bones` | **`True`** (ada komentar `# False for commit!`) [1, 467-472] | Menambah node `<bone>_end` di setiap ujung rantai [2, 2221-2250, 2894-2895] | `False` (harus diisi eksplisit) |
| `primary_bone_axis` / `secondary_bone_axis` | `Y` / `X` [1, 473-494] | Untuk `(Y, X)` tidak ada koreksi (`bone_correction_matrix = None`). Nilai lain memutar orientasi node bone [2, 3563-3573][3, 1780-1787] | Biarkan `Y`/`X`. Komentar preset Unity: "Doesn't really matter for Unity" [2, 3681] |
| `use_armature_deform_only` | `False` [1, 495-499] | Hanya menulis bone dengan `use_deform`, ditambah parent non-deform yang punya anak deform [2, 2168-2182] | Opsional. Importer FBX legacy tidak pernah mengubah `use_deform` (tidak ada referensi di `import_fbx.py`) dan default bone baru adalah `True` **[uji lokal]**. Jadi opsi ini baru berpengaruh setelah bone IK atau virtual ditandai non-deform secara manual. Uji: `ik_foot_root` yang ditandai non-deform ikut hilang dari FBX **[uji lokal]** |
| `armature_nodetype` | `NULL` [1, 500-510] | Node armature ditulis sebagai FBX Null | `NULL` |
| `bake_anim` | **`True`** [1, 511-515] | Membuat AnimStack. `bake_anim_use_all_actions=True` mengekspor *setiap* action yang F-curve-nya cocok dengan objek [1, 528-534][2, 2522-2535], jadi action sisa impor Sketchfab ikut terekspor meskipun `animation_data` sudah di-clear | `False` untuk FBX karakter (animasi dari Mixamo) |
| `mesh_smooth_type` | `OFF` ("Normals Only") [1, 414-424] | Normal selalu ditulis (`write_normals = True`) [2, 865]. Smoothing flag hanya ditulis untuk `FACE`/`EDGE`/`SMOOTH_GROUP` [2, 1053]. Domain `CORNER`/`FACE` ditulis dari `me.corner_normals` sebagai `ByPolygonVertex` (termasuk custom normals) [2, 1185-1202]. Ada workaround khusus Unity untuk normal yang terdeduplikasi dan shape key [2, 1215-1222, 811-817] | `OFF` + Unity `Normals: Import`. Deskripsi opsinya menyarankan "prefer 'Normals Only' option if your target importer understands custom normals" [1, 421-422] |
| `use_mesh_modifiers` | `True` [1, 403-408] | "Apply modifiers ... (except Armature ones) - WARNING: prevents exporting shape keys" [1, 405-406] | `False` bila mesh punya shape key atau tidak ada modifier lain yang perlu diterapkan |
| `path_mode` / `embed_textures` | `AUTO` / `False` [1, 554-559] | `embed_textures` dimatikan paksa kalau `path_mode != 'COPY'` [2, 3559-3561]. Item `path_mode` **[uji lokal]**: `AUTO, ABSOLUTE, RELATIVE, MATCH, STRIP, COPY` | `COPY` + `embed_textures=True` (FBX mandiri), atau salin tekstur terpisah ke `Assets/` |
| `object_types` | semua [1, 389-401] | | `{'ARMATURE','MESH','EMPTY'}` (Empty dibutuhkan untuk `HeadlampSocket`) |

Catatan penting dari kode:

- **Transform node bone diambil dari pose saat ini, bukan rest.** `get_matrix_local` untuk tag `'BO'` bertanda `# 'BO', current pose` dan memakai `pose.bones[...].matrix` [3, 1696-1704]. Model node ditulis dari `fbx_object_tx` tanpa `rest=True` [2, 1992]. Hanya BindPose yang memakai `rest=True` [2, 756]. Jadi pose harus di-reset sebelum ekspor, kalau tidak "default pose" di Unity ikut berubah.
- **Mesh yang di-skin tidak menjadi anak armature di FBX.** Mesh itu ditulis dalam ruang global dan terhubung ke root scene [2, 2213-2216][3, 1770-1772]. Uji: `Body -> 0 (root)`, `pelvis -> Armature` **[uji lokal]**.
- **Objek dengan `parent_type == 'BONE'` diparent ke node bone.** Transform lokalnya dikoreksi dari ruang ujung (tail) bone ke ruang pangkal (head) bone [3, 1671-1686, 1791-1795]. Uji: `SmartGlasses -> head`, `HeadlampSocket -> head` **[uji lokal]**.
- **Manual FBX usang untuk exporter.** `fbx_legacy.rst` menulis "Scale ... 10 is the default" dan "Vertex shape keys ... this exporter does not write them yet" [9b]. Source menunjukkan default `1.0` [1, 349] dan shape key memang diekspor (`data_deformers_shape`) [2, 811-824]. Untuk exporter, pegang source.

## 2. Menghapus empty root 0,01 dengan aman

### Apa yang sebenarnya dilakukan `transform_apply`

- **Manual:** "Applying transforms to armatures is supported, but it does **not** affect pose locations, animation curves, or constraints. It is recommended to apply transforms before rigging and animation." Data yang dipakai bersama (multi-user) harus dijadikan Single User dulu [8].
- **Parameter operator di 5.2:** `location, rotation, scale, properties, corrective_flip_normals, isolate_users` [8b] **[uji lokal]**.
- **Source (`object_transform.cc`, branch `blender-v5.2-release`)** [5]:
  - Matriks yang di-apply dibangun dari `ob->loc`, `ob->rot`, dan `ob->scale` milik objek sendiri (`BKE_object_to_mat3`, `BKE_object_scale_to_mat3`). Artinya hanya transform **lokal**. Skala milik induk tidak ikut.
  - Mesh diproses dengan `bke::mesh_transform(*mesh, mat, true)`, yang juga mentransformasi shape key dan atribut custom normal (`transform_custom_normal_attribute`) [6]. Jadi custom normals dan shape key aman. Uji: `has_custom_normals=True` dan arah normal tetap **[uji lokal]**.
  - Armature diproses dengan `BKE_armature_transform(arm, mat, do_props)`. Fungsi ini hanya mengubah head, tail, dan roll bone rest (`armature_transform_recurse`) dan tidak menyentuh pose channel [7].
  - Setelah objek induk di-apply, anak-anaknya dikompensasi oleh `ignore_parent_tx` supaya tetap di tempat [5].
  - Urutan pemrosesan: induk dulu (`sorted_selected_editable_objects`) [5].
  - Pesan error multi-user: `Cannot apply to a multi user ... aborting`. Bisa diatasi dengan `isolate_users=True` [5].
  - Untuk Empty tidak ada data yang ditransformasi, jadi meng-apply `location` pada Empty mengembalikan posisinya ke origin. Uji: `HeadlampSocket` pindah ke (0,0,0) **[uji lokal]**.

### Hasil eksperimen [uji lokal]

Kondisi awal: root empty (skala 0,01, rotasi X 90°) → armature satuan cm → mesh ter-skin + kacamata dan socket di bone `head`. Pose uji: `pelvis.location = (0,0,5)` dan `spine_01` diputar 20°.

| Metode | Hasil |
|---|---|
| A. Apply armature+mesh tanpa unparent | Skala armature sudah 1 (tidak ada yang berubah), root tetap 0,01. Tidak menyelesaikan apa pun |
| B. `parent_clear(CLEAR_KEEP_TRANSFORM)`, lalu apply selagi pose masih terisi | Pose yang sama bergeser **4,95 m** (inilah "mesh rusak"). Setelah pose di-reset, bentuk rest kembali cocok (selisih 0,0) |
| C. Clear animasi, reset pose, unparent, lalu apply armature+mesh+attachment sekaligus | Mesh cocok (0,0), tetapi **kacamata dan socket melompat sekitar 69-70 m** |
| D. (dipakai) Reset pose, lepas attachment (keep transform), unparent armature, apply hanya armature+mesh, attachment mesh apply rotasi+skala saja, Empty cukup diset skala 1, lalu pasang lagi ke bone `head` | Mesh 0,0, kacamata 0,0, socket 0,0. Semua skala 1. Kacamata ikut saat bone `head` diputar |

Kesimpulan: rencana "clear animation_data + reset pose" diperlukan tetapi belum cukup. Tambahkan unparent keep transform dan penanganan attachment seperti metode D. Cara paling sederhana adalah membuat attachment (kacamata, headlamp, socket) **setelah** rig bersih, langsung dalam satuan meter.

Pengecualian: kalau pose sisa impor bukan pose kosong (misalnya file memang menyimpan pose berbeda dari bind pose), reset pose mengembalikan mesh ke bind pose. Ini memang yang dibutuhkan untuk avatar, tetapi bentuknya mungkin A-pose. Kondisi file asli **belum terverifikasi** karena file sumber tidak ada di repo.

### Asal empty 0,01

Importer FBX legacy mengalikan `global_scale` dengan `UnitScaleFactor / units_blender_to_fbx_factor(scene)` lalu mengonversi sumbu file ke Z-up [4, 3146-3161]. File dalam cm (`UnitScaleFactor=1`) menghasilkan skala 0,01 dan rotasi 90° pada node root. Pada rig uji, ekspor kembali ke Y-up membuat rotasi root menjadi 0 dan menyisakan skala 0,01 **[uji lokal]**. Apakah file Sketchfab ini memakai jalur itu (atau punya node `Sketchfab_model` sendiri) **belum terverifikasi**. Importer C++ `wm.fbx_import` (default menu di 5.x) punya opsi yang berbeda, misalnya tanpa `automatic_bone_orientation` **[uji lokal]**.

### Biarkan root saja?

Bisa, tetapi kurang disarankan. Dengan root dibiarkan dan `FBX_SCALE_ALL`, FBX berisi node `Sketchfab_model` dengan `Lcl Scaling` 0,01, mesh Body di root juga dengan skala 0,01, dan bone dalam cm **[uji lokal]**. Unity akan membawa skala 0,01 itu ke hierarki. Apakah avatar Humanoid dan retarget Mixamo berjalan benar dengan skala non-1 di hierarki **belum terverifikasi**. Opsi ini hanya jadi fallback kalau waktu habis.

## 3. Perubahan Blender 5.x yang memengaruhi pipeline

| Topik | Fakta | Sumber |
|---|---|---|
| Collada | "Collada (`.dae`) import & export has been removed." `wm.collada_export` tidak terdaftar di 5.2.2 | [10] **[uji lokal]** |
| Importer FBX | Importer C++ baru jadi default, versi Python ditandai legacy. Menu legacy berlabel "FBX (.fbx) (Legacy)". Operator C++: `bpy.ops.wm.fbx_import` (opsi: `global_scale`, `use_custom_normals`, `ignore_leaf_bones`, `use_anim`, `mtl_name_collision_mode`, dan lain-lain) | [10][1, 716][9] **[uji lokal]** |
| Exporter FBX | Tetap add-on Python `export_scene.fbx` | [9][1] |
| Action | API action legacy dihapus. Akses F-curve lewat channelbag (`bpy_extras.anim_utils.action_ensure_channelbag_for_slot`). Untuk pipeline ini cukup `obj.animation_data_clear()` | [11] |
| Material | `Material.use_nodes` memunculkan `DeprecationWarning: ... expected to be removed in Blender 6.0` | **[uji lokal]**, [11] |
| Principled BSDF | Socket yang diganti nama sejak 4.0: `Subsurface Weight`, `Specular IOR Level`, `Transmission Weight`, `Coat Weight`, `Sheen Weight`, `Emission Color`. Daftar input 5.2.2: `Base Color, Metallic, Roughness, IOR, Alpha, Thin Wall, Normal, Weight, Diffuse Roughness, Subsurface Weight, ..., Emission Color, Emission Strength, Thin Film Thickness, Thin Film IOR` | [14] **[uji lokal]** |
| Normal | Auto Smooth dihapus di 4.1 (`use_auto_smooth`, `calc_normals_split` hilang). Tersedia `normals_domain` dan `corner_normals`. Custom normals dibuat lewat `normals_split_custom_set(_from_vertices)`. Di 5.2: `hasattr(mesh,'use_auto_smooth') == False`, `has_custom_normals` ada | [12][13] **[uji lokal]** |
| Render method | 4.2: "Blend Mode was replaced by Render Method". `Blended` setara Alpha Blend lama, `Dithered` setara Alpha Hashed. Di 5.2 default `surface_render_method='DITHERED'`. Tidak memengaruhi FBX (exporter menulis `Opacity`/`TransparencyFactor` dari alpha Principled) | [15] **[uji lokal]**, [2, 1618-1631] |
| Importer glTF | Add-on `io_scene_gltf2` 5.2.40. Default `bone_heuristic='BLENDER'`, `guess_original_bind_pose=True`, `merge_vertices=False`. glTF memakai satuan meter dan +Y up, jadi alternatif unduhan glTF dari Sketchfab tidak akan menghasilkan empty 0,01. Namun bone hasil heuristik bisa punya roll dan orientasi berbeda | **[uji lokal]**, [28] |

## 4. Sisi Unity (6000.3)

**Tab Model** [17a]:

- **Scale Factor:** "Unity's physics system expects 1 m in the game world to be 1 unit in the imported file."
- **Convert Units:** "Converts the model scaling defined in the model file to Unity's scale." Dengan `UnitScaleFactor=100` (meter) dari `FBX_SCALE_ALL`, hasil yang diharapkan skala 1:1. Angka pastinya di Unity **belum terverifikasi**.
- **Bake Axis Conversion:** "Bakes the results of axis conversion directly into your application's asset data ... when you import a model that uses a different axis system than Unity. Disable this property to compensate the Transform component of the root GameObject at runtime." File dari exporter sudah Y-up, jadi opsi ini diperkirakan tidak berefek. Rotasi −90° X pada node `Armature`/`Body` tetap ada karena berasal dari transform node, bukan dari sistem sumbu **[uji lokal]**. Perilakunya di Unity **belum terverifikasi**.
- **Normals Import:** "Import normals from the file. This is the default option."

**Tab Rig** [17b]:

- **Animation Type:** Humanoid. **Avatar Definition:** "Create From This Model" untuk karakter.
- **Copy From Other Avatar:** "Point to an Avatar set up on another model."
- **Optimize Game Object:** "Remove and store the GameObject Transform hierarchy ..." Opsi ini menyembunyikan transform `head` beserta kacamata dan socket, kecuali dicantumkan di **Extra Transforms to Expose**. Rekomendasi: matikan.
- **Strip Bones:** "only add bones to Skinned Mesh Renderers that have skin weights assigned to them".

**Avatar** [17c][17d][17h][17i]:

- Minimal 15 bone.
- "name your bones in a way that reflects the body parts they represent".
- Wajib T-pose: "The required pose for the character to be in, in order to make an Avatar". Gunakan Pose → Sample Bind-pose atau Enforce T-Pose, lalu Mapping → Automap.
- Bone wajib digambar lingkaran solid, bone opsional lingkaran putus-putus.
- Daftar bone wajib bisa dicetak lewat `HumanTrait.RequiredBone(i)`.
- Seberapa baik Automap membaca nama gaya UE5 (`pelvis`, `thigh_l`, `calf_l`, `upperarm_l`, `lowerarm_l`) **belum terverifikasi**. Periksa di Configure. Buang dulu sufiks numerik Sketchfab (lihat pipeline).

**Retarget Mixamo** [17e][17f]:

- "Reuse humanoid clips on different models after you configure each Avatar". Retarget terjadi karena kedua model punya Avatar Humanoid yang valid.
- Untuk file klip Mixamo: Rig = Humanoid. Satu klip memakai "Create From This Model", klip lain boleh "Copy From Other Avatar" ke avatar klip pertama (semua kerangka Mixamo sama, **belum terverifikasi**).
- Klip "In Place": atur Root Transform Rotation/Position (Y)/(XZ) → **Bake Into Pose** sesuai kebutuhan, dan **Loop Time** untuk jalan atau lari [17g].
- Bone tambahan yang tidak dipetakan (twist, corrective jari) tidak digerakkan oleh klip Humanoid. Kemungkinan muncul artefak di pergelangan atau lengan bawah, **belum terverifikasi**.

**Material di URP** [17j][18]:

- Material Creation Mode `Import via MaterialDescription` membaca deskripsi material dari FBX. Location `Use Embedded Materials`, lalu tombol **Extract Materials** / **Extract Textures** bila material perlu diedit.
- Preprocessor URP 17.3.0 hanya aktif bila pipeline aktif adalah `UniversalRenderPipelineAsset`. Preprocessor mengganti shader ke URP Lit dan memetakan:
  - `DiffuseColor` → `_BaseMap`/`_BaseColor`
  - `NormalMap`/`Bump` → `_BumpMap`
  - `EmissiveColor` → `_EmissionColor`
  - `Shininess` → `_Smoothness = sqrt(shininess*0.01)`
- Tidak ada pemetaan metallic [18].
- Exporter Blender menulis `Shininess = ((1-roughness)*10)^2` [2, 1646-1649], sehingga smoothness URP = 1 − roughness untuk nilai konstan. Roughness texture dan metallic texture (dipetakan exporter ke `Shininess`/`ReflectionFactor` [2, 2130-2152]) tidak terpakai oleh URP. Pasang manual (URP Lit memakai metallic di R dan smoothness di A).

**Anak bone di Unity:** FBX menaruh kacamata dan socket sebagai anak node `head` **[uji lokal]**. Unity membuat GameObject per node, jadi keduanya akan menjadi child `head` dan ikut animasi. Ini konsekuensi logis dari struktur FBX, tetapi **belum terverifikasi** di editor proyek ini. Spot light Unity dipasang ke `HeadlampSocket` setelah impor. Lampu tidak diekspor dari Blender (`object_types` tanpa `LIGHT`).

**glTFast:** paket itu untuk "import and export glTF™ 3D files" [20]. Impor FBX memakai ModelImporter bawaan [17a]. `com.unity.cloud.gltfast` 6.14.1 ada di proyek hanya sebagai dependensi `com.coplaydev.coplay` (depth 1) [19]. Tidak perlu apa-apa untuk FBX.

## 5. Tooling

### (a) Blender MCP

| | Blender Lab (resmi) | ahujasid/blender-mcp (pihak ketiga) |
|---|---|---|
| Status | Proyek Blender Lab, repo `lab/blender_mcp`, rilis terbaru v1.0.3 (2026-09-11), butuh Blender 5.1+ [23][24] | "This is a third-party integration and not made by Blender" [22] |
| Koneksi | `MCP Client ⇐ stdio ⇒ blender-mcp ⇐ TCP socket ⇒ Blender Add-on`. Host dan port diatur di preferensi add-on [24] | Add-on `addon.py` membuka socket server, default `localhost:9876`. MCP server via `uvx` [22] |
| Tool | `execute_blender_code`, `execute_blender_code_for_cli` (proses Blender background), ringkasan blendfile dan objek, `get_python_api_docs`, `search_api_docs`/`search_manual_docs` (dokumen API dan manual dibundel), screenshot, render viewport [24] | `execute_blender_code`, `get_scene_info`, objek dan material, integrasi Poly Haven, Sketchfab, Hyper3D, dan lain-lain [22] |
| Keamanan | "The MCP server will execute LLM generated code in Blender without any guards in place to protect your data from removal or being sent to a remote location". Disarankan memakai VM [23] | Socket "has no authentication or encryption, so anyone who can reach that port can run Python inside Blender". Ada `BLENDER_MCP_SAFE_MODE=1`. Data pemakaian anonim minimal dikirim secara default, konten hanya kalau opt-in (`DISABLE_TELEMETRY=true` untuk mematikan) [22] |

Nilai tambah untuk tugas hari ini: kecil. Pipeline ini batch dan deterministik. Skrip `blender -b --factory-startup --python` bisa ditinjau, diulang, dan dijalankan tanpa GUI. MCP berguna untuk inspeksi interaktif (screenshot viewport untuk mengecek kacamata duduk di wajah) dan pencarian dokumen API yang dibundel. Kalau dipakai, pilih yang resmi, tetap di localhost, dan simpan file sebelum eksekusi. Menginstalnya tidak dilakukan dalam riset ini.

### (b) context7

Dijalankan langsung:

| Library ID | Isi | Kecocokan dengan 5.x |
|---|---|---|
| `/websites/blender_api_current` (juga ada `_4_5`, `_4_2`) | `transform_apply` lengkap dengan `corrective_flip_normals` dan `isolate_users`; `import_scene.fbx` dengan `mtl_name_collision_mode` (opsi 5.0+) | Era 5.x. Namun `wm.fbx_import` dan `surface_render_method` tidak ditemukan saat di-query |
| `/websites/blender_manual_en` | Mengutip peringatan armature di halaman Apply dengan benar | Juga mengulang teks usang "10 being the default" untuk skala FBX, yang dibantah source [1, 349] |
| `/websites/unity3d_6000_0_manual` (juga `_6000_1_manual_index`) | Deskripsi Bake Axis Conversion sama dengan halaman 6000.3 | Versi 6000.0, bukan 6000.3 |

Kesimpulan: context7 berguna sebagai pencarian cepat. Untuk Blender, sumber terkuat tetap source add-on lokal ditambah probing `blender -b`, karena context7 mewarisi kesalahan manual.

### (c) Mixamo

- FAQ Adobe (terakhir diperbarui 14 Sep 2021) [25]:
  - "You can use both characters and animations royalty free for personal, commercial, and non-profit projects including: ... Create video games."
  - Gratis dengan Adobe ID, tidak tersedia untuk Enterprise/Federated ID.
  - Auto-rigger hanya untuk humanoid bipedal.
- Halaman rigging [26]: upload menerima FBX, OBJ, ZIP, dengan "embed media" untuk tekstur.
- **Belum terverifikasi dari sumber resmi:**
  - Arti opsi unduhan "FBX for Unity", "Without Skin", "In Place", dan "Frames per Second".
  - Klaim larangan redistribusi file mentah. Klaim itu hanya muncul di forum komunitas, bukan di halaman FAQ yang terbuka.

## Rekomendasi pipeline

Urutan dengan risiko minimal untuk hari ini:

1. **Impor** dengan importer legacy (`bpy.ops.import_scene.fbx`), karena perilakunya bisa dibaca di source [4]. Opsi: `use_custom_normals=True`, `ignore_leaf_bones=True`, `use_anim=False` (tidak perlu animasi Sketchfab).
2. **Netralkan animasi dan pose:** `arm.animation_data_clear()`, reset semua pose bone (location 0, rotation identitas, scale 1), lalu hapus action yatim di `bpy.data.actions` supaya tidak terbawa `bake_anim_use_all_actions`.
3. **Bersihkan nama bone:** buang sufiks `_\d+$` lewat `bone.name = ...` (RNA). Rename ini ikut memperbarui vertex group dan `parent_bone` **[uji lokal]**. Cek dulu tidak ada tabrakan nama.
4. **Lepas attachment** yang sudah terpasang ke bone (`parent_clear(type='CLEAR_KEEP_TRANSFORM')`). Lebih baik lagi: jangan buat attachment sebelum langkah 6.
5. **Unparent armature dari root** (`CLEAR_KEEP_TRANSFORM`). Pilih *hanya* armature + mesh ter-skin, lalu `transform_apply(location=True, rotation=True, scale=True, isolate_users=True)`. Hapus empty root.
6. **Buat atau pasang ulang attachment** dalam meter: mesh kacamata dan headlamp apply `rotation+scale` saja; `HeadlampSocket` cukup `matrix_world = LocRotScale(loc, rot, (1,1,1))`. Parent ke armature dengan `parent_type='BONE'`, `parent_bone='head'`, lalu kembalikan `matrix_world` yang disimpan.
7. **Verifikasi di skrip:** skala semua objek 1; posisi verteks rest sama dengan sebelum langkah 5 (toleransi 1e-4); tinggi karakter wajar (sekitar 1,7-1,8 m); kacamata ikut saat `head` diputar.
8. **Ekspor:**

```python
bpy.ops.export_scene.fbx(
    filepath=out, object_types={'ARMATURE', 'MESH', 'EMPTY'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', use_space_transform=True, bake_space_transform=False,
    mesh_smooth_type='OFF', use_mesh_modifiers=False, use_tspace=False,
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    use_armature_deform_only=False, armature_nodetype='NULL',
    bake_anim=False, path_mode='COPY', embed_textures=True)
```

9. **Periksa FBX** dengan `fbx2json.py` bawaan add-on (`5.2/python/bin/python.exe .../io_scene_fbx/fbx2json.py file.fbx`). Pastikan `UnitScaleFactor=100`, tidak ada node `_end`, tidak ada Take, dan `SmartGlasses`/`HeadlampSocket` menjadi anak `head`.
10. **Unity, karakter:**
    - Model: Convert Units on, Normals Import.
    - Rig: Humanoid, Create From This Model, Optimize Game Objects off.
    - Configure: Enforce T-Pose, cek mapping.
    - Materials: Import via MaterialDescription, Extract Materials, pasang metallic/smoothness manual.
11. **Unity, Mixamo:** Rig Humanoid (satu klip Create From This Model, sisanya Copy From Other Avatar), Loop Time untuk siklus, Root Transform Bake Into Pose untuk klip In Place.
12. **Atribusi:** cantumkan kredit model Sketchfab (judul, pembuat, tautan, CC BY 4.0, keterangan "dimodifikasi") di kredit game dan dokumen lomba [27].

### Jebakan dan perbaikannya

| # | Jebakan | Perbaikan |
|---|---|---|
| 1 | Apply transform saat pose atau animasi masih ada membuat mesh rusak (pose `location` tidak diskalakan) [8][7] **[uji lokal: 4,95 m]** | Clear animasi dan reset pose sebelum apply |
| 2 | Apply pada armature tidak menghapus skala empty induk (hanya transform lokal) [5] **[uji lokal]** | `parent_clear(CLEAR_KEEP_TRANSFORM)` dulu |
| 3 | Attachment ber-parent bone melompat saat armature di-apply **[uji lokal: ~69 m]** | Lepas dulu dan pasang lagi, atau buat attachment setelah rig bersih |
| 4 | Apply `location` pada Empty membuang posisinya **[uji lokal]** | Untuk Empty cukup reset skala lewat `matrix_world` |
| 5 | Data multi-user membuat apply batal [5][8] | `isolate_users=True` |
| 6 | `add_leaf_bones` default `True` [1, 471] | Isi `False` |
| 7 | `bake_anim` default `True` + all actions mengekspor action sisa [1, 511-534][2, 2522] | `bake_anim=False` dan hapus action yatim |
| 8 | `use_mesh_modifiers=True` mencegah ekspor shape key [1, 405-406] | `False` |
| 9 | Node bone ditulis dari pose saat ini [3, 1701] | Reset pose sebelum ekspor |
| 10 | `bake_space_transform` rusak untuk armature [1, 385] | Biarkan `False`, terima rotasi −90° X di node root |
| 11 | URP tidak memetakan metallic atau roughness texture [18] | Extract Materials, pasang manual |
| 12 | Optimize Game Objects menyembunyikan `head` beserta attachment [17b] | Matikan, atau Extra Transforms to Expose |
| 13 | Avatar tidak dalam T-pose [17c] | Enforce T-Pose di Configure |
| 14 | Sufiks numerik Sketchfab mengganggu Automap (**belum terverifikasi**) | Rename via RNA (vertex group ikut) **[uji lokal]** |
| 15 | Mengandalkan `.dae` atau Collada [10] | Pakai FBX |
| 16 | Skrip lama memakai `action.fcurves`, `use_auto_smooth`, `blend_method`, nama socket Principled lama [11][13][14][15] | API 5.x: channelbag, `normals_domain`/`corner_normals`, `surface_render_method`, nama socket baru |
| 17 | Mempercayai manual FBX (scale default 10, "shape key tidak diekspor") [9b] | Pegang source add-on |
| 18 | Menganggap `use_armature_deform_only` membuang bone IK hasil impor | Importer tidak menandai non-deform. Tandai manual dulu **[uji lokal]** |

## Sumber

1. Blender 5.2.2 LTS, add-on FBX 5.15.0: `D:\Steam\steamapps\common\Blender\5.2\scripts\addons_core\io_scene_fbx\__init__.py` (baris 5-18 bl_info; 201 `mtl_name_collision_mode`; 311-622 `ExportFBX`; 716 label Legacy).
2. `...\io_scene_fbx\export_fbx_bin.py` (baris 756, 811-824, 865, 1053, 1185-1222, 1600-1651, 1992, 2130-2152, 2168-2182, 2187-2216, 2221-2250, 2522-2535, 2894, 3302-3322, 3494-3645, 3648-3687).
3. `...\io_scene_fbx\fbx_utils.py` (baris 216-217, 1671-1718, 1748-1818).
4. `...\io_scene_fbx\import_fbx.py` (baris 3146-3166).
5. Blender source, `object_transform.cc`: https://projects.blender.org/blender/blender/src/branch/blender-v5.2-release/source/blender/editors/object/object_transform.cc
6. Blender source, `mesh.cc` (`mesh_transform`): https://projects.blender.org/blender/blender/src/branch/blender-v5.2-release/source/blender/blenkernel/intern/mesh.cc
7. Blender source, `armature.cc` (`BKE_armature_transform`): https://projects.blender.org/blender/blender/src/branch/blender-v5.2-release/source/blender/blenkernel/intern/armature.cc
8. Blender Manual, Apply: https://docs.blender.org/manual/en/latest/scene_layout/object/editing/apply.html (source: https://projects.blender.org/blender/blender-manual/src/branch/main/manual/scene_layout/object/editing/apply.rst). 8b. bpy API `transform_apply`: https://docs.blender.org/api/current/bpy.ops.object.html
9. Blender Manual, FBX (importer C++): https://docs.blender.org/manual/en/latest/files/import_export/fbx.html. 9b. FBX Legacy: https://docs.blender.org/manual/en/latest/files/import_export/fbx_legacy.html (source `.rst` di repo blender-manual).
10. Release notes 5.0, Pipeline & I/O: https://developer.blender.org/docs/release_notes/5.0/pipeline_io/
11. Release notes 5.0, Python API: https://developer.blender.org/docs/release_notes/5.0/python_api/
12. Release notes 4.1, Modeling: https://developer.blender.org/docs/release_notes/4.1/modeling/
13. Release notes 4.1, Python API: https://developer.blender.org/docs/release_notes/4.1/python_api/
14. Release notes 4.0, Python API (rename socket Principled): https://developer.blender.org/docs/release_notes/4.0/python_api/
15. Release notes 4.2, EEVEE (Render Method): https://developer.blender.org/docs/release_notes/4.2/eevee/
16. Probing lokal `blender.exe -b --factory-startup --python` di Blender 5.2.2 LTS (hash d13f752e3b9c, build 2026-09-15): parameter operator, daftar input Principled, `wm.fbx_import`, Collada, `use_nodes`, eksperimen apply/ekspor.
17. Unity 6.3 LTS Manual: (a) https://docs.unity3d.com/6000.3/Documentation/Manual/FBXImporter-Model.html (b) https://docs.unity3d.com/6000.3/Documentation/Manual/FBXImporter-Rig.html (c) https://docs.unity3d.com/6000.3/Documentation/Manual/ConfiguringtheAvatar.html (d) https://docs.unity3d.com/6000.3/Documentation/Manual/UsingHumanoidChars.html (e) https://docs.unity3d.com/6000.3/Documentation/Manual/Retargeting.html (f) https://docs.unity3d.com/6000.3/Documentation/Manual/AvatarCreationandSetup.html (g) https://docs.unity3d.com/6000.3/Documentation/Manual/class-AnimationClip.html (h) https://docs.unity3d.com/6000.3/Documentation/Manual/class-Avatar.html (i) https://docs.unity3d.com/6000.3/Documentation/ScriptReference/HumanTrait.RequiredBone.html (j) https://docs.unity3d.com/6000.3/Documentation/Manual/FBXImporter-Materials.html
18. URP 17.3.0, `Library\PackageCache\com.unity.render-pipelines.universal@7865b6b91f8a\Editor\AssetPostProcessors\FBXMaterialDescriptionPreprocessor.cs` (baris 23-164).
19. `Packages\packages-lock.json` proyek (baris 3-11 dan 65-76: glTFast sebagai dependensi Coplay).
20. glTFast 6.14 manual: https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.14/manual/index.html
21. `ProjectSettings\ProjectVersion.txt` (6000.3.23f1).
22. ahujasid/blender-mcp README: https://github.com/ahujasid/blender-mcp (raw: https://raw.githubusercontent.com/ahujasid/blender-mcp/main/README.md)
23. Blender Lab, MCP Server: https://www.blender.org/lab/mcp-server/
24. Blender Lab repo: https://projects.blender.org/lab/blender_mcp (`readme.md`, `readme_tools.rst`, API rilis https://projects.blender.org/api/v1/repos/lab/blender_mcp/releases)
25. Adobe, Mixamo FAQ: https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html
26. Adobe, Upload and rig 3D characters with Mixamo: https://helpx.adobe.com/creative-cloud/help/mixamo-rigging-animation.html
27. Creative Commons BY 4.0: https://creativecommons.org/licenses/by/4.0/
28. Khronos glTF 2.0 Specification ("The units for all linear distances are meters."): https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html
