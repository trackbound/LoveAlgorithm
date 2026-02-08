# PowerShell: generate-manifest.ps1
# Generates Assets/Data/characters_emotes.json by scanning Assets/Resources/Characters
# Usage: pwsh ./tools/generate-manifest.ps1

$base = Join-Path (Get-Location) "Assets/Resources/Characters"
$out = Join-Path (Get-Location) "Assets/Data/characters_emotes.json"

if (-not (Test-Path $base)) { Write-Host "Characters folder not found: $base"; exit 1 }

$manifest = @{ version = 1; generatedAt = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'); characters = @() }

Get-ChildItem -Path $base -Directory | Sort-Object Name | ForEach-Object {
    $charId = $_.Name
    $charObj = @{ id = $charId; displayName = $charId; emotes = @() }
    Get-ChildItem -Path $_.FullName -Filter *.png -File | Sort-Object Name | ForEach-Object {
        $assetPath = (Join-Path "Assets/Resources/Characters/$charId" $_.Name) -replace '\\','/'
        # width/height via System.Drawing
        try {
            Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue
            $img = [System.Drawing.Image]::FromFile($_.FullName)
            $w = $img.Width; $h = $img.Height; $img.Dispose()
        } catch {
            $w = 0; $h = 0
        }

        # guid from .meta file
        $meta = $_.FullName + ".meta"
        $guid = ""
        if (Test-Path $meta) {
            $metaText = Get-Content $meta -Raw
            if ($metaText -match "guid:\s*([0-9a-fA-F]+)") { $guid = $matches[1] }
            if ($metaText -match "spritePixelsPerUnit:\s*([0-9]+(\.[0-9]+)?)") { $ppu = [double]$matches[1] } else { $ppu = 0 }
        } else { $ppu = 0 }

        $key = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
        $em = @{ key = $key; fileName = $_.Name; resourcePath = "Characters/$charId/$key"; guid = $guid; width = $w; height = $h; ppu = $ppu }
        $charObj.emotes += $em
    }

    $manifest.characters += $charObj
}

# Ensure output folder
$outDir = Split-Path $out -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$manifestJson = $manifest | ConvertTo-Json -Depth 6 -Compress
# Pretty print
$manifestJson = $manifest | ConvertTo-Json -Depth 6
Set-Content -Path $out -Value $manifestJson -Encoding UTF8
Write-Host "Manifest generated: $out"
Write-Host ""
Get-Content $out | Select-Object -First 40 | ForEach-Object { Write-Host $_ }
