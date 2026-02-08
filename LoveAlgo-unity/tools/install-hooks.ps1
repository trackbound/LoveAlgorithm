# Git Hooks 설치 스크립트
# 사용법: .\tools\install-hooks.ps1

$projectRoot = Split-Path -Parent $PSScriptRoot
$hooksSource = Join-Path $PSScriptRoot "git-hooks"
$hooksTarget = Join-Path $projectRoot ".git\hooks"

if (-not (Test-Path $hooksTarget)) {
    Write-Host "❌ .git/hooks 폴더를 찾을 수 없습니다." -ForegroundColor Red
    Write-Host "   이 프로젝트가 Git 저장소인지 확인하세요." -ForegroundColor Gray
    exit 1
}

# pre-commit hook 복사
$source = Join-Path $hooksSource "pre-commit"
$target = Join-Path $hooksTarget "pre-commit"

if (Test-Path $source) {
    Copy-Item $source $target -Force
    Write-Host "✅ pre-commit hook 설치 완료" -ForegroundColor Green
    Write-Host "   위치: $target" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Git hooks가 설치되었습니다." -ForegroundColor Cyan
Write-Host "이제 커밋 시 자동으로 검증이 실행됩니다." -ForegroundColor Cyan
