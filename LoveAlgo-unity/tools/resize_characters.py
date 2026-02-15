#!/usr/bin/env python3
"""
LoveAlgo 캐릭터 이미지 리사이즈 툴

사용법:
    python resize_characters.py                    # 기본 실행 (높이 2000px)
    python resize_characters.py --height 1500      # 높이 1500px로 리사이즈
    python resize_characters.py --dry-run          # 실제 변환 없이 미리보기
    python resize_characters.py --backup           # 원본 백업 후 변환
    python resize_characters.py --character Roa    # 특정 캐릭터만 처리

요구사항:
    pip install Pillow
"""

import argparse
import shutil
from pathlib import Path
from datetime import datetime

try:
    from PIL import Image
    # 매우 큰 이미지 처리를 위해 decompression bomb 제한 해제
    Image.MAX_IMAGE_PIXELS = None
except ImportError:
    print("Pillow가 설치되어 있지 않습니다.")
    print("설치: pip install Pillow")
    exit(1)


# 기본 설정
DEFAULT_TARGET_HEIGHT = 2000
SUPPORTED_EXTENSIONS = {'.png', '.jpg', '.jpeg'}
RESAMPLE_METHOD = Image.LANCZOS  # 고품질 리샘플링


def get_project_root() -> Path:
    """프로젝트 루트 경로 반환"""
    script_dir = Path(__file__).parent
    return script_dir.parent


def get_characters_path() -> Path:
    """캐릭터 이미지 폴더 경로"""
    return get_project_root() / "Assets" / "Resources" / "Characters"


def get_backup_path() -> Path:
    """백업 폴더 경로"""
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    return get_project_root() / "Backups" / f"Characters_{timestamp}"


def get_image_info(image_path: Path) -> dict:
    """이미지 정보 반환"""
    with Image.open(image_path) as img:
        return {
            "path": image_path,
            "width": img.width,
            "height": img.height,
            "format": img.format,
            "mode": img.mode,
            "size_mb": image_path.stat().st_size / (1024 * 1024)
        }


def progressive_resize(img: Image.Image, target_height: int) -> tuple[Image.Image, int]:
    """단계적 축소: 50%씩 줄여가며 최종 크기에 도달 (화질 보존)
    
    원본이 매우 큰 경우(~20000px) 한 번에 축소하면 디테일이 손실됨.
    50%씩 점진적으로 줄이면 각 단계에서 LANCZOS 필터가 더 정밀하게 작동.
    """
    w, h = img.size
    steps = 0

    # 50%씩 줄이다가 target의 2배 이하가 되면 마지막 한 번에 target으로
    while h > target_height * 2:
        w = w // 2
        h = h // 2
        img = img.resize((w, h), RESAMPLE_METHOD)
        steps += 1

    # 최종 리사이즈
    ratio = target_height / h
    final_w = round(w * ratio)
    img = img.resize((final_w, target_height), RESAMPLE_METHOD)
    steps += 1

    return img, steps


def resize_image(image_path: Path, target_height: int, dry_run: bool = False) -> dict:
    """이미지 리사이즈 (점진적 축소 알고리즘 사용)"""
    info = get_image_info(image_path)
    
    # 이미 목표 크기 이하면 스킵
    if info["height"] <= target_height:
        return {
            "status": "skipped",
            "reason": f"이미 {info['height']}px (목표: {target_height}px)",
            **info
        }
    
    result = {
        "status": "success" if not dry_run else "dry_run",
        "original_size": (info["width"], info["height"]),
        "original_mb": info["size_mb"],
        **info
    }
    
    if not dry_run:
        with Image.open(image_path) as img:
            # RGBA 모드 유지 (투명도 보존)
            if img.mode in ('RGBA', 'LA') or (img.mode == 'P' and 'transparency' in img.info):
                img = img.convert('RGBA')
            
            # 점진적 축소 (50%씩 줄여가며 화질 보존)
            resized, steps = progressive_resize(img.copy(), target_height)
            result["steps"] = steps
            result["new_size"] = (resized.width, resized.height)
            
            # 저장 (PNG는 무손실)
            if image_path.suffix.lower() == '.png':
                resized.save(image_path, 'PNG', optimize=True)
            else:
                resized.save(image_path, 'JPEG', quality=95)
        
        # 새 파일 크기
        result["new_mb"] = image_path.stat().st_size / (1024 * 1024)
        result["saved_mb"] = result["original_mb"] - result["new_mb"]
    else:
        # dry_run일 때도 예상 크기 계산
        ratio = target_height / info["height"]
        new_width = round(info["width"] * ratio)
        result["new_size"] = (new_width, target_height)
    
    return result


def backup_characters(characters_path: Path, backup_path: Path):
    """캐릭터 폴더 백업"""
    print(f"\n📦 백업 중: {backup_path}")
    shutil.copytree(characters_path, backup_path)
    print(f"   완료!")


def process_characters(
    target_height: int = DEFAULT_TARGET_HEIGHT,
    dry_run: bool = False,
    backup: bool = False,
    character_filter: str = None
):
    """메인 처리 함수"""
    characters_path = get_characters_path()
    
    if not characters_path.exists():
        print(f"❌ 캐릭터 폴더를 찾을 수 없습니다: {characters_path}")
        return
    
    print(f"\n{'='*60}")
    print(f"  LoveAlgo 캐릭터 이미지 리사이즈 툴")
    print(f"{'='*60}")
    print(f"  📁 경로: {characters_path}")
    print(f"  📐 목표 높이: {target_height}px")
    print(f"  🔍 모드: {'미리보기 (dry-run)' if dry_run else '실제 변환'}")
    if character_filter:
        print(f"  🎯 필터: {character_filter}")
    print(f"{'='*60}\n")
    
    # 백업
    if backup and not dry_run:
        backup_characters(characters_path, get_backup_path())
    
    # 캐릭터 폴더 순회
    results = {
        "processed": [],
        "skipped": [],
        "errors": []
    }
    
    total_saved_mb = 0
    
    for char_folder in sorted(characters_path.iterdir()):
        if not char_folder.is_dir():
            continue
        
        char_name = char_folder.name
        
        # 필터 적용
        if character_filter and char_name.lower() != character_filter.lower():
            continue
        
        print(f"\n📂 {char_name}")
        print(f"   {'─'*40}")
        
        for image_file in sorted(char_folder.iterdir()):
            if image_file.suffix.lower() not in SUPPORTED_EXTENSIONS:
                continue
            
            try:
                result = resize_image(image_file, target_height, dry_run)
                
                if result["status"] == "skipped":
                    results["skipped"].append(result)
                    print(f"   ⏭️  {image_file.name}: {result['reason']}")
                else:
                    results["processed"].append(result)
                    orig = result["original_size"]
                    new = result["new_size"]
                    
                    if dry_run:
                        print(f"   📋 {image_file.name}: {orig[0]}x{orig[1]} → {new[0]}x{new[1]}")
                    else:
                        saved = result.get("saved_mb", 0)
                        steps = result.get("steps", 1)
                        total_saved_mb += saved
                        print(f"   ✅ {image_file.name}: {orig[0]}x{orig[1]} → {new[0]}x{new[1]} ({steps}단계, {saved:.1f}MB 절약)")
                        
            except Exception as e:
                results["errors"].append({"path": image_file, "error": str(e)})
                print(f"   ❌ {image_file.name}: {e}")
    
    # 요약
    print(f"\n{'='*60}")
    print(f"  📊 결과 요약")
    print(f"{'='*60}")
    print(f"  ✅ 처리됨: {len(results['processed'])}개")
    print(f"  ⏭️  스킵됨: {len(results['skipped'])}개")
    print(f"  ❌ 오류: {len(results['errors'])}개")
    
    if not dry_run and total_saved_mb > 0:
        print(f"  💾 총 절약: {total_saved_mb:.1f}MB")
    
    print(f"{'='*60}\n")
    
    return results


def main():
    parser = argparse.ArgumentParser(
        description="LoveAlgo 캐릭터 이미지 리사이즈 툴",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
예시:
  python resize_characters.py                    # 기본 실행
  python resize_characters.py --height 1500      # 높이 1500px
  python resize_characters.py --dry-run          # 미리보기
  python resize_characters.py --backup           # 백업 후 변환
  python resize_characters.py --character Roa    # 로아만 처리
        """
    )
    
    parser.add_argument(
        "--height", "-H",
        type=int,
        default=DEFAULT_TARGET_HEIGHT,
        help=f"목표 높이 (기본값: {DEFAULT_TARGET_HEIGHT}px)"
    )
    
    parser.add_argument(
        "--dry-run", "-n",
        action="store_true",
        help="실제 변환 없이 미리보기만"
    )
    
    parser.add_argument(
        "--backup", "-b",
        action="store_true",
        help="변환 전 원본 백업"
    )
    
    parser.add_argument(
        "--character", "-c",
        type=str,
        default=None,
        help="특정 캐릭터만 처리 (예: Roa, Daeun)"
    )
    
    args = parser.parse_args()
    
    process_characters(
        target_height=args.height,
        dry_run=args.dry_run,
        backup=args.backup,
        character_filter=args.character
    )


if __name__ == "__main__":
    main()
