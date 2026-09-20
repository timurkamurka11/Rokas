from pathlib import Path

path = Path("Assets/Rokas/Scripts/Core/LiveMessengerService.cs")
text = path.read_text(encoding="utf-8")
old = '''            if (rule == ContextRule.HasContractContext)\n                return state.contractRunSequence > 0 || state.completedRuns > 0 || state.phase != RunPhase.Home;\n'''
new = '''            if (rule == ContextRule.HasContractContext)\n                return state.completedRuns > 0 || state.phase != RunPhase.Home;\n'''
if old not in text:
    raise SystemExit("Expected HasContractContext production anchor was not found")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
print("Separated live contract availability from cooldown-only contractRunSequence changes")
