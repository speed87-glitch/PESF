"""Create a synthetic donor for canonical-rig export/packaging integration tests."""
import argparse
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import bpy

parser = argparse.ArgumentParser()
parser.add_argument('--rig', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
rig = ET.parse(args.rig).getroot()
data = bpy.data.armatures.new('Donor')
obj = bpy.data.objects.new('Donor', data)
bpy.context.scene.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
root = data.edit_bones.new('Root')
root.head = (0, 0, 0)
root.tail = (0, 0, 1)
arm = data.edit_bones.new('Arm')
arm.head = (0, 0, 2)
arm.tail = (1, 0, 2)
arm.parent = root
bpy.ops.object.mode_set(mode='OBJECT')
scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 1
scene.frame_end = 31
obj.keyframe_insert(data_path='location', frame=1)
obj.location.x = .5
obj.keyframe_insert(data_path='location', frame=31)
pose = obj.pose.bones['Arm']
pose.rotation_mode = 'XYZ'
pose.keyframe_insert(data_path='rotation_euler', frame=1)
pose.rotation_euler.y = .3
pose.keyframe_insert(data_path='rotation_euler', frame=31)
bindings = {n.tag: ('Arm' if n.tag in ('NElbow_1', 'NHand_1') else 'Root')
            for n in rig.find('Nodes') if n.get('Type') == 'Node'}
args.output.mkdir(parents=True, exist_ok=True)
(args.output / 'bindings.json').write_text(json.dumps({'version': 1, 'bindings': bindings}), encoding='utf-8')
(args.output / 'skin.xml').write_text('<Scene><Nodes/><Edges/><Figures><Preview Type="Triangle" Node1="NNeck" Node2="NShoulder_1" Node3="NShoulder_2"/></Figures></Scene>', encoding='utf-8')
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str((args.output / 'donor.blend').resolve()))
