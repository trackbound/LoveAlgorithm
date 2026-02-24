#!/usr/bin/env python3
"""
시나리오 CSV 정리 · 단순화 · 문법 검증 스크립트

사용법:
  python clean_scenario_csv.py -i "시나리오 (업데이트).csv"
  python clean_scenario_csv.py -i "시나리오 (업데이트).csv" -o "시나리오_clean.csv"
  python clean_scenario_csv.py -i "시나리오 (업데이트).csv" --validate-only
  python clean_scenario_csv.py -i "시나리오 (업데이트).csv" --no-simplify

기능:
  1) 정리(clean)    — trailing comma 제거, Notes 컬럼 제거, 주석행 정리
  2) 단순화(simplify) — default duration 파라미터 축약
  3) 검증(validate)  — 파서 문법과 대조하여 오류/경고 출력
"""

from __future__ import annotations

import argparse
import csv
import io
import os
import re
import sys
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Optional


# ─────────────────────────── 메시지 레벨 ───────────────────────────

class Severity(Enum):
    ERROR = "ERROR"
    WARN = "WARN"
    INFO = "INFO"


@dataclass
class Message:
    severity: Severity
    line: int          # CSV 원본 기준 행 번호 (1-based)
    text: str

    def __str__(self) -> str:
        tag = self.severity.value.ljust(5)
        return f"[{tag}] Line {self.line}: {self.text}"


# ─────────────────────────── 유효값 상수 ───────────────────────────

VALID_TYPES = {
    "text", "char", "bg", "cg", "sd", "overlay",
    "sound", "fx", "flow", "choice", "option", "place",
}

VALID_NEXT = {"", ">", "click", "await"}  # float도 허용 (별도 검사)

VALID_SLOTS = {"l", "c", "r"}

VALID_CHAR_ACTIONS = {"enter", "enterup", "emote", "exit", "exitdown"}

VALID_CHARACTERS = {"bom", "daeun", "heewon", "roa", "yeun"}

VALID_BG_TRANSITIONS = {"cut", "fade", "cross"}

VALID_SD_TRANSITIONS = {"fadein", "cross"}

VALID_SOUND_CATEGORIES = {"bgm", "sfx", "voice"}

# FX — 구현 완료
FX_IMPLEMENTED = {
    "fadeout", "fadein", "flash",
    "camshake", "stageshake", "dialogueshake",
    "camzoom",
    "eyeopen", "eyeclose", "eyecloseimmediate", "eyeblink",
    "charshake", "charjump", "chardim",
    "dialoguehide", "dialogueshow",
    "dayend", "daystart", "setup",
}

# FX — 계획됨 (미구현)
FX_PLANNED = {"sceneend", "scenestart", "wait"}

FX_ALL = FX_IMPLEMENTED | FX_PLANNED

# Flow 명령어
FLOW_IMPLEMENTED = {"jump", "end", "save", "if", "minigame"}
FLOW_PLANNED = {"loadingscene"}
FLOW_ALL = FLOW_IMPLEMENTED | FLOW_PLANNED

# Shake 프리셋
SHAKE_PRESETS = {"weak", "medium", "strong"}

# Roa 오버레이 (알려진 값)
ROA_OVERLAYS = {"roa_mob_default", "roa_mob_1", "roa_pc_negative"}

# Default duration 테이블 (단순화용)
DEFAULT_DURATIONS = {
    # BG transitions
    "bg_fade": 2.0,
    "bg_cross": 0.5,
    # CG
    "cg": 1.0,
    # FX
    "fadeout": 0.5,
    "fadein": 0.5,
    "flash": 0.1,
    "eyeopen": 0.8,
    "eyeclose": 0.8,
    "camzoom": 0.5,
    "shake_duration": 0.3,
    # BGM
    "bgm_fade": 3.0,
}


# ─────────────────────────── 테이블 로더 ───────────────────────────

def load_emote_table(path: Optional[str]) -> set[str]:
    """표정테이블.csv 로드 → 유효 emote 이름 집합 (한국어+영문, 소문자)"""
    defaults = {
        "default", "기본",
        "eyesmile", "눈웃음",
        "brightsmile", "밝게웃음",
        "happy", "활짝",
        "glare", "찌릿",
        "surprise", "깜짝",
        "tearful", "울먹",
    }
    if not path or not os.path.isfile(path):
        return defaults

    result: set[str] = set()
    with open(path, encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f)
        header = next(reader, None)
        if not header:
            return defaults
        for row in reader:
            for cell in row:
                cell = cell.strip()
                if cell:
                    result.add(cell.lower())
    return result if result else defaults


def load_bg_table(path: Optional[str]) -> set[str]:
    """데모 배경 테이블.csv 로드 → 유효 BG 이름 집합 (한국어+영문, 소문자)"""
    defaults = {
        "bg_black", "검정 배경",
        "bg_myroom_desk", "자취방 책상",
        "bg_myroom_bed_day", "자취방 침대 낮",
        "bg_myroom_bed_night", "자취방 침대 밤",
        "bg_myroom_night_lighton", "자취방 전경 밤 불켜짐",
        "bg_engineering_classroom", "공대 강의실",
        "bg_engineering_studentlounge", "공대 학생복지실",
        "bg_studentcenter_front_day", "학생회관 앞 낮",
        "bg_studentcenter_office", "학생회관 행정실",
        "bg_studentcenter_board", "학생회관 게시판",
        "bg_conveniencestore_inside", "편의점 내부",
        "bg_campus_street1_day", "캠퍼스거리1_낮",
    }
    if not path or not os.path.isfile(path):
        return defaults

    result: set[str] = set()
    with open(path, encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f)
        header = next(reader, None)
        if not header:
            return defaults
        for row in reader:
            for cell in row:
                cell = cell.strip()
                if cell:
                    result.add(cell.lower())
    return result if result else defaults


# ─────────────────────────── CSV 읽기 ───────────────────────────

def read_raw_lines(path: str) -> list[str]:
    """CSV 파일을 원본 텍스트 줄 단위로 읽기 (따옴표 내 줄바꿈 포함)"""
    with open(path, encoding="utf-8-sig") as f:
        return f.read()  # 전체 텍스트 반환


def parse_csv(text: str) -> list[tuple[int, list[str]]]:
    """
    CSV 텍스트 → [(원본_라인번호, [컬럼들])] 반환.
    csv.reader로 따옴표/줄바꿈 안전 처리.
    라인 번호는 1-based로 각 레코드의 시작 줄.
    """
    records: list[tuple[int, list[str]]] = []
    reader = csv.reader(io.StringIO(text))

    # csv.reader는 줄바꿈을 자동 처리하므로
    # 원본 행 번호 추적을 위해 텍스트의 줄 수를 세야 함
    line_starts: list[int] = []
    current_line = 1
    for row in csv.reader(io.StringIO(text)):
        line_starts.append(current_line)
        # 이 레코드가 차지하는 원본 줄 수 = 레코드 내 줄바꿈 수 + 1
        raw = ",".join(row)  # 근사치
        current_line += raw.count("\n") + 1

    # 다시 파싱 (정확한 방법)
    reader2 = csv.reader(io.StringIO(text))
    line_num = 0
    current_line = 1
    for row in reader2:
        records.append((current_line, row))
        # 행 내 줄바꿈 계산
        lines_in_record = sum(cell.count("\n") for cell in row) + 1
        current_line += lines_in_record

    return records


# ─────────────────────────── 정리(clean) ───────────────────────────

def clean_row(row: list[str], is_header: bool = False) -> list[str]:
    """Notes(인덱스 5) 이후 제거 + trailing 빈 셀 strip"""
    # Notes 컬럼(인덱스 5) 및 이후 모두 제거
    cleaned = row[:5] if len(row) >= 5 else list(row)

    # trailing 빈 셀 제거 (단, 최소 1셀은 유지)
    while len(cleaned) > 1 and cleaned[-1].strip() == "":
        cleaned.pop()

    return cleaned


def is_comment_line(row: list[str]) -> bool:
    """# 주석 행인지 판단"""
    if not row:
        return False
    first = row[0].strip()
    return first.startswith("#")


def is_blank_line(row: list[str]) -> bool:
    """모든 셀이 비어있는 행"""
    return all(cell.strip() == "" for cell in row)


def is_header_line(row: list[str]) -> bool:
    """헤더 행인지 판단"""
    if not row:
        return False
    return row[0].strip().lower() == "lineid"


# ─────────────────────────── 단순화(simplify) ───────────────────────────

def simplify_value(line_type: str, value: str) -> str:
    """
    default duration 파라미터가 명시되어 있으면 축약.
    반환: 단순화된 value
    """
    lt = line_type.lower()

    if lt == "bg":
        return _simplify_bg(value)
    elif lt == "cg":
        return _simplify_cg(value)
    elif lt == "fx":
        return _simplify_fx(value)
    elif lt == "sound":
        return _simplify_sound(value)
    elif lt == "char":
        return _simplify_char(value)
    return value


def _simplify_bg(value: str) -> str:
    parts = value.split(":")
    if len(parts) < 2:
        return value

    transition = parts[1].strip().lower() if len(parts) >= 2 else ""
    if len(parts) == 3:
        try:
            dur = float(parts[2].strip())
        except ValueError:
            return value
        default_dur = DEFAULT_DURATIONS.get(f"bg_{transition}", None)
        if default_dur is not None and abs(dur - default_dur) < 0.01:
            return f"{parts[0]}:{parts[1]}"
    return value


def _simplify_cg(value: str) -> str:
    parts = value.split(":")
    # CG_Name:Fade:1.0 → CG_Name:Fade (default CG = 1.0s)
    if len(parts) == 3:
        try:
            dur = float(parts[2].strip())
        except ValueError:
            return value
        if abs(dur - DEFAULT_DURATIONS["cg"]) < 0.01:
            return f"{parts[0]}:{parts[1]}"
    # CG_Name:1.0 → CG_Name
    if len(parts) == 2:
        try:
            dur = float(parts[1].strip())
        except ValueError:
            return value
        if abs(dur - DEFAULT_DURATIONS["cg"]) < 0.01:
            return parts[0]
    return value


def _simplify_fx(value: str) -> str:
    parts = value.split(":")
    effect = parts[0].strip().lower()

    # EyeOpen:0.8 → EyeOpen, FadeOut:0.5 → FadeOut, etc.
    if len(parts) == 2 and effect in DEFAULT_DURATIONS:
        try:
            dur = float(parts[1].strip())
        except ValueError:
            return value  # 프리셋 이름일 수 있음 (예: Strong)
        if abs(dur - DEFAULT_DURATIONS[effect]) < 0.01:
            return parts[0]

    # Shake 계열: Effect:0.3:Preset → Effect:Preset (duration이 default일 때)
    if len(parts) == 3 and effect in {"camshake", "stageshake", "dialogueshake"}:
        try:
            dur = float(parts[1].strip())
        except ValueError:
            return value
        if abs(dur - DEFAULT_DURATIONS["shake_duration"]) < 0.01:
            preset = parts[2].strip()
            if preset.lower() in SHAKE_PRESETS:
                return f"{parts[0]}:{preset}"

    return value


def _simplify_sound(value: str) -> str:
    parts = value.split(":")
    # BGM:Name:Fade:3.0 → BGM:Name (default BGM fade = 3.0s)
    if len(parts) == 4 and parts[0].strip().upper() == "BGM":
        if parts[2].strip().lower() == "fade":
            try:
                dur = float(parts[3].strip())
            except ValueError:
                return value
            if abs(dur - DEFAULT_DURATIONS["bgm_fade"]) < 0.01:
                return f"{parts[0]}:{parts[1]}"
    # BGM:Stop:Fade:3.0 → BGM:Stop
    if (
        len(parts) == 4
        and parts[0].strip().upper() == "BGM"
        and parts[1].strip().lower() == "stop"
        and parts[2].strip().lower() == "fade"
    ):
        try:
            dur = float(parts[3].strip())
        except ValueError:
            return value
        if abs(dur - DEFAULT_DURATIONS["bgm_fade"]) < 0.01:
            return f"{parts[0]}:{parts[1]}"
    return value


def _simplify_char(value: str) -> str:
    parts = value.split(":")
    # C:Enter:Roa:Default → C:Enter:Roa (Default emote 생략, overlay 없을 때)
    if (
        len(parts) == 4
        and parts[1].strip().lower() == "enter"
        and parts[3].strip().lower() == "default"
    ):
        return f"{parts[0]}:{parts[1]}:{parts[2]}"
    return value


def simplify_next(line_type: str, next_val: str) -> str:
    """Text의 Next가 click이면 빈값으로 축약 (기본값이므로)"""
    if line_type.lower() == "text" and next_val.strip().lower() == "click":
        return ""
    return next_val


def flatten_newlines(value: str) -> str:
    """
    Value 내 실제 개행을 처리하여 CSV 한 줄에 들어가도록 변환.
    - `\\n` + 실제개행 + 공백 → `\\n`  (이미 \n이 있으므로 실제개행 제거)
    - 단독 실제개행 → `\\n`  (실제개행을 \n 텍스트로 변환)
    - 연속 공백 정리
    """
    # 1) \n 뒤의 실제개행 + 선행/후행 공백 제거
    result = re.sub(r'\\n\s*\n\s*', r'\\n', value)
    # 2) 남은 실제개행을 \n으로 변환
    result = re.sub(r'\s*\n\s*', r'\\n', result)
    # 3) 끝 공백 제거
    result = result.strip()
    return result


# ─────────────────────────── 검증(validate) ───────────────────────────


class Validator:
    def __init__(
        self,
        emotes: set[str],
        bgs: set[str],
    ):
        self.emotes = emotes
        self.bgs = bgs
        self.messages: list[Message] = []
        self.line_ids: dict[str, int] = {}  # lineID → 첫 등장 행 번호
        self.jump_targets: list[tuple[int, str]] = []  # (행번호, target)
        self.option_targets: list[tuple[int, str]] = []
        self.stats = {
            "total": 0,
            "comments": 0,
            "data": 0,
            "blanks": 0,
            "choices": 0,
            "options": 0,
            "simplify_count": 0,
        }

    def _add(self, severity: Severity, line: int, text: str):
        self.messages.append(Message(severity, line, text))

    def error(self, line: int, text: str):
        self._add(Severity.ERROR, line, text)

    def warn(self, line: int, text: str):
        self._add(Severity.WARN, line, text)

    def info(self, line: int, text: str):
        self._add(Severity.INFO, line, text)

    # ── 1차 패스: 행 단위 검증 ──

    def validate_row(self, line_num: int, row: list[str], prev_type: str):
        """단일 행 검증. prev_type은 직전 행의 Type (Choice 뒤 Option 체크용)"""
        self.stats["total"] += 1

        if is_blank_line(row):
            self.stats["blanks"] += 1
            return

        if is_comment_line(row):
            self.stats["comments"] += 1
            return

        if is_header_line(row):
            return

        self.stats["data"] += 1

        # 컬럼 수 체크
        col_count = len(row)
        line_id = row[0].strip() if col_count > 0 else ""
        type_str = row[1].strip() if col_count > 1 else ""
        speaker = row[2].strip() if col_count > 2 else ""
        value = row[3].strip() if col_count > 3 else ""
        next_val = row[4].strip() if col_count > 4 else ""

        # Type이 비어있으면서 Value도 비어있으면 스킵 (빈 데이터 행)
        if not type_str and not value:
            return

        # Type 없는데 Value만 있는 행 (Notes 전용 행 등) → 경고만
        if not type_str and value:
            self.warn(line_num, f"Type이 비어있지만 Value가 있음: '{value}'")
            return

        # LineID 중복 체크
        if line_id:
            if line_id in self.line_ids:
                self.warn(
                    line_num,
                    f"LineID '{line_id}' 중복 (첫 등장: line {self.line_ids[line_id]})",
                )
            else:
                self.line_ids[line_id] = line_num

        # Type 유효성
        type_lower = type_str.lower()
        if type_lower not in VALID_TYPES:
            self.error(line_num, f"유효하지 않은 Type: '{type_str}'")
            return

        # 컬럼 수 최소 요건
        min_cols = 4 if type_lower == "option" else 5
        if col_count < min_cols:
            self.error(
                line_num,
                f"컬럼 수 부족: {col_count}개 (최소 {min_cols}개 필요)",
            )
            return

        # Next 유효성
        if next_val and next_val not in VALID_NEXT:
            try:
                float(next_val)
            except ValueError:
                self.error(line_num, f"유효하지 않은 Next 값: '{next_val}'")

        # Type별 검증
        if type_lower == "text":
            self._validate_text(line_num, speaker, value)
        elif type_lower == "char":
            self._validate_char(line_num, value)
        elif type_lower == "bg":
            self._validate_bg(line_num, value)
        elif type_lower == "cg":
            self._validate_cg(line_num, value)
        elif type_lower == "sd":
            self._validate_sd(line_num, value)
        elif type_lower == "sound":
            self._validate_sound(line_num, value)
        elif type_lower == "fx":
            self._validate_fx(line_num, value)
        elif type_lower == "flow":
            self._validate_flow(line_num, value)
        elif type_lower == "choice":
            self.stats["choices"] += 1
        elif type_lower == "option":
            self.stats["options"] += 1
            self._validate_option(line_num, value, prev_type)
        elif type_lower == "place":
            self._validate_place(line_num, value)

    # ── Text 검증 ──

    def _validate_text(self, ln: int, speaker: str, value: str):
        if not value:
            self.warn(ln, "Text 행의 Value가 비어있음")
            return

        # 인라인 태그 검증
        inline_tags = re.findall(r"<(\w+)=([^/>]*)/>", value)
        for tag_name, tag_value in inline_tags:
            if tag_name == "emote":
                # emote=Name 또는 emote=Name/Overlay
                emote_name = tag_value.split("/")[0].strip()
                if emote_name.lower() not in self.emotes:
                    self.error(
                        ln,
                        f"Text 인라인 <emote={tag_value}/> — "
                        f"'{emote_name}'이 표정 테이블에 없음",
                    )
            elif tag_name == "sfx":
                pass  # SFX는 런타임 로드라 정적 검증 불가
            elif tag_name == "wait":
                try:
                    float(tag_value)
                except ValueError:
                    self.error(ln, f"<wait={tag_value}/> — 숫자가 아님")
            elif tag_name == "speed":
                pass
            else:
                self.warn(ln, f"알 수 없는 인라인 태그: <{tag_name}={tag_value}/>")

        # <emote=Name> 형태 (닫는 태그 없이 > 로 끝나는 형태) — overlay 포함
        emote_angle = re.findall(r"<emote=([^/>]+)>", value)
        for ev in emote_angle:
            emote_name = ev.split("/")[0].strip()
            if emote_name.lower() not in self.emotes:
                self.error(
                    ln,
                    f"Text 인라인 <emote={ev}> — "
                    f"'{emote_name}'이 표정 테이블에 없음",
                )

    # ── Char 검증 ──

    def _validate_char(self, ln: int, value: str):
        if not value:
            self.error(ln, "Char 행의 Value가 비어있음")
            return

        parts = value.split(":")
        if len(parts) < 2:
            self.error(ln, f"Char 명령어 형식 오류: '{value}' (최소 2파트 필요)")
            return

        slot = parts[0].strip().lower()
        action = parts[1].strip().lower()

        if slot not in VALID_SLOTS:
            self.error(ln, f"유효하지 않은 Slot: '{parts[0]}' (L/C/R)")

        if action not in VALID_CHAR_ACTIONS:
            self.error(ln, f"유효하지 않은 Action: '{parts[1]}'")
            return

        if action in ("enter", "enterup"):
            if len(parts) < 3:
                self.error(
                    ln,
                    f"Char {parts[1]}: CharacterID 누락 — '{value}'",
                )
                return

            char_id = parts[2].strip().lower()

            # CharacterID가 emote 이름인지 체크 (흔한 실수)
            if char_id in self.emotes and char_id not in VALID_CHARACTERS:
                self.error(
                    ln,
                    f"Char {parts[1]}: '{parts[2]}'은 표정 이름이지 캐릭터 ID가 아님. "
                    f"'{parts[1]}:캐릭터ID:{parts[2]}' 형식이어야 함",
                )
                return

            if char_id not in VALID_CHARACTERS:
                self.error(
                    ln,
                    f"유효하지 않은 CharacterID: '{parts[2]}' "
                    f"(유효: {', '.join(sorted(c.title() for c in VALID_CHARACTERS))})",
                )

            # Emote (4th part)
            if len(parts) >= 4:
                emote = parts[3].strip()
                if emote and emote.lower() not in self.emotes:
                    self.error(
                        ln, f"유효하지 않은 Emote: '{emote}' — 표정 테이블에 없음"
                    )

            # Overlay (5th part) — Roa 전용
            if len(parts) >= 5:
                overlay = parts[4].strip()
                if overlay:
                    if char_id != "roa":
                        self.warn(
                            ln,
                            f"오버레이 '{overlay}'가 Roa가 아닌 "
                            f"'{parts[2]}'에 사용됨 (Roa 전용 기능)",
                        )
                    elif overlay.lower() not in ROA_OVERLAYS:
                        self.warn(
                            ln,
                            f"알 수 없는 Roa 오버레이: '{overlay}' "
                            f"(알려진 값: {', '.join(sorted(ROA_OVERLAYS))})",
                        )

        elif action == "emote":
            if len(parts) < 3:
                self.error(ln, f"Char Emote: 표정 이름 누락 — '{value}'")
                return
            emote = parts[2].strip()
            if emote.lower() not in self.emotes:
                self.error(
                    ln, f"유효하지 않은 Emote: '{emote}' — 표정 테이블에 없음"
                )
            # 4th part = overlay (Roa 전용 info)
            if len(parts) >= 4:
                overlay = parts[3].strip()
                if overlay and overlay.lower() not in ROA_OVERLAYS:
                    self.warn(ln, f"Emote 오버레이: 알 수 없는 값 '{overlay}'")

        elif action in ("exit", "exitdown"):
            pass  # 추가 파라미터 없음

    # ── BG 검증 ──

    def _validate_bg(self, ln: int, value: str):
        if not value:
            self.error(ln, "BG 행의 Value가 비어있음")
            return

        parts = value.split(":")
        bg_name = parts[0].strip()

        if bg_name.lower() not in self.bgs:
            self.error(ln, f"배경 테이블에 없는 BG: '{bg_name}'")

        if len(parts) >= 2:
            transition = parts[1].strip().lower()
            if transition not in VALID_BG_TRANSITIONS:
                self.error(
                    ln,
                    f"유효하지 않은 BG Transition: '{parts[1]}' "
                    f"(Cut/Fade/Cross)",
                )

        if len(parts) >= 3:
            try:
                float(parts[2].strip())
            except ValueError:
                self.error(ln, f"BG duration이 숫자가 아님: '{parts[2]}'")

    # ── CG 검증 ──

    def _validate_cg(self, ln: int, value: str):
        if not value:
            self.error(ln, "CG 행의 Value가 비어있음")
            return
        parts = value.split(":")
        name = parts[0].strip().lower()
        if name in ("exit", "close"):
            return  # 닫기 명령
        # CG 이름 형식은 자유 (런타임 로드)

    # ── SD 검증 ──

    def _validate_sd(self, ln: int, value: str):
        if not value:
            self.error(ln, "SD 행의 Value가 비어있음")
            return
        parts = value.split(":")
        name = parts[0].strip().lower()
        if name in ("exit", "close"):
            return
        # SD transition 검증
        if len(parts) >= 2:
            transition = parts[1].strip().lower()
            if transition not in VALID_SD_TRANSITIONS and transition not in ("exit", "close"):
                try:
                    float(transition)  # duration일 수도 있음
                except ValueError:
                    self.warn(ln, f"SD Transition: '{parts[1]}' — FadeIn/Cross 권장")

    # ── Sound 검증 ──

    def _validate_sound(self, ln: int, value: str):
        if not value:
            self.error(ln, "Sound 행의 Value가 비어있음")
            return
        parts = value.split(":")
        if len(parts) < 2:
            self.error(ln, f"Sound 형식 오류: '{value}' (최소 Category:Name 필요)")
            return
        category = parts[0].strip().lower()
        if category not in VALID_SOUND_CATEGORIES:
            self.error(
                ln,
                f"유효하지 않은 Sound Category: '{parts[0]}' (BGM/SFX/Voice)",
            )

    # ── FX 검증 ──

    def _validate_fx(self, ln: int, value: str):
        if not value:
            self.error(ln, "FX 행의 Value가 비어있음")
            return
        parts = value.split(":")
        effect = parts[0].strip().lower()

        if effect not in FX_ALL:
            self.error(ln, f"알 수 없는 FX 명령: '{parts[0]}'")
            return

        if effect in FX_PLANNED:
            self.warn(ln, f"FX '{parts[0]}' — 미구현 명령어 (코드 구현 필요)")

        # SceneStart BG 검증
        if effect == "scenestart" and len(parts) >= 2:
            bg = parts[1].strip()
            if bg.lower() not in self.bgs and bg.lower() not in ("eyeclose",):
                self.error(
                    ln, f"SceneStart 배경 '{bg}'가 배경 테이블에 없음"
                )

        # Shake 2-part 프리셋 버그 경고
        if effect in ("camshake", "stageshake", "dialogueshake"):
            if len(parts) == 2:
                preset = parts[1].strip().lower()
                if preset in SHAKE_PRESETS and preset != "medium":
                    self.warn(
                        ln,
                        f"'{value}' — 코드 버그: 2파트 Shake는 항상 Medium으로 동작. "
                        f"'{parts[0]}:0.3:{parts[1]}' 형식을 사용하거나 코드 수정 필요",
                    )

    # ── Flow 검증 ──

    def _validate_flow(self, ln: int, value: str):
        if not value:
            self.error(ln, "Flow 행의 Value가 비어있음")
            return
        parts = value.split(":")
        command = parts[0].strip().lower()

        if command not in FLOW_ALL:
            self.error(ln, f"알 수 없는 Flow 명령: '{parts[0]}'")
            return

        if command in FLOW_PLANNED:
            self.warn(ln, f"Flow '{parts[0]}' — 미구현 명령어 (코드 구현 필요)")

        if command == "jump":
            if len(parts) < 2 or not parts[1].strip():
                self.error(ln, "Flow Jump: 대상 LineID 누락")
            else:
                target = parts[1].strip()
                self.jump_targets.append((ln, target))

    # ── Option 검증 ──

    def _validate_option(self, ln: int, value: str, prev_type: str):
        if prev_type.lower() not in ("choice", "option"):
            self.error(ln, "Option 행이 Choice 또는 다른 Option 뒤에 오지 않음")

        if not value:
            self.error(ln, "Option 행의 Value가 비어있음")
            return

        pipe_parts = value.split("|")
        if len(pipe_parts) < 2:
            self.warn(ln, f"Option에 JumpTarget이 없음: '{value}'")
            return

        # 마지막 콤마 제거 (스프레드시트 오류)
        target = pipe_parts[1].strip().rstrip(",")
        if target:
            self.option_targets.append((ln, target))

    # ── Place 검증 ──

    def _validate_place(self, ln: int, value: str):
        if not value:
            self.error(ln, "Place 행의 Value가 비어있음")
            return
        if value.strip().lower() == "hide":
            return

    # ── 2차 패스: 크로스 참조 ──

    def cross_reference(self):
        """Jump/Option 대상 LineID 존재 여부 검증"""
        all_targets = self.jump_targets + self.option_targets
        for ln, target in all_targets:
            if target not in self.line_ids:
                self.error(ln, f"Jump/Option 대상 LineID '{target}'가 존재하지 않음")

    def print_results(self):
        """결과 출력"""
        errors = [m for m in self.messages if m.severity == Severity.ERROR]
        warns = [m for m in self.messages if m.severity == Severity.WARN]

        # ERROR 먼저, 그다음 WARN (행 번호 순)
        for m in sorted(errors, key=lambda x: x.line):
            print(m)
        for m in sorted(warns, key=lambda x: x.line):
            print(m)

        print()
        print(f"── 검증 결과 ──")
        print(f"  총 행: {self.stats['total']} "
              f"(데이터 {self.stats['data']}, "
              f"주석 {self.stats['comments']}, "
              f"빈 행 {self.stats['blanks']})")
        print(f"  Choice: {self.stats['choices']}, Option: {self.stats['options']}")
        print(f"  LineID: {len(self.line_ids)}개 정의")
        print(f"  ERROR: {len(errors)}개, WARN: {len(warns)}개")
        if self.stats["simplify_count"] > 0:
            print(f"  단순화 적용: {self.stats['simplify_count']}건")


# ─────────────────────────── 메인 처리 ───────────────────────────

def process_csv(
    input_path: str,
    output_path: Optional[str],
    emote_table_path: Optional[str],
    bg_table_path: Optional[str],
    do_simplify: bool = True,
    do_clean: bool = True,
    validate_only: bool = False,
):
    """메인 처리 함수"""
    # 테이블 로드
    emotes = load_emote_table(emote_table_path)
    bgs = load_bg_table(bg_table_path)

    print(f"입력: {input_path}")
    print(f"표정 테이블: {len(emotes)}개 항목 로드")
    print(f"배경 테이블: {len(bgs)}개 항목 로드")
    print()

    # CSV 읽기
    text = read_raw_lines(input_path)
    records = parse_csv(text)

    validator = Validator(emotes, bgs)

    output_rows: list[tuple[int, str]] = []  # (원본행번호, 출력행_텍스트)
    prev_type = ""
    simplify_count = 0

    for line_num, row in records:
        # 검증
        validator.validate_row(line_num, row, prev_type)

        # 현재 행의 Type 기억
        if len(row) > 1 and row[1].strip():
            prev_type = row[1].strip()

        if validate_only:
            continue

        # ── 정리 ──
        if is_blank_line(row):
            output_rows.append((line_num, ""))
            continue

        if is_comment_line(row):
            # 주석 행: # 텍스트만 유지
            comment_text = row[0].strip()
            output_rows.append((line_num, comment_text))
            continue

        if is_header_line(row):
            output_rows.append((line_num, "LineID,Type,Speaker,Value,Next"))
            continue

        # 데이터 행
        cleaned = clean_row(row)

        # Notes 제거 후 실질적으로 빈 행이 됐는지 체크
        if is_blank_line(cleaned):
            output_rows.append((line_num, ""))
            continue

        # 5컬럼 확보
        while len(cleaned) < 5:
            cleaned.append("")

        line_id = cleaned[0].strip()
        type_str = cleaned[1].strip()
        speaker = cleaned[2].strip()
        value = cleaned[3]  # Value는 공백 유지 (대사 공백 보존)
        next_val = cleaned[4].strip()

        # ── Value 내 실제 개행 → \n 텍스트로 통합 ──
        value = flatten_newlines(value)

        # ── 단순화 ──
        if do_simplify and type_str:
            new_value = simplify_value(type_str, value)
            if new_value != value:
                simplify_count += 1
                value = new_value

            new_next = simplify_next(type_str, next_val)
            if new_next != next_val:
                simplify_count += 1
                next_val = new_next

        # CSV 행 구성 — 항상 5컬럼 유지
        out_cols = [line_id, type_str, speaker, value, next_val]

        # CSV 포맷
        formatted = _format_csv_row(out_cols)
        output_rows.append((line_num, formatted))

    # 크로스 참조 검증
    validator.cross_reference()
    validator.stats["simplify_count"] = simplify_count

    # 검증 결과 출력
    validator.print_results()

    # 파일 출력
    if not validate_only and output_path:
        with open(output_path, "w", encoding="utf-8", newline="\n") as f:
            for _, row_text in output_rows:
                f.write(row_text + "\n")
        print(f"\n출력: {output_path}")
        print(f"단순화 적용: {simplify_count}건")


def _format_csv_row(cols: list[str]) -> str:
    """CSV 행을 문자열로 포맷 (필요시 따옴표 감싸기).
    Value에 실제개행이 남아있으면 안 되므로 최종 안전장치 포함.
    """
    # 안전장치: 혹시 남은 실제개행 제거
    safe_cols = [c.replace("\n", "\\n").replace("\r", "") for c in cols]
    out = io.StringIO()
    writer = csv.writer(out, lineterminator="")
    writer.writerow(safe_cols)
    return out.getvalue()


# ─────────────────────────── 테이블 경로 자동 탐색 ───────────────────────────

def find_table(input_dir: str, candidates: list[str]) -> Optional[str]:
    """입력 파일 디렉토리 기준으로 테이블 파일 자동 탐색"""
    for candidate in candidates:
        # 같은 디렉토리
        p = os.path.join(input_dir, candidate)
        if os.path.isfile(p):
            return p
        # 상위 디렉토리
        p = os.path.join(os.path.dirname(input_dir), candidate)
        if os.path.isfile(p):
            return p
    return None


# ─────────────────────────── CLI ───────────────────────────

def main():
    parser = argparse.ArgumentParser(
        description="시나리오 CSV 정리 · 단순화 · 문법 검증",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
사용 예시:
  python clean_scenario_csv.py -i "시나리오 (업데이트).csv"
  python clean_scenario_csv.py -i "시나리오.csv" --validate-only
  python clean_scenario_csv.py -i "시나리오.csv" --no-simplify
        """,
    )
    parser.add_argument(
        "-i", "--input", required=True, help="입력 CSV 파일 경로"
    )
    parser.add_argument(
        "-o", "--output", default=None, help="출력 CSV 파일 경로 (기본: 입력파일명_clean.csv)"
    )
    parser.add_argument(
        "--validate-only",
        action="store_true",
        help="검증만 수행, 파일 출력 없음",
    )
    parser.add_argument(
        "--no-simplify",
        action="store_true",
        help="단순화 건너뛰기",
    )
    parser.add_argument(
        "--emote-table",
        default=None,
        help="표정테이블.csv 경로 (미지정 시 자동 탐색)",
    )
    parser.add_argument(
        "--bg-table",
        default=None,
        help="데모 배경 테이블.csv 경로 (미지정 시 자동 탐색)",
    )

    args = parser.parse_args()

    if not os.path.isfile(args.input):
        print(f"❌ 입력 파일을 찾을 수 없습니다: {args.input}", file=sys.stderr)
        sys.exit(1)

    input_dir = os.path.dirname(os.path.abspath(args.input))

    # 테이블 경로 자동 탐색
    emote_table = args.emote_table or find_table(
        input_dir, ["표정테이블.csv", "emote_table.csv"]
    )
    bg_table = args.bg_table or find_table(
        input_dir, ["데모 배경 테이블.csv", "bg_table.csv"]
    )

    if emote_table:
        print(f"표정 테이블 발견: {emote_table}")
    else:
        print("⚠ 표정 테이블 없음 → 기본값 사용")

    if bg_table:
        print(f"배경 테이블 발견: {bg_table}")
    else:
        print("⚠ 배경 테이블 없음 → 기본값 사용")

    # 출력 경로
    output_path = args.output
    if not output_path and not args.validate_only:
        base, ext = os.path.splitext(args.input)
        output_path = f"{base}_clean{ext}"

    process_csv(
        input_path=args.input,
        output_path=output_path,
        emote_table_path=emote_table,
        bg_table_path=bg_table,
        do_simplify=not args.no_simplify,
        validate_only=args.validate_only,
    )


if __name__ == "__main__":
    main()
