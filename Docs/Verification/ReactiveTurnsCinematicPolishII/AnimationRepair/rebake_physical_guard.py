from pathlib import Path
import json
script=Path('D:/Rokas/reactiveturns-b2-staging/cinematic/author_polish_ii_animation.py')
exec(script.read_text(encoding='utf-8').split("bake(idle_source,'Two handed sword block'")[0])
path=EVIDENCE/'animation_bake_report.json'
previous=json.loads(path.read_text())
bake(idle_source,'Two handed sword block',25,(1,1),lambda t,n:lerp_keys(GUARD,t),contact=.34,body=body_guard)
previous['sources'].update(REPORT['sources'])
previous['takes'].update(REPORT['takes'])
previous['takes']['Two handed sword block']['guardDesign']='two-handed hanging guard: downward blade intersects existing low descending Yokai claws; original contact .34s retained'
path.write_text(json.dumps(previous,indent=2))
