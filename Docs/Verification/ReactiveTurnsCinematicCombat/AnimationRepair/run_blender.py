from pathlib import Path
import subprocess, sys, os
base = Path(__file__).parent
script = base / (sys.argv[1] if len(sys.argv) > 1 else 'inspect_blender.py')
log = base / (script.stem + '.log')
args = [r'D:/3DMODELS/blender.exe', '--background', '--factory-startup', '--python', str(script)]
env = os.environ.copy()
if len(sys.argv)>2 and sys.argv[2]=='preview': env['ROKAS_ANIMATION_PREVIEW']='1'
with log.open('w', encoding='utf-8') as f:
    result = subprocess.run(args, stdout=f, stderr=subprocess.STDOUT, env=env)
print('EXIT', result.returncode, 'LOG', log)
print(log.read_text(encoding='utf-8', errors='replace'))
