"""
Generate all UI sound effects for the VN.
Output directory: Assets/Resources/Audio/UI/

Sounds:
  - ui_hover.wav          : Soft, subtle hover highlight
  - ui_click.wav          : Gentle but responsive click
  - ui_dialogue_next.wav  : Soft tonal pop for advancing dialogue
  - ui_choice_hover.wav   : Warm shimmer for choice hover
  - ui_choice_select.wav  : Satisfying confirm tone for choice selection
  - ui_choice_appear.wav  : Gentle whoosh/chime for choices appearing
  - ui_popup_open.wav     : Soft rising tone for popup open
  - ui_popup_close.wav    : Soft descending tone for popup close
  - ui_save.wav           : Warm confirm chime for save complete
  - ui_load.wav           : Gentle ascending sweep for load complete
"""

import numpy as np
import struct
import os

SAMPLE_RATE = 44100

def write_wav(path: str, samples: np.ndarray, sr: int = SAMPLE_RATE):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = np.clip(samples, -1.0, 1.0)
    pcm16 = (pcm * 32767).astype(np.int16)
    data = pcm16.tobytes()
    n = len(data)
    with open(path, "wb") as f:
        f.write(b"RIFF")
        f.write(struct.pack("<I", 36 + n))
        f.write(b"WAVE")
        f.write(b"fmt ")
        f.write(struct.pack("<IHHIIHH", 16, 1, 1, sr, sr * 2, 2, 16))
        f.write(b"data")
        f.write(struct.pack("<I", n))
        f.write(data)

def fade_in(samples, ms):
    n = int(SAMPLE_RATE * ms / 1000)
    samples[:n] *= np.linspace(0, 1, n)
    return samples

def normalize(samples, peak_db=-3):
    peak = np.max(np.abs(samples))
    if peak > 0:
        target = 10 ** (peak_db / 20)
        samples = samples / peak * target
    return samples


# ─────────────────────────────────────────────
# 1. Hover – very subtle, airy tick
# ─────────────────────────────────────────────
def gen_hover():
    dur = 0.06  # 60ms – snappy
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    # High, soft tick
    tone = np.sin(2 * np.pi * 2200 * t) * np.exp(-t * 80)
    # Tiny sub body
    body = np.sin(2 * np.pi * 800 * t) * np.exp(-t * 90) * 0.3

    mix = tone + body
    mix = fade_in(mix, 1)
    return normalize(mix, -6)  # quieter for hover


# ─────────────────────────────────────────────
# 2. Click – gentle but distinct pop
# ─────────────────────────────────────────────
def gen_click():
    dur = 0.08  # 80ms
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    # Primary click tone
    primary = np.sin(2 * np.pi * 1400 * t) * np.exp(-t * 55)
    # Warm mid
    mid = np.sin(2 * np.pi * 700 * t) * np.exp(-t * 50) * 0.4
    # Tiny transient snap
    snap = np.sin(2 * np.pi * 3000 * t) * np.exp(-t * 120) * 0.15

    mix = primary + mid + snap
    mix = fade_in(mix, 1)
    return normalize(mix, -4)


# ─────────────────────────────────────────────
# 3. Dialogue Next – soft tonal pop (already exists, regenerate for consistency)
# ─────────────────────────────────────────────
def gen_dialogue_next():
    dur = 0.12  # 120ms
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    layer1 = np.sin(2 * np.pi * 1200 * t) * np.exp(-t * 45) * 0.4
    layer2 = np.sin(2 * np.pi * 600 * t) * np.exp(-t * 35) * 0.35
    layer3 = np.sin(2 * np.pi * 300 * t) * np.exp(-t * 50) * 0.15
    layer4 = np.sin(2 * np.pi * 2400 * t) * np.exp(-t * 60) * 0.1

    mix = layer1 + layer2 + layer3 + layer4
    mix = fade_in(mix, 2)
    return normalize(mix, -3)


# ─────────────────────────────────────────────
# 4. Choice Hover – warmer, slightly musical shimmer
# ─────────────────────────────────────────────
def gen_choice_hover():
    dur = 0.10  # 100ms
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    # Warm bell-like tone
    bell = np.sin(2 * np.pi * 1000 * t) * np.exp(-t * 40)
    # Harmonic shimmer
    shimmer = np.sin(2 * np.pi * 1500 * t) * np.exp(-t * 50) * 0.3
    # Sub warmth
    sub = np.sin(2 * np.pi * 500 * t) * np.exp(-t * 55) * 0.25

    mix = bell + shimmer + sub
    mix = fade_in(mix, 1.5)
    return normalize(mix, -5)  # subtle


# ─────────────────────────────────────────────
# 5. Choice Select – satisfying confirm, two-tone rise
# ─────────────────────────────────────────────
def gen_choice_select():
    dur = 0.18  # 180ms – slightly longer for satisfaction
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    # Rising two-tone: starts at one pitch, slides up slightly
    freq_start = 800
    freq_end = 1200
    freq = freq_start + (freq_end - freq_start) * (t / dur) ** 0.5
    phase = 2 * np.pi * np.cumsum(freq) / SAMPLE_RATE
    primary = np.sin(phase) * np.exp(-t * 20)

    # Harmonic
    harm = np.sin(phase * 2) * np.exp(-t * 30) * 0.2
    # Warm base
    base = np.sin(2 * np.pi * 400 * t) * np.exp(-t * 25) * 0.3

    mix = primary + harm + base
    mix = fade_in(mix, 2)
    return normalize(mix, -3)


# ─────────────────────────────────────────────
# 6. Choice Appear – gentle whoosh + soft chime
# ─────────────────────────────────────────────
def gen_choice_appear():
    dur = 0.25  # 250ms – slightly longer for the "appearing" feel
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    # Filtered noise whoosh (rising)
    np.random.seed(42)
    noise = np.random.randn(len(t))
    # Bandpass-ish: multiply noise by a swept sine
    sweep_freq = 800 + 2000 * (t / dur)
    sweep = np.sin(2 * np.pi * sweep_freq * t)
    whoosh = noise * sweep * np.exp(-((t - 0.08) ** 2) / (2 * 0.04 ** 2)) * 0.15

    # Soft chime (delayed slightly)
    delay_samples = int(SAMPLE_RATE * 0.04)
    chime_t = np.maximum(t - 0.04, 0)
    chime = np.sin(2 * np.pi * 1400 * chime_t) * np.exp(-chime_t * 25) * 0.5
    chime[:delay_samples] = 0

    # Sub
    sub = np.sin(2 * np.pi * 500 * t) * np.exp(-t * 30) * 0.2

    mix = whoosh + chime + sub
    mix = fade_in(mix, 3)
    return normalize(mix, -4)


# ─────────────────────────────────────────────
# 7. Popup Open – soft rising airy tone
# ─────────────────────────────────────────────
def gen_popup_open():
    dur = 0.18
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    # Rising pitch sweep
    freq = 600 + 800 * (t / dur) ** 0.6
    phase = 2 * np.pi * np.cumsum(freq) / SAMPLE_RATE
    primary = np.sin(phase) * np.exp(-t * 18)

    # Soft harmonic
    harm = np.sin(phase * 1.5) * np.exp(-t * 25) * 0.2
    # Airy breath
    np.random.seed(77)
    breath = np.random.randn(len(t)) * np.exp(-t * 30) * 0.06

    mix = primary + harm + breath
    mix = fade_in(mix, 2)
    return normalize(mix, -5)


# ─────────────────────────────────────────────
# 8. Popup Close – soft descending, fading out
# ─────────────────────────────────────────────
def gen_popup_close():
    dur = 0.15
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    # Falling pitch
    freq = 1200 - 600 * (t / dur) ** 0.5
    phase = 2 * np.pi * np.cumsum(freq) / SAMPLE_RATE
    primary = np.sin(phase) * np.exp(-t * 30)

    # Sub thud
    sub = np.sin(2 * np.pi * 300 * t) * np.exp(-t * 45) * 0.25

    mix = primary + sub
    mix = fade_in(mix, 1.5)
    return normalize(mix, -6)


# ─────────────────────────────────────────────
# 9. Save Complete – warm two-note confirm chime
# ─────────────────────────────────────────────
def gen_save():
    dur = 0.30
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    # Note 1 (C5 ~523 Hz) for first half
    # Note 2 (E5 ~659 Hz) for second half → pleasant major third
    split = int(len(t) * 0.45)
    tone = np.zeros_like(t)

    t1 = t[:split]
    tone[:split] = np.sin(2 * np.pi * 523 * t1) * np.exp(-t1 * 15)

    t2 = t[split:] - t[split]
    tone[split:] = np.sin(2 * np.pi * 659 * t2) * np.exp(-t2 * 18) * 0.9

    # Warm sub
    sub = np.sin(2 * np.pi * 260 * t) * np.exp(-t * 20) * 0.2
    # Shimmer
    shimmer = np.sin(2 * np.pi * 1046 * t) * np.exp(-t * 25) * 0.1

    mix = tone + sub + shimmer
    mix = fade_in(mix, 2)
    return normalize(mix, -3)


# ─────────────────────────────────────────────
# 10. Load Complete – gentle ascending arpeggio sweep
# ─────────────────────────────────────────────
def gen_load():
    dur = 0.35
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)

    # Three quick ascending notes: C5 → E5 → G5
    notes = [(523, 0.0, 0.12), (659, 0.10, 0.12), (784, 0.20, 0.15)]
    mix = np.zeros_like(t)

    for freq, onset, length in notes:
        mask = (t >= onset) & (t < onset + length)
        local_t = t[mask] - onset
        env = np.exp(-local_t * 18)
        mix[mask] += np.sin(2 * np.pi * freq * local_t) * env

    # Sub body across full duration
    sub = np.sin(2 * np.pi * 260 * t) * np.exp(-t * 15) * 0.15

    mix = mix + sub
    mix = fade_in(mix, 2)
    return normalize(mix, -3)


# ─────────────────────────────────────────────
# Generate all
# ─────────────────────────────────────────────
if __name__ == "__main__":
    base = os.path.dirname(os.path.abspath(__file__))
    out_dir = os.path.join(base, "Assets", "Resources", "Audio", "UI")

    sounds = {
        "ui_hover.wav":         gen_hover,
        "ui_click.wav":         gen_click,
        "ui_dialogue_next.wav": gen_dialogue_next,
        "ui_choice_hover.wav":  gen_choice_hover,
        "ui_choice_select.wav": gen_choice_select,
        "ui_choice_appear.wav": gen_choice_appear,
        "ui_popup_open.wav":    gen_popup_open,
        "ui_popup_close.wav":   gen_popup_close,
        "ui_save.wav":          gen_save,
        "ui_load.wav":          gen_load,
    }

    for filename, gen_fn in sounds.items():
        path = os.path.join(out_dir, filename)
        samples = gen_fn()
        write_wav(path, samples)
        dur_ms = len(samples) / SAMPLE_RATE * 1000
        print(f"  {filename:30s}  {dur_ms:5.0f}ms  {len(samples):6d} samples")

    print(f"\nAll {len(sounds)} UI sounds generated in {out_dir}")
