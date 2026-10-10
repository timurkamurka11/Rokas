"""Reproducible original ROKAS audio. Uses no recordings or third-party music."""
from pathlib import Path
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Rokas/Audio'
RATE = 22050
rng = np.random.default_rng(71043)


def write(name, values, peak=.76):
    values = np.asarray(values, dtype=float)
    if values.ndim == 1:
        values = np.column_stack((values, np.roll(values, 17)))
    values -= values.mean(axis=0)
    values *= min(1, peak / max(.001, np.abs(values).max()))
    samples = (np.clip(values, -.99, .99) * 32767).astype('<i2')
    ROOT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(ROOT / (name + '.wav')), 'wb') as file:
        file.setnchannels(2)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(samples.tobytes())


def time(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def cyclic_noise(n, cutoff, amplitude):
    # Periodic spectrum gives seamless rain / room loops without a hard splice.
    frequencies = np.fft.rfftfreq(n, 1 / RATE)
    spectrum = (rng.normal(size=len(frequencies)) + 1j * rng.normal(size=len(frequencies)))
    spectrum *= 1 / (1 + (frequencies / cutoff)**2)
    spectrum[0] = 0
    noise = np.fft.irfft(spectrum, n)
    return noise / max(np.std(noise), .0001) * amplitude


def pluck(t, hz):
    attack = 1 - np.exp(-t * 150)
    return attack * np.exp(-t * 2.4) * (np.sin(2 * np.pi * hz * t) + .3 * np.sin(2 * np.pi * hz * 2.006 * t))


def make_loops():
    t = time(24)
    n = len(t)
    rain = cyclic_noise(n, 1750, .05) + cyclic_noise(n, 95, .025)
    room = .018 * np.sin(2 * np.pi * 100 * t) + .008 * np.sin(2 * np.pi * 150 * t)
    write('HomeRain', rain + room)
    rail = cyclic_noise(n, 270, .045) + cyclic_noise(n, 2500, .012)
    rail += .025 * np.sin(2 * np.pi * 49 * t) * (.7 + .3 * np.sin(2 * np.pi * t / 12))
    rail += .008 * np.sin(2 * np.pi * 1000 * t)
    write('SubwayHum', rail)
    home = np.zeros(n)
    notes = [(0, 293.665), (3, 440), (6, 466.164), (10, 349.228), (12, 293.665), (16, 220), (19, 349.228), (21, 293.665)]
    for onset, hz in notes:
        tone = pluck(time(5), hz) * .10
        for i in range(len(tone)):
            home[(int(onset * RATE) + i) % n] += tone[i]
    home += .018 * np.sin(2 * np.pi * (146 + 1 / 24) * t)
    write('HomeNocturne', home)
    tension = .055 * np.sin(2 * np.pi * (73 + 1 / 24) * t) * (.6 + .25 * np.sin(2 * np.pi * t / 12))
    tension += .023 * np.sin(2 * np.pi * (110 + 1 / 24) * t)
    for onset, hz in [(1, 293.665), (8, 311.127), (14, 220), (20, 207.652)]:
        tone = pluck(time(4), hz) * .043
        idx = (np.arange(len(tone)) + int(onset * RATE)) % n
        tension[idx] += tone
    write('OtherSide', tension)


def make_effects():
    t = time(.13)
    write('UIWood', (.25 * np.sin(2 * np.pi * 610 * t) + rng.normal(0, .04, len(t))) * np.exp(-t * 55) * (1 - np.exp(-t * 2000)))
    t = time(.42)
    whoosh = rng.normal(0, .075, len(t)) * np.exp(-((t - .09) / .045)**2)
    body = .30 * np.sin(2 * np.pi * (85 * t + 32 * t**2)) * np.exp(-t * 16) * (1 - np.exp(-t * 150))
    write('BladeHit', whoosh + body)
    t = time(1.1)
    write('SealBreak', (pluck(t, 880) + .45 * pluck(t, 1174.66) + .2 * pluck(t, 1760)) * .24)
    t = time(1.35)
    envelope = np.sin(np.pi * t / 1.35)**2
    write('PortalCrossing', (rng.normal(0, .075, len(t)) + .11 * np.sin(2 * np.pi * (85 * t + 31 * t**2))) * envelope)
    t = time(2.4)
    write('ContractSealed', (pluck(t, 293.665) + .7 * pluck(t, 440) + .6 * pluck(t, 587.33)) * .19)
    t = time(.65)
    phase = 2 * np.pi * (200 * t + 45 * np.sin(t * 7) / 7)
    envelope = np.sin(np.pi * t / .65)**2
    write('MameMurmur', (.08 * np.sin(phase) + .035 * np.sin(phase * 2.1)) * envelope)


if __name__ == '__main__':
    make_loops()
    make_effects()
    print('Wrote 10 original stereo audio assets to', ROOT)
