from pathlib import Path

path = Path("Assets/Rokas/Scripts/Presentation/WorldEffects.cs")
text = path.read_text()
old = """            if (!home)\n            {\n                lightningAge = -1f;\n                thunderCountdown = -1f;\n"""
new = """            if (!home)\n            {\n                if (lightningAge >= 0f) nextLightning = 0f;\n                lightningAge = -1f;\n                thunderCountdown = -1f;\n"""
if old not in text:
    raise SystemExit("Home exit lightning reset block not found exactly once")
if text.count(old) != 1:
    raise SystemExit(f"expected one Home exit lightning reset block, got {text.count(old)}")
path.write_text(text.replace(old, new, 1))
print(f"applied minimal active-lightning Home-exit reschedule fix to {path}")
