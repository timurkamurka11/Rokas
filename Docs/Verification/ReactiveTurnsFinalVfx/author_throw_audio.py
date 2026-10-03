"""Deterministic original stereo PCM Throw release and blade contact layers."""
from pathlib import Path
import math, random, wave, struct, hashlib, json
project = Path(__file__).resolve().parents[3]
out = project / 'Assets/Rokas/Resources/Combat/ReactiveTurns/Audio/FinalVfx'
out.mkdir(parents=True, exist_ok=True)
rate = 22050
rng = random.Random(20260930)
manifest = []
for name, length in [('ThrowRelease', .28), ('ThrowContact', .22)]:
    samples = []
    low = previous = 0.
    for i in range(int(rate * length)):
        t = i / rate
        noise = rng.uniform(-1., 1.)
        low = low * .92 + noise * .08
        high = noise - low
        if name == 'ThrowRelease':
            envelope = math.sin(math.pi * t / length) ** 2
            sweep = math.sin(2 * math.pi * (1700 * t - 2100 * t * t))
            sample = (.21 * high + .08 * sweep) * envelope
        else:
            envelope = math.exp(-t * 34) * min(1., t / .004)
            body = math.sin(2 * math.pi * 174 * t) * math.exp(-t * 42)
            sample = .34 * high * envelope + .18 * body + .08 * math.sin(2 * math.pi * 2340 * t) * math.exp(-t * 46)
        samples.append(max(-.7, min(.7, sample)))
    dest = out / (name + '.wav')
    with wave.open(str(dest), 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(rate)
        w.writeframes(b''.join(struct.pack('<hh', round(s * 32767), round(s * 32767)) for s in samples))
    manifest.append({'file': str(dest.relative_to(project)), 'sha256': hashlib.sha256(dest.read_bytes()).hexdigest(),
                     'seconds': length, 'sampleRate': rate, 'channels': 2, 'peak': max(abs(s) for s in samples)})
(Path(__file__).parent / 'ThrowAudio.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(json.dumps(manifest, indent=2))
