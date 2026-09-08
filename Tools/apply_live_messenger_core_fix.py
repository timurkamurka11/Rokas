from pathlib import Path

path = Path("Assets/Rokas/Scripts/Core/GameSession.cs")
text = path.read_text(encoding="utf-8")
old = '''            LiveMessages = new LiveMessengerService(state, Messages);\n            LiveMessages.Changed += NotifyChanged;\n'''
new = '''            LiveMessages = new LiveMessengerService(state, Messages);\n'''
if old not in text:
    raise SystemExit("Expected LiveMessages Changed subscription was not present after core patch")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
print("Preserved existing GameSession.Changed notification cardinality")
