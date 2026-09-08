from pathlib import Path

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
print("Migrated Live Messenger 1.1 legacy return assertions")
