from pathlib import Path

path = Path("Assets/Rokas/Tests/PlayMode/LiveMessengerPlayModeTests.cs")
text = path.read_text(encoding="utf-8")
old = '''            Assert.That(FindButtonWithPrefix("MessagesReaction_"), Is.Not.Null,\n                "Yumiko incoming history should expose lightweight safe reaction controls");\n'''
new = '''            Assert.That(FindButtonWithPrefix("MessagesReactionOpen_"), Is.Not.Null,\n                "Yumiko incoming history should expose the per-message Live Messenger 1.1 reaction opener");\n'''
if old not in text:
    raise SystemExit("Missing legacy Yumiko reaction UI anchor")
text = text.replace(old, new, 1)
old = '''            Assert.That(FindButtonWithPrefix("MessagesReaction_"), Is.Null,\n                "Guild must not expose casual warm emoji-style reactions");\n'''
new = '''            Assert.That(FindButtonWithPrefix("MessagesReactionOpen_"), Is.Not.Null,\n                "player reaction picker must remain available on Guild incoming messages; Guild formality is enforced by authored NPC reaction rules");\n'''
if old not in text:
    raise SystemExit("Missing legacy Guild reaction UI anchor")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
print("Migrated Live Messenger 1.1 PlayMode reaction UI expectations")
