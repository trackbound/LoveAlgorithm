# Generates horizontal soft-pink gradient strip used as LogPopup narration/user row background.
# Output: Assets/Art/UI/Log/LogRowBg_Pink.png  (512x64, RGBA, soft horizontal gradient)
# Run: c:/Users/chris/GitHub/LoveAlgorithm/.venv/Scripts/python.exe LoveAlgo-unity/_generate_log_bg.py
import os
import sys
from PIL import Image

W, H = 512, 64
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Assets", "Art", "UI", "Log")
os.makedirs(OUT_DIR, exist_ok=True)
OUT = os.path.join(OUT_DIR, "LogRowBg_Pink.png")

# Center pink color (rich), edges fade to transparent
center = (255, 175, 205)         # soft pink
peak_alpha = 170                 # ~0.66
edge_alpha = 0                   # fully transparent at edges

img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
px = img.load()

# Vertical fade — strongest in middle row, fades to top/bottom (subtle)
def vfactor(y):
    cy = (H - 1) / 2.0
    d = abs(y - cy) / cy
    # smooth ease-out
    return max(0.0, 1.0 - d * d)

# Horizontal fade — peak in middle, smoothly to edges
def hfactor(x):
    cx = (W - 1) / 2.0
    d = abs(x - cx) / cx
    # smoothstep-ish
    return max(0.0, 1.0 - d * d * d)

for y in range(H):
    vf = vfactor(y)
    for x in range(W):
        hf = hfactor(x)
        a = int(round(peak_alpha * hf * (0.55 + 0.45 * vf)))
        if a <= 0:
            continue
        px[x, y] = (center[0], center[1], center[2], a)

img.save(OUT, "PNG", optimize=True)
print(f"wrote: {OUT}  ({W}x{H} RGBA)")
