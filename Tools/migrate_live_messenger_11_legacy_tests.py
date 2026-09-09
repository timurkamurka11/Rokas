from pathlib import Path

# Live Messenger 1.0 Yumiko return expectations: core returns stay repeatable,
# while the 1.1 narrative continuation is first-story only per contract.
path = Path("Tests/Core/YumikoFoodIntegrationTests.cs")
text = path.read_text(encoding="utf-8")
old = '''                Equal(yumiko.entries.Count + 2, restoredYumiko.entries.Count,\n                    "reload must preserve history plus one deterministic live continuation for each real return");\n'''
new = '''                Equal(yumiko.entries.Count + 1, restoredYumiko.entries.Count,\n                    "reload must preserve repeatable core return history plus one first-story live continuation");\n'''
if old not in text:
    raise SystemExit("Missing legacy Yumiko return count anchor")
text = text.replace(old, new, 1)
old = '''                Equal(2, CountEventPrefix(restoredYumiko, "live:yumiko:return:"),\n                    "two real returns must recover exactly two distinct live continuations");\n'''
new = '''                Equal(1, CountEventPrefix(restoredYumiko, "live:yumiko:return:"),\n                    "repeatable real returns must keep the first-story Yumiko live continuation exactly once");\n'''
if old not in text:
    raise SystemExit("Missing legacy Yumiko live return identity anchor")
path.write_text(text.replace(old, new, 1), encoding="utf-8")

# Live Messenger 1.0 core harness assumptions superseded by 1.1:
# authored typing now takes 3-5 seconds per incoming bubble, and the player
# reaction picker is available on formal Guild incoming messages too. Guild
# personality is enforced by authored NPC reactions, not by hiding the picker.
path = Path("Tests/Core/LiveMessengerCoreTests.cs")
text = path.read_text(encoding="utf-8")
old = '''        private static void DriveUntilChoices(GameSession session, object live, string contactId)\n        {\n            for (int index = 0; index < 12; index++)\n'''
new = '''        private static void DriveUntilChoices(GameSession session, object live, string contactId)\n        {\n            for (int index = 0; index < 20; index++)\n'''
if old not in text:
    raise SystemExit("Missing legacy Live Messenger choice timeout anchor")
text = text.replace(old, new, 1)
old = '''            Equal(0, Items(Invoke(live, "GetReactionOptions", "guild")).Count,\n                "Guild must not expose casual emoji-style reactions");\n'''
new = '''            Equal(10, Items(Invoke(live, "GetReactionOptions", "guild")).Count,\n                "player reaction picker must expose the 10 stable reactions on Guild incoming messages; Guild formality is enforced by authored NPC reaction rules");\n'''
if old not in text:
    raise SystemExit("Missing legacy Guild reaction picker anchor")
path.write_text(text.replace(old, new, 1), encoding="utf-8")

print("Migrated Live Messenger 1.1 legacy assertions")
