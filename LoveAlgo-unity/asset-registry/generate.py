#!/usr/bin/env python3
"""
asset-registry 자동 생성 스크립트.
Assets/ 폴더를 스캔하여 카테고리별 JSON 레지스트리를 생성한다.

출력 파일:
    asset-registry.json  — 전체 요약 인덱스 (에이전트 첫 진입점)
    characters.json      — 캐릭터 상세
    audio.json           — BGM / SFX 상세
    stage.json           — 배경, CG, SD
    story.json           — 스토리 CSV
    ui.json              — UI 아트 / 프리팹
    data.json            — ScriptableObject / 설정 / 아이템
    scripts.json         — 네임스페이스별 스크립트 클래스 목록

사용법:
    python asset-registry/generate.py
    (LoveAlgo-unity/ 디렉토리에서 실행)
"""

import json
from datetime import date
from pathlib import Path
from collections import defaultdict

ROOT = Path(__file__).resolve().parent.parent  # LoveAlgo-unity/
ASSETS = ROOT / "Assets"
OUT_DIR = ROOT / "asset-registry"

# ── 유틸 ──

def files_in(folder: Path, *exts: str) -> list[str]:
    """폴더 내 파일명 목록 (확장자 필터, 재귀 아님, .meta 제외)."""
    if not folder.is_dir():
        return []
    result = []
    for f in sorted(folder.iterdir()):
        if f.is_file() and f.suffix != ".meta":
            if not exts or f.suffix.lower() in exts:
                result.append(f.name)
    return result


def subdirs(folder: Path) -> list[str]:
    if not folder.is_dir():
        return []
    return sorted(d.name for d in folder.iterdir() if d.is_dir() and d.name != ".meta")


def relative(path: Path) -> str:
    return str(path.relative_to(ROOT)).replace("\\", "/")


def write_json(filename: str, data: dict) -> Path:
    """JSON 파일 쓰기 (None 값 자동 제거)."""
    def clean(obj):
        if isinstance(obj, dict):
            return {k: clean(v) for k, v in obj.items() if v is not None}
        if isinstance(obj, list):
            return [clean(v) for v in obj]
        return obj

    path = OUT_DIR / filename
    with open(path, "w", encoding="utf-8") as f:
        json.dump(clean(data), f, indent=2, ensure_ascii=False)
    return path


# ── 캐릭터 ──

def scan_characters() -> dict:
    base = ASSETS / "Resources" / "Characters"
    chars = []
    for d in subdirs(base):
        sprites = files_in(base / d, ".png")
        expressions = [s.removesuffix(".png") for s in sprites]
        chars.append({
            "id": d.lower(),
            "name": d,
            "folder": relative(base / d),
            "expressions": expressions,
            "expression_count": len(expressions),
            "data_so": f"Assets/Data/Characters/Character_{d}.asset",
            "bgm": f"Assets/Resources/Audio/BGM/{d}.mp3",
        })
    return {
        "folder": relative(base),
        "count": len(chars),
        "characters": chars,
    }


# ── 오디오 ──

def scan_audio() -> dict:
    bgm_dir = ASSETS / "Resources" / "Audio" / "BGM"
    sfx_dir = ASSETS / "Resources" / "Audio" / "SFX"
    bgm_files = files_in(bgm_dir, ".mp3", ".wav", ".ogg")
    sfx_files = files_in(sfx_dir, ".mp3", ".wav", ".ogg")

    sfx_exts = defaultdict(int)
    for f in sfx_files:
        sfx_exts[Path(f).suffix.lower()] += 1

    return {
        "bgm": {
            "folder": relative(bgm_dir),
            "count": len(bgm_files),
            "files": bgm_files,
        },
        "sfx": {
            "folder": relative(sfx_dir),
            "count": len(sfx_files),
            "pattern": "{NNN}_{Name}.{wav|mp3|ogg}",
            "format_counts": dict(sfx_exts),
            "examples": sfx_files[:3] + sfx_files[-2:] if len(sfx_files) > 5 else sfx_files,
        },
    }


# ── 스테이지 (BG, CG, SD) ──

def scan_stage() -> dict:
    bg_dir = ASSETS / "Resources" / "Backgrounds"
    cg_dir = ASSETS / "Resources" / "CG"
    sd_dir = ASSETS / "Resources" / "SD"

    bg_files = files_in(bg_dir, ".png", ".jpg")
    cg_files = files_in(cg_dir, ".png", ".jpg")
    sd_files = files_in(sd_dir, ".png", ".jpg")

    bg_locations: dict[str, int] = defaultdict(int)
    for f in bg_files:
        parts = f.removesuffix(".png").split("_")
        loc = parts[1] if len(parts) > 1 else "misc"
        bg_locations[loc] += 1

    return {
        "backgrounds": {
            "folder": relative(bg_dir),
            "count": len(bg_files),
            "pattern": "BG_{Location}_{Detail}_{Time}.png",
            "by_location": dict(bg_locations),
        },
        "cg": {
            "folder": relative(cg_dir),
            "count": len(cg_files),
            "pattern": "CG_{Character}_{NN}.png",
            "files": cg_files,
        },
        "sd": {
            "folder": relative(sd_dir),
            "count": len(sd_files),
            "pattern": "SD_{Character}_{NN}.png",
            "files": sd_files,
        },
    }


# ── 스토리 ──

def scan_story() -> dict:
    story_dir = ASSETS / "Resources" / "Story"
    csv_files = files_in(story_dir, ".csv")
    return {
        "folder": relative(story_dir),
        "count": len(csv_files),
        "files": csv_files,
        "pattern": "Day{N}_{Session}.csv, Event{N}.csv, Ending_{Character}.csv",
        "dsl_reference": "docs/reference/csv-script-commands.md",
    }


# ── 아이템 ──

def scan_items() -> dict:
    art_dir = ASSETS / "Art" / "Item"
    icon_dir = art_dir / "Icon"
    art_files = files_in(art_dir, ".png", ".jpg")
    icon_files = files_in(icon_dir, ".png", ".jpg")
    return {
        "art_folder": relative(art_dir),
        "icon_folder": relative(icon_dir),
        "art_count": len(art_files),
        "icon_count": len(icon_files),
        "data_so": "Assets/Resources/Data/ItemCatalog.asset",
    }


# ── UI ──

def scan_ui() -> dict:
    art_dir = ASSETS / "Art" / "UI"
    prefab_dir = ASSETS / "Prefabs"

    art_cats = {}
    for d in subdirs(art_dir):
        sub = subdirs(art_dir / d)
        count = len(files_in(art_dir / d, ".png", ".jpg"))
        for s in sub:
            count += len(files_in(art_dir / d / s, ".png", ".jpg"))
        art_cats[d] = {"file_count": count, "subfolders": sub if sub else None}

    prefab_cats = {}
    for d in subdirs(prefab_dir):
        prefabs = [f.removesuffix(".prefab") for f in files_in(prefab_dir / d, ".prefab")]
        for sd in subdirs(prefab_dir / d):
            sub_prefabs = [f"{sd}/{f.removesuffix('.prefab')}" for f in files_in(prefab_dir / d / sd, ".prefab")]
            prefabs.extend(sub_prefabs)
        if prefabs:
            prefab_cats[d] = prefabs
    root_prefabs = [f.removesuffix(".prefab") for f in files_in(prefab_dir, ".prefab")]
    if root_prefabs:
        prefab_cats["_root"] = root_prefabs

    return {
        "art": {
            "folder": relative(art_dir),
            "categories": art_cats,
        },
        "prefabs": {
            "folder": relative(prefab_dir),
            "categories": prefab_cats,
        },
        "runtime_ui": {
            "loading_folder": "Assets/Resources/UI/Loading",
            "loading_count": len(files_in(ASSETS / "Resources" / "UI" / "Loading", ".png")),
        },
        "code_folder": "Assets/Scripts/UI/",
    }


# ── 데이터 ──

def scan_data() -> dict:
    data_dir = ASSETS / "Data"
    res_data = ASSETS / "Resources" / "Data"

    char_data = files_in(data_dir / "Characters", ".asset")
    root_assets = files_in(data_dir, ".asset")
    root_json = files_in(data_dir, ".json")
    runtime = files_in(res_data, ".asset", ".txt")

    return {
        "character_data": {
            "folder": relative(data_dir / "Characters"),
            "count": len(char_data),
            "files": char_data,
        },
        "settings": {
            "folder": relative(data_dir),
            "files": root_assets,
        },
        "json_files": {
            "folder": relative(data_dir),
            "files": root_json,
        },
        "runtime_data": {
            "folder": relative(res_data),
            "files": runtime,
        },
        "reference_docs": {
            "game_data": "docs/reference/game-data.md",
            "csv_commands": "docs/reference/csv-script-commands.md",
        },
    }


# ── 스크립트 구조 ──

def scan_scripts() -> dict:
    scripts_dir = ASSETS / "Scripts"
    namespaces = {}
    for d in subdirs(scripts_dir):
        cs_files = sorted(f.stem for f in (scripts_dir / d).rglob("*.cs"))
        namespaces[d] = {
            "count": len(cs_files),
            "files": cs_files,
        }
    return {
        "folder": relative(scripts_dir),
        "namespaces": namespaces,
    }


# ── 메인 ──

def main():
    today = str(date.today())
    meta = {"_generated": today, "_generator": "asset-registry/generate.py"}

    # 스캔
    characters = scan_characters()
    audio = scan_audio()
    stage = scan_stage()
    story = scan_story()
    items = scan_items()
    ui = scan_ui()
    data = scan_data()
    scripts = scan_scripts()

    # 카테고리별 파일 출력
    write_json("characters.json", {**meta, **characters})
    write_json("audio.json", {**meta, **audio})
    write_json("stage.json", {**meta, **stage})
    write_json("story.json", {**meta, **story})
    write_json("ui.json", {**meta, **ui})
    write_json("data.json", {**meta, "items": items, **data})
    write_json("scripts.json", {**meta, **scripts})

    # 인덱스 — 에이전트가 처음 읽는 요약 파일
    index = {
        **meta,
        "_description": "AI 에이전트용 에셋 요약 인덱스. 상세는 각 카테고리 JSON 참조. 수동 편집 금지.",
        "naming_conventions": {
            "characters": "{Name}/{Expression}.png",
            "backgrounds": "BG_{Location}_{Detail}_{Time}.png",
            "cg": "CG_{Character}_{NN}.png",
            "sd": "SD_{Character}_{NN}.png",
            "bgm": "{Character|Ambient}.mp3",
            "sfx": "{NNN}_{Name}.{wav|mp3|ogg}",
            "items": "{english_lowercase}.png",
            "csv_story": "Day{N}_{Session}.csv, Event{N}.csv, Ending_{Character}.csv",
        },
        "summary": {
            "characters": {
                "count": characters["count"],
                "names": [c["name"] for c in characters["characters"]],
                "details": "characters.json",
            },
            "audio": {
                "bgm_count": audio["bgm"]["count"],
                "sfx_count": audio["sfx"]["count"],
                "details": "audio.json",
            },
            "stage": {
                "bg_count": stage["backgrounds"]["count"],
                "cg_count": stage["cg"]["count"],
                "sd_count": stage["sd"]["count"],
                "details": "stage.json",
            },
            "story": {
                "csv_count": story["count"],
                "dsl_reference": "docs/reference/csv-script-commands.md",
                "details": "story.json",
            },
            "items": {
                "count": items["art_count"],
                "details": "data.json",
            },
            "ui": {
                "art_categories": list(ui["art"]["categories"].keys()),
                "prefab_categories": list(ui["prefabs"]["categories"].keys()),
                "details": "ui.json",
            },
            "data": {
                "character_so_count": data["character_data"]["count"],
                "details": "data.json",
            },
            "scripts": {
                "namespaces": {k: v["count"] for k, v in scripts["namespaces"].items()},
                "total": sum(v["count"] for v in scripts["namespaces"].values()),
                "details": "scripts.json",
            },
        },
    }
    write_json("asset-registry.json", index)

    # 출력 요약
    print(f"Generated 8 files in {OUT_DIR.relative_to(ROOT)}/")
    print(f"  Characters: {characters['count']}")
    print(f"  BGM: {audio['bgm']['count']}, SFX: {audio['sfx']['count']}")
    print(f"  BG: {stage['backgrounds']['count']}, CG: {stage['cg']['count']}, SD: {stage['sd']['count']}")
    print(f"  Story CSV: {story['count']}")
    print(f"  Items: {items['art_count']}")
    total_scripts = sum(v["count"] for v in scripts["namespaces"].values())
    print(f"  Scripts: {total_scripts} files across {len(scripts['namespaces'])} namespaces")


if __name__ == "__main__":
    main()
