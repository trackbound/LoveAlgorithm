# PowerShell: validate-manifest.ps1
# Usage: pwsh ./tools/validate-manifest.ps1

$manifestPath = "Assets/Data/characters_emotes.json"
$schemaPath = "Assets/Data/characters_emotes.schema.json"

if (-not (Test-Path $manifestPath)) { Write-Host "Manifest not found: $manifestPath"; exit 1 }
if (-not (Test-Path $schemaPath)) { Write-Host "Schema not found: $schemaPath"; exit 1 }

$json = Get-Content $manifestPath -Raw | ConvertFrom-Json
$schemaJson = Get-Content $schemaPath -Raw | ConvertFrom-Json

# Basic checks (shape-based) - lightweight validator
$errors = @()
if (-not $json.version) { $errors += "Missing version" }
if (-not $json.generatedAt) { $errors += "Missing generatedAt" }
if (-not $json.characters) { $errors += "Missing characters" }
else {
    foreach ($c in $json.characters) {
        if (-not $c.id) { $errors += "Character missing id" }
        if (-not $c.emotes) { $errors += "Character $($c.id) missing emotes" }
        else {
            foreach ($e in $c.emotes) {
                if (-not $e.key) { $errors += "Emote missing key in $($c.id)" }
                if (-not $e.fileName) { $errors += "Emote missing fileName in $($c.id)" }
                if (-not $e.guid) { $errors += "Emote missing guid in $($c.id)" }
            }
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "Manifest validation FAILED:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host " - $_" }
    exit 2
}
else {
    Write-Host "Manifest validation PASSED" -ForegroundColor Green
    exit 0
}
