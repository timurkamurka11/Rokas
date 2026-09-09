from pathlib import Path

path = Path('.github/workflows/home-weather-3.yml')
text = path.read_text()
old_sha = '510cdb2c3c93690e144a5a06842f69f4c6f9badf'
new_sha = '3035975cce69ba1034ecd3ddd4cf719083715c56'
old_tree = '7976b497cca51db776a8d7645cf4afa58ddb0992'
new_tree = '386a02647fef70ad6e196e5ae032ebda84af6fe7'

if text.count(old_sha) < 2:
    raise SystemExit(f'expected multiple old SHA guards, got {text.count(old_sha)}')
if text.count(old_tree) < 2:
    raise SystemExit(f'expected multiple old tree guards, got {text.count(old_tree)}')
text = text.replace(old_sha, new_sha).replace(old_tree, new_tree)

old_scope = '''          test "$(git diff --name-only a05dc28dd7f81c1c76e4be8788c89c72b0398020..HEAD)" = "Assets/Rokas/Scripts/Presentation/WorldEffects.cs"\n'''
new_scope = '''          mapfile -t changed < <(git diff --name-only a05dc28dd7f81c1c76e4be8788c89c72b0398020..HEAD | sort)\n          test "${#changed[@]}" -eq 2\n          test "${changed[0]}" = "Assets/Rokas/Scripts/Presentation/WorldEffects.cs"\n          test "${changed[1]}" = "Assets/Rokas/Tests/PlayMode/HomeWeather3PlayModeTests.cs"\n'''
if text.count(old_scope) != 1:
    raise SystemExit(f'expected one old scope guard, got {text.count(old_scope)}')
text = text.replace(old_scope, new_scope, 1)

path.write_text(text)
print(f'retargeted {path} to {new_sha} tree {new_tree}')
