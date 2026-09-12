"""Blender: transfer an evaluated armature animation onto an SF2 point rig.

Uses explicit bone bindings and a calibration frame, never guessed bone names.
Run inside the donor .blend; see the character authoring guide.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import CharacterPipeline as pipeline


def sample(rig, armature, bindings, reference, start, end, scale):
    if armature is None or armature.type != 'ARMATURE':
        raise ValueError('Select an existing armature object by name')
    if not isinstance(bindings, dict):
        raise ValueError('bindings must map each native Node name to a bone name')
    nodes = list(rig.find('Nodes'))
    required = {n.tag for n in nodes if n.get('Type') == 'Node'}
    if set(bindings) != required:
        raise ValueError('Binding mismatch; missing: ' + ', '.join(sorted(required - set(bindings)))
                         + '; unknown/derived: ' + ', '.join(sorted(set(bindings) - required)))
    for name, bone in bindings.items():
        if not isinstance(bone, str) or bone not in armature.pose.bones:
            raise ValueError(f'{name}: source bone does not exist: {bone}')
    scale = pipeline.finite(scale, 'scale')
    if scale <= 0:
        raise ValueError('scale must be positive native units per Blender unit')
    scene = bpy.context.scene
    fps = scene.render.fps / scene.render.fps_base
    if not 1 <= fps <= 240:
        raise ValueError('Source scene frame rate must be 1..240 fps')
    if end <= start or end - start + 1 > 36000 or (end - start + 1) * len(nodes) > 2000000:
        raise ValueError('Sample range requires 2..36000 frames and at most two million node samples')
    previous, subframe = scene.frame_current, scene.frame_subframe

    def matrices():
        evaluated = armature.evaluated_get(bpy.context.evaluated_depsgraph_get())
        return {name: evaluated.matrix_world @ evaluated.pose.bones[bone].matrix
                for name, bone in bindings.items()}

    try:
        scene.frame_set(reference)
        calibration = matrices()
        offsets = {}
        for node in nodes:
            if node.tag not in bindings:
                continue
            matrix = calibration[node.tag]
            if abs(matrix.determinant()) < 1e-10:
                raise ValueError(f'{node.tag}: singular source bone transform at calibration frame')
            x, y, z = [float(node.get(axis, 0)) for axis in 'XYZ']
            # SF2 Y is up; Blender Z is up. Calibrate in world space so
            # object parenting, object motion and evaluated constraints survive.
            offsets[node.tag] = matrix.inverted() @ Vector((x / scale, -z / scale, y / scale))
        frames = []
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            transforms = matrices()
            positions = {}
            for name, offset in offsets.items():
                point = transforms[name] @ offset
                positions[name] = [pipeline.finite(v * scale, f'{name} frame {frame}')
                                   for v in (point.x, point.z, -point.y)]
            pipeline.helper_positions(rig, positions)
            frames.append([positions[node.tag] for node in nodes])
        source = {'version': 1, 'fps': fps, 'names': [n.tag for n in nodes], 'frames': frames}
        return source, pipeline.bake(rig, source)
    finally:
        scene.frame_set(previous, subframe=subframe)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rig', type=Path, required=True)
    parser.add_argument('--mapping', type=Path, required=True)
    parser.add_argument('--armature', required=True)
    parser.add_argument('--reference-frame', type=int, required=True)
    parser.add_argument('--start', type=int, required=True)
    parser.add_argument('--end', type=int, required=True)
    parser.add_argument('--scale', type=float, default=100)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    mapping = json.loads(args.mapping.read_text(encoding='utf-8-sig'))
    if not isinstance(mapping, dict) or set(mapping) != {'version', 'bindings'} or mapping['version'] != 1:
        raise ValueError('Mapping requires version 1 and bindings only')
    if args.output.exists():
        raise ValueError('Choose a new output directory; existing work is never overwritten')
    rig = pipeline.model(args.rig)
    source, clip = sample(rig, bpy.data.objects.get(args.armature), mapping['bindings'],
                          args.reference_frame, args.start, args.end, args.scale)
    args.output.mkdir(parents=True)
    animation = args.output / 'retargeted.bytes'
    pipeline.write_animation(animation, clip)
    (args.output / 'frames.json').write_text(json.dumps(source), encoding='utf-8')
    metadata = {'version': 1, 'fps': 60, 'mid_frames': 0, 'frames': len(clip['frames']),
                'nodes': clip['names'], 'rig_sha256': hashlib.sha256(args.rig.read_bytes()).hexdigest(),
                'animation_sha256': hashlib.sha256(animation.read_bytes()).hexdigest()}
    animation.with_suffix('.rig.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    pipeline.preview(args.output / 'preview.html', rig, clip)
    (args.output / 'retarget.json').write_text(json.dumps({
        'version': 1, 'mapping': mapping, 'armature': args.armature,
        'reference_frame': args.reference_frame, 'start': args.start, 'end': args.end,
        'scale': args.scale, 'source_fps': source['fps'],
    }, indent=2), encoding='utf-8')
    print('Retargeted animation: ' + str(animation.resolve()))


if __name__ == '__main__':
    main()
