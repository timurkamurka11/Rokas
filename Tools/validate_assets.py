"""Static integrity checks for authored Unity content. Does not replace Unity import or PlayMode."""
from pathlib import Path
import json
import re
import struct
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
errors = []
guids = {}
asset_count = 0

for path in sorted((ROOT / 'Assets').rglob('*')):
    if path.suffix == '.meta':
        continue
    asset_count += path.is_file()
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        errors.append('Missing meta: ' + str(path.relative_to(ROOT)))
        continue
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
    if not match:
        errors.append('Invalid GUID: ' + str(meta.relative_to(ROOT)))
        continue
    value = match.group(1)
    if value in guids:
        errors.append('Duplicate GUID: ' + value)
    guids[value] = path

references = 0
for path in list((ROOT / 'Assets').rglob('*.asset')) + list((ROOT / 'Assets').rglob('*.unity')) + list((ROOT / 'ProjectSettings').glob('*.asset')):
    for value in re.findall(r'guid: ([0-9a-f]{32})', path.read_text()):
        references += 1
        if value not in guids and int(value, 16) != 0:
            errors.append('Unresolved GUID ' + value + ' in ' + str(path.relative_to(ROOT)))

for path in list((ROOT / 'Assets').rglob('*.asmdef')) + list((ROOT / 'Assets').rglob('*.json')) + [ROOT / 'Packages/manifest.json']:
    try:
        json.loads(path.read_text())
    except Exception as exc:
        errors.append('Invalid JSON: ' + str(path.relative_to(ROOT)) + ': ' + str(exc))

images = []
for path in sorted((ROOT / 'Assets').rglob('*.png')):
    with path.open('rb') as stream:
        header = stream.read(33)
    if header[:8] != b'\x89PNG\r\n\x1a\n':
        errors.append('Invalid PNG: ' + path.name)
        continue
    width, height = struct.unpack('>II', header[16:24])
    images.append((path.name, width, height))
    if 'Yokai' in path.parts or 'Familiars' in path.parts:
        if header[25] != 6:
            errors.append('Character PNG must carry real RGBA: ' + path.name)
    elif width < 1600 or height < 900:
        errors.append('Background below HD: ' + path.name)

audio_count = 0
for path in sorted((ROOT / 'Assets').rglob('*.wav')):
    with wave.open(str(path)) as clip:
        if clip.getnchannels() != 2 or clip.getsampwidth() != 2 or clip.getnframes() <= 0:
            errors.append('Invalid stereo PCM source: ' + path.name)
        audio_count += 1

scene_path = 'Assets/Rokas/Scenes/Rokas.unity'
settings = (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text()
if not re.search(r'enabled: 1\s+path: ' + re.escape(scene_path), settings):
    errors.append('Required startup scene is not enabled.')
bootstrap = ROOT / 'Assets/Rokas/Scripts/Presentation/RokasBootstrap.cs'
bootstrap_guid = next((key for key, value in guids.items() if value == bootstrap), None)
if not bootstrap_guid or bootstrap_guid not in (ROOT / scene_path).read_text():
    errors.append('Scene does not reference RokasBootstrap.')

asset = (ROOT / 'Assets/Rokas/Resources/RokasAssets.asset').read_text()
fields = ['home', 'portal', 'subway', 'enemy', 'familiar', 'sans', 'serif', 'contract',
          'homeAmbience', 'subwayAmbience', 'homeMusic', 'missionMusic', 'click', 'hit', 'critical', 'portalSound', 'seal', 'mame']
for name in fields:
    if not re.search(r'^  ' + name + r': \{fileID: [1-9][0-9]*, guid: [0-9a-f]{32}, type: 3\}', asset, re.M):
        errors.append('Missing presentation binding: ' + name)

if errors:
    print('\n'.join('FAIL: ' + error for error in errors))
    sys.exit(1)
print(f'PASS: {asset_count} assets, {len(guids)} unique metas, {references} resolved serialized GUID references.')
print(f'PASS: {len(images)} PNGs (RGBA characters), {audio_count} stereo PCM assets, 18 presentation bindings, enabled bootstrap scene.')
for name, width, height in images:
    print(f'  {name}: {width}x{height}')
print('Unity import, C# presentation compilation, PlayMode and Windows player validation are separate required checks.')
