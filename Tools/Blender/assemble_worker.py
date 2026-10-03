"""Assemble the SAFE-MINING worker: miner character + smart glasses + helmet headlamp, bound to the head bone.

Run: blender -b --factory-startup --python assemble_worker.py -- <pitch_degrees> [render_only_tag]
All inputs and outputs live in the session scratchpad; nothing touches the Unity repo.
"""
import bpy, sys, re, os, math, shutil
from mathutils import Vector, Matrix

SP = "C:/Users/UNKNOWN/AppData/Local/Temp/claude/D--Dev-Projects-UnityProjects-EvacSafe-Minning/d9b3bb79-714b-4450-9a98-1f95e6f984bb/scratchpad"
CHARACTER = SP + "/miner_src/man-miner-rigged.fbx"
GLASSES = SP + "/miner_src/futuristic_glasses.fbx"
LAMP = SP + "/miner_src/headlamp.glb"
LAMP_NORMAL_CLEAN = SP + "/lamp-normal-clean.png"
OUT = SP + "/miner_out"
TEX = OUT + "/Textures"

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PITCH = math.radians(float(args[0]) if args else 37.8)   # mount follows the helmet slope
os.makedirs(TEX, exist_ok=True)
scene = bpy.context.scene


def world_bounds(objs):
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi


def import_new(op, path):
    before = set(bpy.data.objects)
    op(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def flatten(objs):
    """Unparent keeping world transform, then bake transforms into mesh data."""
    for o in objs:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    for o in objs:
        if o.type == "MESH":
            o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active; joined.name = joined.data.name = name
    return joined


def parent_to_bone(o, arm, bone):
    mw = o.matrix_world.copy()
    o.parent = arm; o.parent_type = "BONE"; o.parent_bone = bone
    bpy.context.view_layer.update()
    o.matrix_world = mw


# ---------- character ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=CHARACTER)
arm = next(o for o in scene.objects if o.type == "ARMATURE")
root = arm.parent
for o in list(scene.objects):
    if o.type == "MESH" and "hair" in o.name.lower():
        bpy.data.objects.remove(o, do_unlink=True)            # hidden under the helmet

# Drop the bundled clips first (their origin is unclear: Unreal Mannequin naming; animations come from Mixamo)
# and return every bone to rest, otherwise applying the root scale bakes a distorted pose into the meshes.
arm.animation_data_clear()
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for pb in arm.pose.bones: pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
print("RIG mesh parents", sorted({(o.parent.name if o.parent else None) for o in scene.objects if o.type == "MESH"}))

# Bake the 0.01 / 90 degree root empty into the armature and meshes, so Unity sees plain metres.
parts = [o for o in scene.objects if o.type in ("ARMATURE", "MESH")]
for o in parts:
    mw = o.matrix_world.copy(); o.parent = None if o is arm or o.parent is root else o.parent; o.matrix_world = mw
if root: bpy.data.objects.remove(root, do_unlink=True)
bpy.ops.object.select_all(action="DESELECT")
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
arm.name = arm.data.name = "Skeleton"

# Strip Sketchfab's numeric suffixes (pelvis_04 -> pelvis) so Unity Humanoid auto-maps the rig.
wanted = {b.name: re.sub(r"_\d+$", "", b.name) for b in arm.data.bones}
counts = {}
for v in wanted.values(): counts[v] = counts.get(v, 0) + 1
for b in list(arm.data.bones):
    target = wanted[b.name]
    if counts[target] == 1: b.name = target
bone_names = {b.name for b in arm.data.bones}
meshes = [o for o in scene.objects if o.type == "MESH"]
orphans = sorted({g.name for o in meshes for g in o.vertex_groups} - bone_names)
print("RIG bones", len(bone_names), "renamed", sum(1 for k, v in wanted.items() if v in bone_names and k != v), "orphan vertex groups", orphans[:10])
for o in meshes:
    o.name = re.sub(r"_0$", "", o.name)

# ---------- smart glasses ----------
new = import_new(bpy.ops.import_scene.fbx, GLASSES)
glass_meshes = [o for o in new if o.type == "MESH"]
flatten(new)
for o in new:
    if o.type != "MESH": bpy.data.objects.remove(o, do_unlink=True)
glasses = join(glass_meshes, "Smart glasses")
lo, hi = world_bounds([glasses])
s = .19 / (hi.x - lo.x)                                    # 19 cm across, just wider than the face
glasses.data.transform(Matrix.Scale(s, 4))
lo, hi = world_bounds([glasses])
glasses.data.transform(Matrix.Translation(Vector((-(lo.x + hi.x) / 2, -.128 - lo.y, 1.625 - (lo.z + hi.z) / 2))))
for slot in glasses.material_slots:
    m = slot.material; bsdf = m.node_tree.nodes.get("Principled BSDF")
    lens = len([p for p in glasses.data.polygons if glasses.material_slots[p.material_index].material == m]) < 60
    if lens:
        m.name = "M_SmartGlasses_Lens"
        bsdf.inputs["Base Color"].default_value = (.06, .55, .66, 1); bsdf.inputs["Alpha"].default_value = .45
        bsdf.inputs["Metallic"].default_value = 0; bsdf.inputs["Roughness"].default_value = .08
        bsdf.inputs["Emission Color"].default_value = (.1, .75, .9, 1); bsdf.inputs["Emission Strength"].default_value = .6
        m.surface_render_method = "BLENDED"
    else:
        m.name = "M_SmartGlasses_Frame"
        bsdf.inputs["Base Color"].default_value = (.035, .04, .045, 1); bsdf.inputs["Metallic"].default_value = .6
        bsdf.inputs["Roughness"].default_value = .35

# ---------- headlamp ----------
new = import_new(bpy.ops.import_scene.gltf, LAMP)
body_names, mount_names = ("Lamp_low", "Glass_low", "Lightbars_low"), ("StrapMount_low", "PlasticClip_low")
group = lambda names: [o for o in new if o.type == "MESH" and o.parent and o.parent.name.split(".")[0] in names]
body, mount = group(body_names), group(mount_names)
flatten(new)
for o in new:
    if o not in body and o not in mount: bpy.data.objects.remove(o, do_unlink=True)   # strap and strap clips
mlo, mhi = world_bounds(mount)
back = Vector((mlo.x, (mlo.y + mhi.y) / 2, (mlo.z + mhi.z) / 2))   # mm, lamp faces +X
pivot = Vector((6.2, 0, .3))                                          # hinge between clip and lamp body
hinge = Matrix.Translation(pivot) @ Matrix.Rotation(PITCH, 4, "Y") @ Matrix.Translation(-pivot)
for o in body: o.data.transform(hinge)                                # lamp body swings back to level
normal = Vector((0, -math.cos(PITCH), math.sin(PITCH)))               # helmet surface normal at the mount
surface = Vector((0, -.098, 1.705))
place = (Matrix.Translation(surface - normal * .0015) @ Matrix.Rotation(math.radians(-90), 4, "Z")
         @ Matrix.Rotation(-PITCH, 4, "Y") @ Matrix.Scale(.001, 4) @ Matrix.Translation(-back))
for o in body + mount: o.data.transform(place)
lens_objs = [o for o in body if o.parent is None and "003" in o.name]   # Glass_low mesh
glo, ghi = world_bounds([o for o in body])
lamp = join(body + mount, "Helmet headlamp")
lamp.data.materials[0].name = "M_Headlamp"
images = sorted([i for i in bpy.data.images if i.packed_file], key=lambda i: i.name)
for img, name in zip(images, ("T_Headlamp_BaseColor", "T_Headlamp_MetallicRoughness", "T_Headlamp_Normal")):
    path = TEX + "/" + name + ".png"
    if name == "T_Headlamp_Normal": shutil.copy(LAMP_NORMAL_CLEAN, path)     # Duracell logo removed
    else: img.filepath_raw = path; img.file_format = "PNG"; img.save()
    img.unpack(method="REMOVE") if img.packed_file else None
    img.filepath = path; img.name = name; img.reload()

# Socket for the Unity spot light: centre of the lamp glass, facing the worker's forward.
socket = bpy.data.objects.new("HeadlampSocket", None); scene.collection.objects.link(socket)
socket.empty_display_size = .03
lens_center = Vector(((glo.x + ghi.x) / 2, glo.y, (glo.z + ghi.z) / 2))
socket.location = lens_center
for o in (glasses, lamp, socket): parent_to_bone(o, arm, "head")

# Character textures travel with the export: only images still used by a material.
used = set()
for o in scene.objects:
    if o.type != "MESH": continue
    for slot in o.material_slots:
        if slot.material and slot.material.node_tree:
            used |= {n.image for n in slot.material.node_tree.nodes if n.type == "TEX_IMAGE" and n.image}
for img in used:
    src = bpy.path.abspath(img.filepath)
    if not img.packed_file and os.path.exists(src) and not src.replace("\\", "/").startswith(TEX):
        dst = TEX + "/" + os.path.basename(src)
        shutil.copy(src, dst); img.filepath = dst
print("TEXTURES", sorted(os.path.basename(bpy.path.abspath(i.filepath)) for i in used))

tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in scene.objects if o.type == "MESH")
print("RESULT tris", tris, "glasses", tuple(round(v, 3) for v in world_bounds([glasses])[0]), tuple(round(v, 3) for v in world_bounds([glasses])[1]))
print("RESULT lamp", tuple(round(v, 3) for v in world_bounds([lamp])[0]), tuple(round(v, 3) for v in world_bounds([lamp])[1]), "socket", tuple(round(v, 3) for v in socket.matrix_world.translation))

bpy.ops.wm.save_as_mainfile(filepath=OUT + "/SafeMiningWorker.blend", relative_remap=True)
for pb in arm.pose.bones: pb.matrix_basis = Matrix.Identity(4)   # bone nodes are written from the current pose
bpy.ops.export_scene.fbx(filepath=OUT + "/SafeMiningWorker.fbx", object_types={"ARMATURE", "MESH", "EMPTY"},
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="OFF",
                         path_mode="RELATIVE", embed_textures=False, use_armature_deform_only=False)

# ---------- review renders ----------
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = scene.render.resolution_y = 900
world = bpy.data.worlds.new("w"); scene.world = world; world.color = (.18, .18, .19)
for rot in ((math.radians(55), 0, math.radians(-30)), (math.radians(60), 0, math.radians(150))):
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun)
    sun.rotation_euler = rot; sun.data.energy = 3.5


def shoot(name, pos, target, lens=50):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name)); scene.collection.objects.link(cam)
    cam.data.lens = lens; cam.location = pos
    cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam; scene.render.filepath = OUT + "/review-" + name + ".png"
    bpy.ops.render.render(write_still=True)


shoot("full", (1.6, -3.6, 1.2), (0, 0, .92), 45)
shoot("head-front", (0, -.75, 1.66), (0, 0, 1.64), 60)
shoot("head-side", (.75, -.05, 1.68), (0, -.03, 1.64), 60)
shoot("head-three", (.45, -.55, 1.78), (0, -.04, 1.64), 60)
