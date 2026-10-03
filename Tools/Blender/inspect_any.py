import bpy, sys, json, struct, os
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
path, out = args[0], args[1]
if path.lower().endswith(".glb"):
    with open(path, "rb") as f:
        f.read(12); length, _ = struct.unpack("<II", f.read(8)); doc = json.loads(f.read(length))
    print("ASSET", json.dumps(doc.get("asset", {}))[:600])

bpy.ops.wm.read_factory_settings(use_empty=True)
if path.lower().endswith(".fbx"): bpy.ops.import_scene.fbx(filepath=path)
else: bpy.ops.import_scene.gltf(filepath=path)
dg = bpy.context.evaluated_depsgraph_get()
total = 0; lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
for o in bpy.context.scene.objects:
    if o.type == "MESH":
        m = o.evaluated_get(dg).to_mesh(); tris = sum(len(p.vertices) - 2 for p in m.polygons); total += tris
        olo, ohi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
            olo = Vector(map(min, olo, w)); ohi = Vector(map(max, ohi, w))
        print("MESH", o.name, tris, [s.material.name if s.material else None for s in o.material_slots],
              "size", tuple(round(v, 3) for v in ohi - olo), "center", tuple(round(v, 3) for v in (olo + ohi) / 2))
        o.evaluated_get(dg).to_mesh_clear()
    else:
        print(o.type, o.name, "scale", tuple(round(s, 4) for s in o.scale))
print("TOTAL tris", total, "size", tuple(round(v, 3) for v in hi - lo), "center", tuple(round(v, 3) for v in (lo + hi) / 2))
for mat in bpy.data.materials:
    bsdf = mat.node_tree and next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf:
        print("MAT", mat.name, "base", tuple(round(c, 2) for c in bsdf.inputs["Base Color"].default_value[:3]), "alpha", round(bsdf.inputs["Alpha"].default_value, 2),
              "metal", round(bsdf.inputs["Metallic"].default_value, 2), "linked", [i.name for i in bsdf.inputs if i.is_linked])
for img in bpy.data.images:
    print("IMAGE", img.name, tuple(img.size), "packed" if img.packed_file else img.filepath)
    if img.size[0] and out:
        img.filepath_raw = out + "-tex-" + bpy.path.clean_name(img.name) + ".png"; img.file_format = "PNG"; img.save()

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = scene.render.resolution_y = 800
world = bpy.data.worlds.new("w"); scene.world = world; world.color = (.3, .3, .3)
center = (lo + hi) / 2; span = max(hi - lo)
for name, offset in (("front", Vector((0, -1, .15))), ("three", Vector((.8, -.8, .3))), ("back", Vector((0, 1, .15)))):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name)); scene.collection.objects.link(cam)
    cam.location = center + offset.normalized() * span * 2.2
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    light = bpy.data.objects.new(name + "L", bpy.data.lights.new(name + "L", "SUN")); scene.collection.objects.link(light)
    light.rotation_euler = cam.rotation_euler; light.data.energy = 3
    scene.camera = cam; scene.render.filepath = out + "-" + name + ".png"
    bpy.ops.render.render(write_still=True)
