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

assemblies = {}
for path in list((ROOT / 'Assets').rglob('*.asmdef')) + list((ROOT / 'Assets').rglob('*.json')) + [ROOT / 'Packages/manifest.json']:
    try:
        data = json.loads(path.read_text())
        if path.suffix == '.asmdef':
            name = data.get('name')
            if not name or name in assemblies:
                errors.append('Missing or duplicate assembly name: ' + str(path.relative_to(ROOT)))
            else:
                assemblies[name] = data
    except Exception as exc:
        errors.append('Invalid JSON: ' + str(path.relative_to(ROOT)) + ': ' + str(exc))

# Unity package imports still require the Editor. Check local assembly boundaries and
# the explicitly declared uGUI dependency here so a misspelled reference cannot pass as JSON.
dependencies = json.loads((ROOT / 'Packages/manifest.json').read_text()).get('dependencies', {})
external_assemblies = {'UnityEngine.UI'} if 'com.unity.ugui' in dependencies else set()
for name, data in assemblies.items():
    for reference in data.get('references', []):
        if reference not in assemblies and reference not in external_assemblies:
            errors.append('Unknown assembly reference: ' + name + ' -> ' + reference)
        elif reference in assemblies:
            target = assemblies[reference]
            if target.get('includePlatforms') == ['Editor'] and data.get('includePlatforms') != ['Editor']:
                errors.append('Player assembly references Editor-only code: ' + name + ' -> ' + reference)

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
    matches = re.findall(r'^  ' + name + r': \{fileID: ([1-9][0-9]*), guid: ([0-9a-f]{32}), type: 3\}', asset, re.M)
    if len(matches) != 1:
        errors.append('Missing presentation binding: ' + name)
        continue
    file_id, guid = matches[0]
    expected = (('12800000', '.ttf') if name in ('sans', 'serif') else
                ('4900000', '.json') if name == 'contract' else
                ('2800000', '.png') if name in fields[:5] else ('8300000', '.wav'))
    target = guids.get(guid)
    if not target or (file_id, target.suffix.lower()) != expected:
        errors.append('Wrong imported asset type in binding: ' + name)

if errors:
    print('\n'.join('FAIL: ' + error for error in errors))
    sys.exit(1)
print(f'PASS: {asset_count} assets, {len(guids)} unique metas, {references} resolved serialized GUID references.')
print(f'PASS: {len(images)} PNGs (RGBA characters), {audio_count} stereo PCM assets, 18 presentation bindings, enabled bootstrap scene.')
print(f'PASS: {len(assemblies)} assembly definitions and declared references; presentation binding file types match.')
for name, width, height in images:
    print(f'  {name}: {width}x{height}')
print('Unity import, C# presentation compilation, PlayMode and Windows player validation are separate required checks.')
