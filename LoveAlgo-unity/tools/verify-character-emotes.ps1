# PowerShell script: verify-character-emotes.ps1
# Scans Assets/Resources/Characters/* for PNG files and reports non-canonical names
# Usage: pwsh ./tools/verify-character-emotes.ps1

$repoRoot = Resolve-Path ".." | Select-Object -ExpandProperty Path | Split-Path -Parent
# Ensure running from repo root context; adjust if necessary
$base = Join-Path (Get-Location) "Assets/Resources/Characters"
$characters = @('Roa','Yeun','Daeun','Bom','Heewon')
$canonical = @('Default','EyeSmile','Bright','Happy','Glare','Tearful','Surprise','Happy_Alt')
$variants = @{ 'Smile'='EyeSmile'; 'Laugh'='Happy'; 'Crying'='Tearful'; 'Tear'='Tearful'; 'BigSmile'='Happy' }

$report = @()
$report += "Character Naming Verification Report"
$report += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$report += ""

$total = 0
$nonCanonical = 0

foreach ($c in $characters) {
    $folder = Join-Path $base $c
    $report += "-- $c --"
    if (-not (Test-Path $folder)) { $report += "  (folder missing)"; $report += ""; continue }
    $files = Get-ChildItem $folder -Filter *.png -File -ErrorAction SilentlyContinue
    if ($files.Count -eq 0) { $report += "  (no png files)"; $report += ""; continue }
    foreach ($f in $files) {
        $total++
        $name = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
        if ($canonical -contains $name) { $report += "  OK: $($f.Name)" }
        elseif ($variants.ContainsKey($name)) { $nonCanonical++; $report += "  Variant: $($f.Name)  → Suggest: $($variants[$name]).png" }
        else { $nonCanonical++; $report += "  Unknown: $($f.Name)  → No suggestion available" }
    }
    $report += ""
}
$report += "Total files scanned: $total"
$report += "Non-canonical files: $nonCanonical"

$reportPath = Join-Path (Get-Location) 'character_naming_report.txt'
$report | Out-File -FilePath $reportPath -Encoding utf8
Write-Host "Report written: $reportPath"
Write-Host ""
$report | Select-Object -First 40 | ForEach-Object { Write-Host $_ }
