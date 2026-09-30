from pathlib import Path
import json
script=Path('D:/Rokas/reactiveturns-b2-staging/cinematic/author_polish_ii_animation.py')
exec(script.read_text(encoding='utf-8').split("bake(idle_source,'Two handed sword block'")[0])
OUTPUT=EVIDENCE/'scaled-dodge-staging';OUTPUT.mkdir(exist_ok=True)
DODGE[-1]=(.8,READY,READY_BLADE)
path=EVIDENCE/'animation_bake_report.json'
previous=json.loads(path.read_text())
bake(idle_source,'Two handed dodge backstep',25,(1,1),lambda t,n:lerp_keys(DODGE,t),contact=.24,body=body_dodge)
previous['sources'].update(REPORT['sources'])
previous['takes'].update(REPORT['takes'])
previous['dodgeRecoveryRig']=REPORT['dodgeRecoveryRig']
previous['takes']['Two handed dodge backstep']['recoveryCompensation']='Measured current stage stature and source posed mesh; inverse yaw cancels both world X and Z planted-foot drift'
path.write_text(json.dumps(previous,indent=2))
print('DODGE METRICS',REPORT['dodgeRecoveryRig'])
