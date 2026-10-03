"""Finds the single-stride window in the Jog clip with the smallest loop seam, phase-matched to the walk cycle."""
import bpy, sys

path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
scene = bpy.context.scene
arm = next(o for o in scene.objects if o.type == "ARMATURE")
start, end = (int(f) for f in arm.animation_data.action.frame_range)
poses = {}
for f in range(start, end + 1):
    scene.frame_set(f)
    poses[f] = {b.name: b.matrix.copy() for b in arm.pose.bones}


def err(a, b):
    return max(poses[a][k].to_quaternion().rotation_difference(poses[b][k].to_quaternion()).angle * 57.3
               + (poses[a][k].translation - poses[b][k].translation).length * 100 for k in poses[a])


best = []
for length in range(22, 29):
    for a in range(start, end - length + 1):
        best.append((err(a, a + length), a, length))
best.sort()
for e, a, length in best[:6]:
    print(f"WINDOW frames {a}-{a + length} length {length} ({length / 30:.3f}s) seam {e:.2f}")
