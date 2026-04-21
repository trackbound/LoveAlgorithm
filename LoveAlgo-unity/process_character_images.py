"""
캐릭터 이미지 파이프라인
========================
Character Origin/ (한글 원본) → Resources/Characters/ (Char_{Id}_{Emote}.png)

사용법:
    python process_character_images.py [--max-height 2000] [--dry-run]

새 표정 추가 시:
    1. Character Origin/ 에 한글 이름 PNG 드롭
    2. 아래 CHAR_NAME_MAP / EMOTE_NAME_MAP 에 항목 추가 (필요 시)
    3. 스크립트 실행
"""

import argparse
import sys
from pathlib import Path

# Pillow 필요
try:
    from PIL import Image, ImageFilter
    Image.MAX_IMAGE_PIXELS = 200_000_000  # 고해상도 원본 허용
except ImportError:
    print("ERROR: Pillow가 필요합니다.  pip install Pillow")
    sys.exit(1)

# Windows cp949 콘솔에서도 이모지/한글 안전 출력
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

# ─── 경로 설정 ───────────────────────────────────────────────
SCRIPT_DIR = Path(__file__).resolve().parent
ORIGIN_DIR = SCRIPT_DIR / "Assets" / "Character Origin"
OUTPUT_DIR = SCRIPT_DIR / "Assets" / "Resources" / "Characters"

# ─── 매핑 테이블 (새 항목은 여기에 추가) ──────────────────────
# 한글 캐릭터 이름 → 영문 ID
CHAR_NAME_MAP = {
    "봄이":  "Bom",
    "다은":  "Daeun",
    "로아":  "Roa",
    "예은":  "Yeun",
    "희원이": "Heewon",
    "희원":  "Heewon",
}

# 한글 표정 이름 → 영문 파일명
EMOTE_NAME_MAP = {
    "기본":     "Default",
    "깜짝":     "Surprise",
    "눈웃음":   "EyeSmile",
    "밝게웃음": "BrightSmile",
    "울먹":     "Tearful",
    "찌릿":     "Glare",
    "활짝웃음": "Happy",
    "활짝":     "Happy",
    "부끄":     "Shy",
}


def parse_korean_filename(stem: str):
    """한글 파일명에서 (캐릭터ID, 영문표정) 추출. 실패 시 (None, None)."""
    stem = stem.strip()

    # 캐릭터 이름 매칭 (긴 것부터 시도, '희원이' 먼저 '희원'보다)
    sorted_names = sorted(CHAR_NAME_MAP.keys(), key=len, reverse=True)
    char_id = None
    emote_kr = None

    for kr_name in sorted_names:
        if stem.startswith(kr_name):
            char_id = CHAR_NAME_MAP[kr_name]
            emote_kr = stem[len(kr_name):].strip()
            break

    if char_id is None:
        return None, None

    emote_en = EMOTE_NAME_MAP.get(emote_kr)
    if emote_en is None:
        return char_id, None  # 캐릭터는 매칭됐지만 표정이 매핑 안 됨

    return char_id, emote_en


def process_image(src: Path, dst: Path, max_height: int):
    """원본을 다단계 Lanczos로 다운스케일 후 UnsharpMask로 디테일 복원."""
    img = Image.open(src)

    # 알파 보존을 위해 RGBA 통일
    if img.mode != "RGBA":
        img = img.convert("RGBA")

    w, h = img.size
    downscaled = False
    if h > max_height:
        ratio = max_height / h
        new_w = round(w * ratio)
        # reducing_gap=3.0 → 큰 축소 비율에서 내부적으로 box reduce 후 Lanczos 적용
        # (Photoshop "bicubic sharper" 와 유사한 다단계 처리)
        img = img.resize((new_w, max_height), Image.LANCZOS, reducing_gap=3.0)
        downscaled = True

    # 큰 폭 다운스케일 후 디테일 복원 (캐릭터 라인아트 선명화)
    if downscaled:
        img = img.filter(ImageFilter.UnsharpMask(radius=1.5, percent=160, threshold=2))

    # PNG 최적화 저장 (투명도 유지)
    img.save(dst, "PNG", optimize=True)
    return img.size


def main():
    parser = argparse.ArgumentParser(description="캐릭터 이미지 파이프라인")
    parser.add_argument("--max-height", type=int, default=2000,
                        help="출력 최대 높이 (기본: 2000)")
    parser.add_argument("--dry-run", action="store_true",
                        help="실제 파일 생성 없이 매핑만 출력")
    args = parser.parse_args()

    if not ORIGIN_DIR.exists():
        print(f"ERROR: 원본 폴더가 없습니다: {ORIGIN_DIR}")
        sys.exit(1)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    pngs = sorted(ORIGIN_DIR.glob("*.png"))
    if not pngs:
        print(f"WARNING: 원본 폴더에 PNG가 없습니다: {ORIGIN_DIR}")
        sys.exit(0)

    created = []
    skipped = []
    unmapped = []

    for src in pngs:
        char_id, emote_en = parse_korean_filename(src.stem)

        if char_id is None:
            unmapped.append((src.name, "캐릭터 이름 매핑 없음"))
            continue
        if emote_en is None:
            unmapped.append((src.name, f"표정 매핑 없음 (캐릭터: {char_id})"))
            continue

        out_name = f"Char_{char_id}_{emote_en}.png"
        dst = OUTPUT_DIR / out_name

        if args.dry_run:
            created.append((src.name, out_name, "-"))
            continue

        size = process_image(src, dst, args.max_height)
        size_kb = dst.stat().st_size / 1024
        created.append((src.name, out_name, f"{size[0]}x{size[1]}, {size_kb:.0f}KB"))

    # ─── 결과 출력 ───────────────────────────────────────────
    print(f"\n{'=' * 60}")
    print(f"캐릭터 이미지 파이프라인 결과")
    print(f"{'=' * 60}")

    if created:
        print(f"\n✅ 생성: {len(created)}개")
        for src_name, out_name, info in created:
            print(f"   {src_name} → {out_name}  [{info}]")

    if skipped:
        print(f"\n⏭️ 스킵 (변경 없음): {len(skipped)}개")
        for name in skipped:
            print(f"   {name}")

    if unmapped:
        print(f"\n⚠️ 매핑 실패: {len(unmapped)}개")
        for name, reason in unmapped:
            print(f"   {name} — {reason}")
        print("\n   → CHAR_NAME_MAP 또는 EMOTE_NAME_MAP에 항목을 추가하세요.")

    print(f"\n{'=' * 60}")
    total = len(created) + len(unmapped)
    print(f"총 {total}개 파일 중 {len(created)}개 변환 완료")
    if args.dry_run:
        print("(--dry-run 모드: 실제 파일 미생성)")
    print()


if __name__ == "__main__":
    main()
