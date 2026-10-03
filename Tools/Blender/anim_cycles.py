"""Reports clip length, in-place drift, loop seam error and foot-contact cycle length for a Mixamo FBX."""
import bpy, sys
from mathutils import Vector

path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
scene = bpy.context.scene
arm = next(o for o in scene.objects if o.type == "ARMATURE")
act = arm.animation_data.action
start, end = (int(f) for f in act.frame_range)
bones = {n: next(b for b in arm.pose.bones if b.name.endswith(n)) for n in ("Hips", "LeftFoot", "RightFoot", "Head")}


def pose(f):
    scene.frame_set(f)
    return {n: (arm.matrix_world @ b.head).copy() for n, b in bones.items()}, {b.name: b.matrix.copy() for b in arm.pose.bones}


p0, m0 = pose(start); p1, m1 = pose(end)
seam = max((m0[k].translation - m1[k].translation).length for k in m0)
rot = max(m0[k].to_quaternion().rotation_difference(m1[k].to_quaternion()).angle for k in m0)
left = []
for f in range(start, end + 1):
    p, _ = pose(f); left.append(p["LeftFoot"].z)
# Foot strikes: local minima of the left foot height.
strikes = [start + i for i in range(1, len(left) - 1) if left[i] <= left[i - 1] and left[i] < left[i + 1] and left[i] < min(left) + .03]
drift = (Vector((p1["Hips"].x, p1["Hips"].y, 0)) - Vector((p0["Hips"].x, p0["Hips"].y, 0))).length
print(f"CLIP {path.split('/')[-1]}: frames {start}-{end} ({(end - start) / scene.render.fps:.2f}s) drift {drift:.3f} m "
      f"seam pos {seam:.4f} rot {rot * 57.3:.2f} deg | left-foot strikes {strikes}")
