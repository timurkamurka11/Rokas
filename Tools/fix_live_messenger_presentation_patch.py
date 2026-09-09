from pathlib import Path

path = Path("Tools/apply_live_messenger_presentation.py")
text = path.read_text(encoding="utf-8")


def replace_block(source, owner, label, replacement):
    marker = '"' + label + '")'
    label_at = source.find(marker)
    if label_at < 0:
        raise SystemExit(label + " label was not found")
    start = source.rfind("replace_once(" + owner + ",", 0, label_at)
    if start < 0:
        raise SystemExit(label + " replace_once start was not found")
    end = label_at + len(marker)
    return source[:start] + replacement + source[end:]


home_replacement = r'''replace_once(home,
''' + "'''" + r'''            ActionButton(parent, "LaptopHotspot", "YOMI  /  Ноутбук", HomeActionGlyph.Laptop, 950, 638, 390,\n                () => open("laptop"));\n''' + "'''" + r''',
''' + "'''" + r'''            ActionButton(parent, "LaptopHotspot", "YOMI  /  Ноутбук", HomeActionGlyph.Laptop, 950, 638, 390,\n                () => open("laptop"));\n            RectTransform unread = ui.Rect(parent, "HomeLaptopUnreadIndicator", 1260, 628, 34, 34);\n            laptopUnreadIndicator = unread.gameObject.AddComponent<CanvasGroup>();\n            Surface(unread, "UnreadGlow", 0, 0, 34, 34, 17, new Color(.18f, .86f, .90f, .86f));\n            Surface(unread, "UnreadCore", 10, 10, 14, 14, 7, new Color(.90f, .98f, 1f, .95f));\n            UpdateLaptopUnreadIndicator();\n''' + "'''" + r''', "home unread build")'''
text = replace_block(text, "home", "home unread build", home_replacement)

view_state_replacement = r'''replace_once(view,
''' + "'''" + r'''        private int laptopOpenedFrame = -1;\n''' + "'''" + r''',
''' + "'''" + r'''        private int laptopOpenedFrame = -1;\n        private int observedMessageSequence;\n\n        public string LastMessageAudioCue { get; private set; }\n        public int MessageAudioCueCount { get; private set; }\n''' + "'''" + r''', "view routing state")'''
text = replace_block(text, "view", "view routing state", view_state_replacement)

path.write_text(text, encoding="utf-8")
print("Repaired HomeView and RokasView anchors in presentation patch")
