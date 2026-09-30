from pathlib import Path
from io_scene_fbx import parse_fbx
folder=Path(r'D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY/Assets/Rokas/Art/CombatActors/Keiko')
for file in ['Normal attack.fbx','Normal attack corrected.fbx']:
    root,version=parse_fbx.parse(str(folder/file))
    print('FILE',file,version)
    for element in root.elems:
        if element.id==b'GlobalSettings':
            for props in element.elems:
                if props.id==b'Properties70':
                    for p in props.elems:
                        if p.props[0] in [b'UnitScaleFactor',b'OriginalUnitScaleFactor',b'UpAxis',b'FrontAxis',b'CoordAxis']: print('GLOBAL',p.props)
        if element.id==b'Objects':
            for node in element.elems:
                if node.id==b'Model' and any(x in node.props[1] for x in [b'Armature',b'mixamorig:Hips']):
                    print('NODE',node.props)
                    for pset in node.elems:
                        if pset.id==b'Properties70':
                            for p in pset.elems:
                                if p.props[0] in [b'Lcl Translation',b'Lcl Rotation',b'Lcl Scaling',b'PreRotation',b'PostRotation']: print('PROP',p.props)
