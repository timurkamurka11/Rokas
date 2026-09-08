from pathlib import Path

path = Path("Tools/apply_live_messenger_presentation.py")
text = path.read_text(encoding="utf-8")
label = '"home unread build")'
label_at = text.find(label)
if label_at < 0:
    raise SystemExit("home unread build label was not found")
start = text.rfind("replace_once(home,", 0, label_at)
if start < 0:
    raise SystemExit("home unread build replace_once start was not found")
end = label_at + len(label)

replacement = r'''replace_once(home,
''' + "'''" + r'''            ActionButton(parent, "LaptopHotspot", "YOMI  /  Ноутбук", HomeActionGlyph.Laptop, 950, 638, 390,\n                () => open("laptop"));\n''' + "'''" + r''',
''' + "'''" + r'''            ActionButton(parent, "LaptopHotspot", "YOMI  /  Ноутбук", HomeActionGlyph.Laptop, 950, 638, 390,\n                () => open("laptop"));\n            RectTransform unread = ui.Rect(parent, "HomeLaptopUnreadIndicator", 1260, 628, 34, 34);\n            laptopUnreadIndicator = unread.gameObject.AddComponent<CanvasGroup>();\n            Surface(unread, "UnreadGlow", 0, 0, 34, 34, 17, new Color(.18f, .86f, .90f, .86f));\n            Surface(unread, "UnreadCore", 10, 10, 14, 14, 7, new Color(.90f, .98f, 1f, .95f));\n            UpdateLaptopUnreadIndicator();\n''' + "'''" + r''', "home unread build")'''

fixed = text[:start] + replacement + text[end:]
path.write_text(fixed, encoding="utf-8")
print("Repaired HomeView unread-indicator anchor in presentation patch")
