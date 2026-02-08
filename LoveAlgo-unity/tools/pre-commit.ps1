# LoveAlgo Pre-commit Validation Script
# 커밋 전 스크립트 유효성 검사 실행

param(
    [switch]$Fix,      # 자동 수정 가능한 문제 수정
    [switch]$Verbose   # 상세 출력
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$scriptsPath = Join-Path $projectRoot "Assets\Resources\Story"
$csPath = Join-Path $projectRoot "Assets\Scripts"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  LoveAlgo Pre-commit Validation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$errors = @()
$warnings = @()

# 1. CSV 스크립트 기본 검증
Write-Host "[1/4] CSV 스크립트 검증..." -ForegroundColor Yellow

$csvFiles = Get-ChildItem -Path $scriptsPath -Filter "*.csv" -Recurse -ErrorAction SilentlyContinue
foreach ($csv in $csvFiles) {
    $content = Get-Content $csv.FullName -Raw -ErrorAction SilentlyContinue
    if ($content) {
        $lines = $content -split "`n"
        $lineNum = 0
        foreach ($line in $lines) {
            $lineNum++
            $trimmed = $line.Trim()
            
            # 빈 줄, 주석, 헤더 스킵
            if ([string]::IsNullOrEmpty($trimmed)) { continue }
            if ($trimmed.StartsWith("#")) { continue }
            if ($trimmed.StartsWith("LineID,")) { continue }
            
            # 컬럼 수 확인 (최소 5개)
            $columns = $trimmed -split ","
            if ($columns.Count -lt 5) {
                $errors += "$($csv.Name):$lineNum - 컬럼 부족 ($($columns.Count)/5)"
            }
        }
    }
}

if ($csvFiles.Count -gt 0) {
    Write-Host "  ✓ $($csvFiles.Count)개 CSV 파일 검사 완료" -ForegroundColor Green
}

# 2. C# 컴파일 오류 패턴 검사
Write-Host "[2/4] C# 코드 패턴 검사..." -ForegroundColor Yellow

$csFiles = Get-ChildItem -Path $csPath -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue
$coroutineCount = 0
$todoCount = 0

foreach ($cs in $csFiles) {
    $content = Get-Content $cs.FullName -Raw -ErrorAction SilentlyContinue
    $relativePath = $cs.FullName.Replace($projectRoot, "").TrimStart("\")
    
    # 코루틴 사용 경고 (에디터 제외)
    if ($content -match "StartCoroutine" -and $relativePath -notmatch "Editor") {
        $coroutineCount++
        if ($Verbose) {
            $warnings += "$relativePath - Coroutine 사용 발견 (UniTask 권장)"
        }
    }
    
    # TODO/FIXME 카운트
    $matches = [regex]::Matches($content, "(?i)(TODO|FIXME)")
    $todoCount += $matches.Count
}

Write-Host "  ✓ $($csFiles.Count)개 C# 파일 검사 완료" -ForegroundColor Green
if ($coroutineCount -gt 0) {
    $warnings += "코루틴 사용: $coroutineCount개 파일 (UniTask 마이그레이션 권장)"
}
if ($todoCount -gt 0) {
    Write-Host "  ℹ TODO/FIXME: $todoCount개 발견" -ForegroundColor Gray
}

# 3. 리소스 존재 확인
Write-Host "[3/4] 리소스 무결성 검사..." -ForegroundColor Yellow

$bgPath = Join-Path $projectRoot "Assets\Resources\Backgrounds"
$charPath = Join-Path $projectRoot "Assets\Resources\Characters"

$bgCount = (Get-ChildItem -Path $bgPath -Filter "*.png" -Recurse -ErrorAction SilentlyContinue).Count
$charCount = (Get-ChildItem -Path $charPath -Filter "*.png" -Recurse -ErrorAction SilentlyContinue).Count

Write-Host "  ✓ 배경: $bgCount개, 캐릭터: $charCount개" -ForegroundColor Green

# 4. 대용량 파일 확인
Write-Host "[4/4] 대용량 파일 확인..." -ForegroundColor Yellow

$largeFiles = Get-ChildItem -Path $projectRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Length -gt 50MB -and $_.FullName -notmatch "Library|Temp|Builds" } |
    Select-Object -First 5

if ($largeFiles.Count -gt 0) {
    foreach ($file in $largeFiles) {
        $sizeMB = [math]::Round($file.Length / 1MB, 1)
        $warnings += "대용량 파일: $($file.Name) ($sizeMB MB)"
    }
}
else {
    Write-Host "  ✓ 대용량 파일 없음" -ForegroundColor Green
}

# 결과 출력
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  검증 결과" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "❌ 오류 ($($errors.Count)개):" -ForegroundColor Red
    foreach ($err in $errors) {
        Write-Host "   - $err" -ForegroundColor Red
    }
}

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "⚠️  경고 ($($warnings.Count)개):" -ForegroundColor Yellow
    foreach ($warn in $warnings) {
        Write-Host "   - $warn" -ForegroundColor Yellow
    }
}

if ($errors.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host ""
    Write-Host "✅ 모든 검증 통과!" -ForegroundColor Green
}

Write-Host ""

# 오류가 있으면 exit code 1 반환 (Git hook에서 커밋 차단용)
if ($errors.Count -gt 0) {
    exit 1
}

exit 0
