"""시나리오 CSV 전수 검증 — 재생 전 최종 점검"""
import csv
import io
import sys
import os

csv_path = os.path.join(os.path.dirname(__file__), "..", "..", "시나리오 (업데이트)_clean.csv")

lines_raw = open(csv_path, encoding="utf-8-sig").readlines()

errors = []
warns = []

# 파싱
parsed = []  # (line_no, cols)
line_ids = set()
for i, raw in enumerate(lines_raw):
    r = raw.strip()
    if not r or r.startswith("#"):
        parsed.append((i + 1, None))
        continue
    reader = csv.reader(io.StringIO(r))
    cols = next(reader, [])
    parsed.append((i + 1, cols))
    if cols and cols[0].strip():
        line_ids.add(cols[0].strip())

# 알려진 표정 이름
EMOTES = {"Default", "EyeSmile", "BrightSmile", "Happy", "Glare", "Surprise", "Tearful"}

# 배경 테이블 (데모 배경 테이블.csv에서)
KNOWN_BGS = {
    "BG_Black", "BG_MyRoom_Desk", "BG_MyRoom_Bed_Day", "BG_MyRoom_Bed_Night",
    "BG_MyRoom_Night_LightOn", "BG_Engineering_Classroom", "BG_Engineering_StudentLounge",
    "BG_StudentCenter_Front_Day", "BG_StudentCenter_Office", "BG_StudentCenter_Board",
    "BG_ConvenienceStore_Inside", "BG_Campus_Street1_Day",
    # 별칭/추가
    "Black",
}

# 지원 타입
VALID_TYPES = {"Text", "Char", "BG", "CG", "SD", "Overlay", "Sound", "FX", "Flow", "Choice", "Option", "Place"}

# FX 명령
VALID_FX = {
    "FadeOut", "FadeIn", "Flash",
    "CamShake", "StageShake", "DialogueShake",
    "CamZoom", "EyeOpen", "EyeClose", "EyeCloseImmediate", "EyeBlink",
    "CharShake", "CharJump", "CharDim",
    "DayEnd", "DayStart", "SceneEnd", "SceneStart", "Setup", "Wait",
    "DialogueHide", "DialogueShow",
}

# Flow 명령
VALID_FLOW = {"Jump", "End", "Save", "If", "MiniGame", "LoadingScene"}

# Char 액션
VALID_CHAR_ACTIONS = {"Enter", "EnterUp", "Emote", "Exit", "ExitDown"}

for line_no, cols in parsed:
    if cols is None:
        continue

    # 헤더 행 스킵
    if len(cols) >= 2 and cols[1].strip() == "Type":
        continue

    typ = cols[1].strip() if len(cols) > 1 else ""
    val = cols[3].strip() if len(cols) > 3 else ""
    nxt = cols[4].strip() if len(cols) > 4 else ""

    if not typ:
        continue

    # 1. 타입 유효성
    if typ not in VALID_TYPES:
        errors.append(f"Line {line_no}: 알 수 없는 Type '{typ}'")
        continue

    # 2. FX 명령 검증
    if typ == "FX" and val:
        cmd = val.split(":")[0]
        if cmd not in VALID_FX:
            errors.append(f"Line {line_no}: 알 수 없는 FX 명령 '{cmd}' (전체: {val})")

    # 3. Flow 명령 검증
    if typ == "Flow" and val:
        parts = val.split(":")
        cmd = parts[0]
        if cmd not in VALID_FLOW:
            errors.append(f"Line {line_no}: 알 수 없는 Flow 명령 '{cmd}'")
        if cmd == "Jump" and len(parts) > 1:
            target = parts[1]
            if target not in line_ids:
                errors.append(f"Line {line_no}: Jump 대상 '{target}' 없음")

    # 4. Char 검증
    if typ == "Char" and val:
        parts = val.split(":")
        if len(parts) < 2:
            errors.append(f"Line {line_no}: Char 형식 오류 (최소 슬롯:액션 필요): {val}")
        else:
            slot = parts[0]
            action = parts[1]
            if slot.upper() not in ("L", "C", "R"):
                errors.append(f"Line {line_no}: 잘못된 슬롯 '{slot}'")
            if action not in VALID_CHAR_ACTIONS:
                errors.append(f"Line {line_no}: 잘못된 Char 액션 '{action}'")
            if action in ("Enter", "EnterUp") and len(parts) >= 3:
                char_name = parts[2]
                if char_name in EMOTES:
                    errors.append(f"Line {line_no}: 캐릭터 위치에 표정 이름 '{char_name}' (캐릭터ID 누락?): {val}")

    # 5. BG 검증
    if typ == "BG" and val:
        bg_name = val.split(":")[0]
        if bg_name not in KNOWN_BGS:
            warns.append(f"Line {line_no}: 배경 '{bg_name}'이 데모 배경 테이블에 없음 (오타?)")

    # 6. Option jump target 검증
    if typ == "Option" and val:
        parts = val.split("|")
        if len(parts) >= 2 and parts[1].strip():
            target = parts[1].strip()
            if target not in line_ids:
                errors.append(f"Line {line_no}: Option 점프 대상 '{target}' 없음")

    # 7. Sound 검증
    if typ == "Sound" and val:
        parts = val.split(":")
        cat = parts[0]
        if cat not in ("BGM", "SFX", "Voice"):
            warns.append(f"Line {line_no}: 알 수 없는 Sound 카테고리 '{cat}'")

# 결과 출력
print(f"\n{'='*50}")
print(f"전수 검증 결과: {len(errors)} ERROR, {len(warns)} WARN")
print(f"{'='*50}")

if errors:
    print("\n[ERROR]")
    for e in errors:
        print(f"  ❌ {e}")

if warns:
    print("\n[WARN]")
    for w in warns:
        print(f"  ⚠️  {w}")

if not errors and not warns:
    print("\n✅ 문제 없음! 재생 가능합니다.")
elif not errors:
    print(f"\n⚠️  WARN {len(warns)}개 있지만 재생에는 문제 없습니다.")
