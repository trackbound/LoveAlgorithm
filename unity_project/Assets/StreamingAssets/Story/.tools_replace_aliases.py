#!/usr/bin/env python3
"""
Prologue.csv 한글 별칭 치환 — 1회용.
ResourceCatalogSO에 등록된 한글 별칭으로 CSV 내 Id를 일괄 변환.

실행: python3 .tools_replace_aliases.py
"""
import csv
import io
import os
import shutil
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(ROOT, "Prologue.csv")
BAK = os.path.join(ROOT, "Prologue.csv.bak_pre_alias")

# ── 매핑 테이블 ──
# Character (Char Enter/EnterUp:character)
CHAR_MAP = {
    "c01": "로아",
    "Roa": "로아",
    "Daeun": "서다은",
    "SeoDaEun": "서다은",
    "Yeun": "하예은",
    "HaYeEun": "하예은",
    "Heewon": "도희원",
    "DoHeewon": "도희원",
    "Bom": "이봄",
    "LeeBom": "이봄",
}

# Emote codes → Korean
EMOTE_MAP = {
    "_00": "기본",
    "_11": "눈웃음",
    "_12": "밝게웃음",
    "_13": "활짝웃음",
    "_14": "행복",
    "_21": "찌릿",
    "_22": "쌔짐",
    "_23": "머쓱",
    "_24": "어질어질",
    "_31": "울먹",
    "_32": "주르륵",
    "_33": "와아앙",
    "_34": "부끄",
    "_35": "졸려",
    "_41": "깜짝",
    "_42": "반짝빈짝",
    "_43": "궁금",
    "_44": "윙크",
    "_45": "자신만만",
    "_55": "음주",
    "_56": "만취",
    "_57": "집중",
    "_58": "고민",
}

# BGM → Korean (catalog에 있는 것만)
BGM_MAP = {
    "Roa": "로아",
    "Daily2": "일상2",
    "Daeun": "서다은",
    "Yeun": "하예은",
    "Heewon": "도희원",
    "Bom": "이봄",
    "white_noise": "백색소음",
    # Daily1, Night 등은 catalog에 없어서 폴백 그대로 둠
}

# SD → Korean
SD_MAP = {
    "sd_c02_01": "다은 속닥",
    "sd_c04_01": "희원 맥주",
    "sd_c05_01": "봄 글썹",
}

# CG → Korean
CG_MAP = {
    "cg_c01_01": "로아 첫만남",
    "cg_c03_01": "예은 입부신청서 작성",
}

# BG → Korean (catalog에 있는 것만)
BG_MAP = {
    "bg_00_00": "빈 화면",
    "bg_10_01": "자취방 전경 낮",
    "bg_10_02": "자취방 전경 밤 불꺼짐",
    "bg_10_03": "자취방 전경 밤 불커짐",
    "bg_10_04": "자취방 침대위 아침",
    "bg_10_05": "자취방 침대위 밤",
    "bg_10_06": "자취방 책상 모니터",
    "bg_20_01": "공대 앞 낮",
    "bg_20_02": "공대 앞 밤",
    "bg_20_03": "공대 강의실복도",
    "bg_20_04": "공대 학생복지실",
    "bg_20_05": "공대 강의실 낮",
    "bg_20_06": "공대 강의실 낮 벚꽃",
    "bg_30_01": "캠퍼스거리1 낮 맑음",
    "bg_30_02": "캠퍼스거리2 낮 맑음",
    "bg_40_01": "학생회관 앞 낮",
    "bg_40_02": "학생회관 앞 밤",
    "bg_40_03": "행정실",
    "bg_40_04": "학생회관 복도",
    "bg_40_05": "학생회관 게시판앞",
    "bg_40_06": "학생회관 동아리방 낮",
    "bg_40_07": "학생회관 동아리방 벚꽃",
    "bg_60_02": "편의점 앞 밤",
    # bg_60_01 catalog에 없음 — 영문 ID 그대로 (Resources 폴백)
}

stats = {"BG": 0, "CG": 0, "SD": 0, "Char": 0, "Emote": 0, "BGM": 0}


def replace_in_value(line_type: str, value: str) -> str:
    """LineType별 Value 변환."""
    if not value:
        return value
    parts = value.split(":")

    if line_type == "BG":
        if parts[0] in BG_MAP:
            old = parts[0]
            parts[0] = BG_MAP[old]
            stats["BG"] += 1
        return ":".join(parts)

    if line_type == "CG":
        if parts[0] in CG_MAP:
            parts[0] = CG_MAP[parts[0]]
            stats["CG"] += 1
        return ":".join(parts)

    if line_type == "SD":
        if parts[0] in SD_MAP:
            parts[0] = SD_MAP[parts[0]]
            stats["SD"] += 1
        return ":".join(parts)

    if line_type == "Char":
        # Slot:Action:Char[:Emote[:Mode]] 또는 Slot:Emote:emote 등
        if len(parts) >= 2:
            action = parts[1].lower()
            if action in ("enter", "enterup"):
                # parts[2] = character
                if len(parts) >= 3 and parts[2] in CHAR_MAP:
                    parts[2] = CHAR_MAP[parts[2]]
                    stats["Char"] += 1
                # parts[3] = emote (optional)
                if len(parts) >= 4 and parts[3] in EMOTE_MAP:
                    parts[3] = EMOTE_MAP[parts[3]]
                    stats["Emote"] += 1
            elif action == "emote":
                # parts[2] = emote
                if len(parts) >= 3 and parts[2] in EMOTE_MAP:
                    parts[2] = EMOTE_MAP[parts[2]]
                    stats["Emote"] += 1
        return ":".join(parts)

    if line_type == "Sound":
        # BGM:name 또는 SFX:name 또는 Voice:name
        if len(parts) >= 2 and parts[0] == "BGM":
            if parts[1] in BGM_MAP:
                parts[1] = BGM_MAP[parts[1]]
                stats["BGM"] += 1
        return ":".join(parts)

    return value


def main():
    if not os.path.exists(SRC):
        print(f"ERROR: {SRC} 없음", file=sys.stderr)
        sys.exit(1)

    # 백업
    if not os.path.exists(BAK):
        shutil.copy2(SRC, BAK)
        print(f"백업 생성: {BAK}")
    else:
        print(f"백업 이미 존재: {BAK}")

    # 읽기
    with open(SRC, "r", encoding="utf-8", newline="") as f:
        reader = csv.reader(f)
        rows = list(reader)

    # 변환
    for i, row in enumerate(rows):
        if len(row) < 5:
            continue
        if i == 0 and row[0] == "LineID":
            continue  # 헤더 스킵
        line_type = row[1].strip() if len(row) > 1 else ""
        value = row[3] if len(row) > 3 else ""
        if line_type and value:
            row[3] = replace_in_value(line_type, value)

    # 쓰기 (LF 유지, UTF-8 BOM 없음, 한글 그대로)
    out = io.StringIO()
    w = csv.writer(out, lineterminator="\n", quoting=csv.QUOTE_MINIMAL)
    for row in rows:
        w.writerow(row)
    with open(SRC, "w", encoding="utf-8", newline="") as f:
        f.write(out.getvalue())

    print(f"치환 완료:")
    for k, v in stats.items():
        print(f"  {k}: {v}건")


if __name__ == "__main__":
    main()
