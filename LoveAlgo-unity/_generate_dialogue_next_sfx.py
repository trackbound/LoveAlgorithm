"""
Generate a soft dialogue-next click sound for the VN.
Output: Assets/Resources/Audio/UI/ui_dialogue_next.wav
"""

import numpy as np
import struct
import os

SAMPLE_RATE = 44100

def write_wav(path: str, samples: np.ndarray, sr: int = SAMPLE_RATE):
    """Write mono 16-bit WAV."""
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

def generate_soft_click():
    """
    Soft, warm dialogue-next sound.
    - A gentle tonal 'pop' using layered sine waves
    - Quick exponential decay for a snappy but soft feel
    - Subtle low-frequency body for warmth
    """
    duration = 0.12  # 120ms total
    t = np.linspace(0, duration, int(SAMPLE_RATE * duration), endpoint=False)

    # --- Layer 1: Primary soft pop (mid-high tone) ---
    freq1 = 1200  # Hz - bright but not harsh
    env1 = np.exp(-t * 45)  # fast decay
    layer1 = np.sin(2 * np.pi * freq1 * t) * env1

    # --- Layer 2: Warm body (lower tone) ---
    freq2 = 600  # Hz - gives warmth
    env2 = np.exp(-t * 35)
    layer2 = np.sin(2 * np.pi * freq2 * t) * env2

    # --- Layer 3: Sub presence ---
    freq3 = 300
    env3 = np.exp(-t * 50)
    layer3 = np.sin(2 * np.pi * freq3 * t) * env3

    # --- Layer 4: Gentle high shimmer ---
    freq4 = 2400
    env4 = np.exp(-t * 60)
    layer4 = np.sin(2 * np.pi * freq4 * t) * env4 * 0.15

    # Mix layers
    mix = layer1 * 0.4 + layer2 * 0.35 + layer3 * 0.15 + layer4 * 0.1

    # Soft attack (2ms fade-in to avoid click)
    attack_samples = int(SAMPLE_RATE * 0.002)
    mix[:attack_samples] *= np.linspace(0, 1, attack_samples)

    # Normalize to comfortable level
    peak = np.max(np.abs(mix))
    if peak > 0:
        mix = mix / peak * 0.7  # -3dB headroom

    return mix

if __name__ == "__main__":
    base = os.path.dirname(os.path.abspath(__file__))
    out_path = os.path.join(base, "Assets", "Resources", "Audio", "UI", "ui_dialogue_next.wav")

    samples = generate_soft_click()
    write_wav(out_path, samples)

    dur_ms = len(samples) / SAMPLE_RATE * 1000
    print(f"Generated: {out_path}")
    print(f"Duration: {dur_ms:.0f}ms, Samples: {len(samples)}, Rate: {SAMPLE_RATE}Hz")
