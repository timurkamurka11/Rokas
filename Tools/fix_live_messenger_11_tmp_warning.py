from pathlib import Path

path = Path("Assets/Rokas/Scripts/Presentation/LaptopMessagesView.cs")
text = path.read_text(encoding="utf-8")
old = "            text.enableWordWrapping = true;\n"
new = "            text.textWrappingMode = TMPro.TextWrappingModes.Normal;\n"
if old not in text:
    raise SystemExit("Missing obsolete TMP word-wrapping anchor")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
print("Replaced obsolete TMP enableWordWrapping API")
