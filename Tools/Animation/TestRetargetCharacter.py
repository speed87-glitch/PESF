"""Run in Blender background mode; real evaluated armature/constraint regression."""
import math
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import RetargetCharacter as retarget

rig = ET.fromstring('''<Scene><Nodes>
<A Type="Node" X="100" Y="0" Z="0" Mass="1"/>
<B Type="Node" X="200" Y="0" Z="0" Mass="1"/>
<Middle Type="MacroNode" NodesCount="2" ChildNode1="A" ChildNode2="B" LCC1="0.5" LCC2="0.5"/>
</Nodes><Edges/><Figures/></Scene>''')
data = bpy.data.armatures.new('Donor')
obj = bpy.data.objects.new('Donor', data)
bpy.context.scene.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bone = data.edit_bones.new('Root')
bone.head = (0, 0, 0)
bone.tail = (0, 1, 0)
bpy.ops.object.mode_set(mode='OBJECT')
scene = bpy.context.scene
scene.render.fps = 30
scene.frame_set(1)
obj.location = (3, 2, 1)
obj.keyframe_insert(data_path='location', frame=1)
obj.location.x += 1
obj.keyframe_insert(data_path='location', frame=3)
for curve in obj.animation_data.action.fcurves:
    for key in curve.keyframe_points:
        key.interpolation = 'LINEAR'
scene.frame_set(8, subframe=0.25)
source, clip = retarget.sample(rig, obj, {'A': 'Root', 'B': 'Root'}, 1, 1, 3, 100)
assert scene.frame_current == 8 and scene.frame_subframe == .25
assert len(source['frames']) == 3 and len(clip['frames']) == 5
def near(actual, expected):
    assert max(abs(a - b) for a, b in zip(actual, expected)) < .001, (actual, expected)
near(clip['frames'][0][0], [100, 0, 0])
near(clip['frames'][-1][0], [200, 0, 0])
near(clip['frames'][1][0], [125, 0, 0])
near(clip['frames'][-1][2], [250, 0, 0])

# A real pose-bone constraint supplies the motion, not raw object keyframes.
obj.animation_data_clear()
obj.location = (0, 0, 0)
target = bpy.data.objects.new('Driver', None)
scene.collection.objects.link(target)
constraint = obj.pose.bones['Root'].constraints.new('COPY_ROTATION')
constraint.target = target
constraint.owner_space = 'WORLD'
constraint.target_space = 'WORLD'
target.rotation_euler = (0, 0, 0)
target.keyframe_insert(data_path='rotation_euler', frame=1)
target.rotation_euler.y = math.pi / 2
target.keyframe_insert(data_path='rotation_euler', frame=3)
source, clip = retarget.sample(rig, obj, {'A': 'Root', 'B': 'Root'}, 1, 1, 3, 100)
near(clip['frames'][0][1], [200, 0, 0])
near(clip['frames'][-1][1], [0, -200, 0])
near(clip['frames'][-1][2], [0, -150, 0])

def reject(bindings, scale=100, end=3):
    try:
        retarget.sample(rig, obj, bindings, 1, 1, end, scale)
    except ValueError:
        return
    raise AssertionError('Invalid mapping/range was accepted')

reject({'A': 'Root'})
reject({'A': 'Root', 'B': 'Missing'})
reject({'A': 'Root', 'B': 'Root', 'Middle': 'Root'})
reject({'A': 'Root', 'B': 'Root'}, scale=0)
reject({'A': 'Root', 'B': 'Root'}, scale=float('nan'))
reject({'A': 'Root', 'B': 'Root'}, end=1)
obj.scale = (0, 1, 1)
reject({'A': 'Root', 'B': 'Root'})
assert scene.frame_current == 8 and scene.frame_subframe == .25
print('PASS: evaluated retarget translation, constrained rotation, calibration, helper nodes, resampling, frame restoration and invalid mappings.')
