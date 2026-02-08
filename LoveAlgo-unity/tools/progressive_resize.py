#!/usr/bin/env python3
"""
단계적 축소(Progressive Downscaling)로 캐릭터 이미지 재처리
원본(~20000px) → 2000px: 50%씩 줄여가며 최종 크기에 도달
"""

from PIL import Image
from pathlib import Path

Image.MAX_IMAGE_PIXELS = None

TARGET_HEIGHT = 2000

# 한글 파일명 → 영문 표정명 매핑
NAME_MAP = {
    "기본": "Default",
    "깜짝": "Surprised",
    "눈웃음": "Smile",
    "밝게웃음": "Happy",
    "울먹": "Sad",
    "찌릿": "Shy",
    "활짝웃음": "Laugh",
    "활짝": "Laugh",
}

FOLDERS = {
    "봄이": "Bom",
    "다은": "Daeun",
    "희원": "Heewon",
    "로아": "Roa",
    "예은": "Yeun",
}

SRC_ROOT = Path(r"C:\Users\podola\Downloads")
DST_ROOT = Path(__file__).parent.parent / "Assets" / "Resources" / "Characters"


def resolve_expression(filename: str, char_kor: str) -> str | None:
    """한글 파일명에서 표정명 추출"""
    stem = Path(filename).stem
    prefixes = [char_kor + "이 ", char_kor + "이", char_kor + " ", char_kor]
    for prefix in prefixes:
        if stem.startswith(prefix):
            expr = stem[len(prefix):]
            break
    else:
        expr = stem

    expr = expr.strip()
    for kor, eng in NAME_MAP.items():
        if expr == kor:
            return eng
    print(f"  WARNING: unmapped expression: '{expr}' from '{filename}'")
    return None


def progressive_resize(img: Image.Image, target_height: int) -> Image.Image:
    """단계적 축소: 50%씩 줄여가며 최종 크기에 도달 (화질 보존)"""
    w, h = img.size
    steps = 0

    # 50%씩 줄이다가 target의 2배 이하가 되면 마지막 한 번에 target으로
    while h > target_height * 2:
        w = w // 2
        h = h // 2
        img = img.resize((w, h), Image.LANCZOS)
        steps += 1

    # 최종 리사이즈
    ratio = target_height / h
    final_w = round(w * ratio)
    img = img.resize((final_w, target_height), Image.LANCZOS)
    steps += 1

    return img, steps


def main():
    total_saved = 0.0
    processed = 0
    errors = 0

    for kor, eng in FOLDERS.items():
        src_dir = SRC_ROOT / kor
        dst_dir = DST_ROOT / eng

        if not src_dir.exists():
            print(f"ERROR: {src_dir} not found")
            continue

        dst_dir.mkdir(parents=True, exist_ok=True)
        print(f"\n[{kor} -> {eng}]")

        for f in sorted(src_dir.glob("*.png")):
            expr = resolve_expression(f.name, kor)
            if expr is None:
                errors += 1
                continue

            dst = dst_dir / f"{expr}.png"

            with Image.open(f) as img:
                orig_w, orig_h = img.size
                orig_mb = f.stat().st_size / (1024 * 1024)

                if img.mode != "RGBA":
                    img = img.convert("RGBA")

                resized, steps = progressive_resize(img.copy(), TARGET_HEIGHT)
                resized.save(dst, "PNG", optimize=True)

                new_mb = dst.stat().st_size / (1024 * 1024)
                saved = orig_mb - new_mb

                print(f"  {f.name}")
                print(
                    f"    -> {expr}.png  {orig_w}x{orig_h} -> {resized.width}x{resized.height}"
                    f"  ({steps} steps)  {orig_mb:.1f}MB -> {new_mb:.1f}MB  (-{saved:.1f}MB)"
                )

                total_saved += saved
                processed += 1

    print(f"\n{'='*50}")
    print(f"  processed: {processed}, errors: {errors}")
    print(f"  total saved: {total_saved:.0f}MB")
    print(f"{'='*50}")


if __name__ == "__main__":
    main()
