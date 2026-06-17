#requires -Version 5.1
<#
  LoveAlgorithm auto-check loop runner (read-only, report-only; respects CLAUDE.md gates).

  Unattended schedule entry point. Heavy deterministic work (headless EditMode tests +
  static greps + git) runs here for free and produces raw-findings.md; only the
  interpretation / summary / next-actions are delegated to claude -p (read-only).
  The report file is written by THIS script -> claude has no filesystem/commit access.
  No self-commit, no game-code edits. To disable: create a DISABLED file in this folder.

  NOTE: keep this file ASCII-only. Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI,
  which corrupts non-ASCII source and breaks parsing. Korean lives in the .md files.
#>
[CmdletBinding()]
param(
  [switch]$SkipTests,           # skip headless tests, static checks only
  [switch]$NoClaude,            # skip claude summary (deterministic report only)
  [string]$Model = 'haiku',     # claude model for the summary (cheap default; override: sonnet/opus)
  [int]$TestTimeoutSec = 1200,  # headless test timeout (seconds)
  [double]$MaxBudgetUsd = 0.50  # claude spend ceiling (USD)
)

$ErrorActionPreference = 'Stop'
# Force UTF-8 for native-command I/O (claude prompt in / report out) on Windows PowerShell 5.1.
$OutputEncoding = [System.Text.Encoding]::UTF8
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$AutoDir      = $PSScriptRoot
$UnityProject = Split-Path (Split-Path $AutoDir -Parent) -Parent
$LastRun      = Join-Path $AutoDir 'last-run'
$ChecksDir    = Join-Path $UnityProject 'docs\checks'
$stamp        = Get-Date -Format 'yyyy-MM-dd_HHmm'
$today        = Get-Date -Format 'yyyy-MM-dd'

function Rel([string]$p){ if ($p.StartsWith($UnityProject)) { $p.Substring($UnityProject.Length+1) } else { $p } }

if (Test-Path (Join-Path $AutoDir 'DISABLED')) {
  Write-Host '[auto-check] DISABLED file present - exiting.'; exit 0
}

New-Item -ItemType Directory -Force -Path $LastRun, $ChecksDir | Out-Null
$rawPath = Join-Path $LastRun 'raw-findings.md'
$raw = [System.Collections.Generic.List[string]]::new()
function Add-Raw([string]$s){ $raw.Add($s) | Out-Null }

Add-Raw "# RAW check artifacts - $stamp"
Add-Raw ''

# --- 0. Environment ---
$editorRunning = [bool](Get-Process Unity -ErrorAction SilentlyContinue)
$verLine = Get-Content (Join-Path $UnityProject 'ProjectSettings\ProjectVersion.txt') |
           Select-String 'm_EditorVersion:' | Select-Object -First 1
$ver = if ($verLine) { $verLine.ToString().Split(':')[1].Trim() } else { '' }
$unityExe = "C:\Program Files\Unity\Hub\Editor\$ver\Editor\Unity.exe"

Add-Raw '## Environment'
Add-Raw "- Unity version: $ver"
Add-Raw "- Editor running: $editorRunning"
Add-Raw ''

# --- 1. Headless EditMode tests ---
Add-Raw '## Tests (EditMode)'
$testStatus = ''
if ($SkipTests) {
  $testStatus = 'skipped (-SkipTests)'
} elseif ($editorRunning) {
  $testStatus = 'skipped - editor open (headless batch not possible). Run via MCP run_tests at the desk.'
} elseif (-not (Test-Path $unityExe)) {
  $testStatus = "skipped - Unity exe not found: $unityExe"
} else {
  $resultsXml = Join-Path $LastRun 'editmode-results.xml'
  $unityLog   = Join-Path $LastRun 'unity-editmode.log'
  Remove-Item $resultsXml, $unityLog -ErrorAction SilentlyContinue
  $unityArgs = @('-batchmode','-projectPath',$UnityProject,'-runTests',
                 '-testPlatform','EditMode','-testResults',$resultsXml,'-logFile',$unityLog)
  $proc = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru
  if (-not $proc.WaitForExit($TestTimeoutSec * 1000)) {
    try { $proc.Kill() } catch {}
    $testStatus = "TIMEOUT ($TestTimeoutSec s) - killed"
  } elseif (Test-Path $resultsXml) {
    # Regex-extract the root <test-run> summary. Avoids strict [xml] parsing, which
    # chokes on NUnit's parameterized test names (quotes) and Korean payloads.
    try {
      $xmlText = Get-Content $resultsXml -Raw -Encoding UTF8
      $total  = if ($xmlText -match 'test-run[^>]*\btotal="(\d+)"')  { $Matches[1] } else { '?' }
      $passed = if ($xmlText -match 'test-run[^>]*\bpassed="(\d+)"') { $Matches[1] } else { '?' }
      $failed = if ($xmlText -match 'test-run[^>]*\bfailed="(\d+)"') { $Matches[1] } else { '?' }
      $result = if ($xmlText -match 'test-run[^>]*\bresult="(\w+)"')  { $Matches[1] } else { '?' }
      $testStatus = "EditMode passed $passed/$total (failed $failed, result $result, exit $($proc.ExitCode))"
      if ($failed -notin @('0','?')) {
        Add-Raw '**Failed tests:**'
        [regex]::Matches($xmlText, '<test-case\b[^>]*>') |
          Where-Object { $_.Value -match 'result="Failed"' } |
          ForEach-Object { if ($_.Value -match 'fullname="([^"]+)"') { Add-Raw ("- {0}" -f $Matches[1]) } }
      }
    } catch {
      $testStatus = "results XML parse error: $($_.Exception.Message)"
    }
  } else {
    $testStatus = "no results XML (exit $($proc.ExitCode)) - log: $(Rel $unityLog)"
  }
}
Add-Raw "- status: $testStatus"
Add-Raw ''

# --- 2. Static checks (grep) ---
$scriptsDir = Join-Path $UnityProject 'Assets\_Project\Scripts'
$cs = @(Get-ChildItem $scriptsDir -Recurse -Filter *.cs -ErrorAction SilentlyContinue)
Add-Raw "## Static checks (Scripts/*.cs: $($cs.Count))"

$obsolete = @($cs | Select-String -Pattern 'FindObjectOfType|FindObjectsOfType|enableWordWrapping')
Add-Raw "### Obsolete API ($($obsolete.Count))"
$obsolete | ForEach-Object { Add-Raw ("- {0}:{1}  ``{2}``" -f (Rel $_.Path), $_.LineNumber, $_.Line.Trim()) }
Add-Raw ''

$svc = @($cs | Select-String -Pattern 'Services\.(TryGet|Get|Register)\b')
Add-Raw "### Forbidden: Service Locator revival ($($svc.Count))"
$svc | ForEach-Object { Add-Raw ("- {0}:{1}  ``{2}``" -f (Rel $_.Path), $_.LineNumber, $_.Line.Trim()) }
Add-Raw ''

$managers = @($cs | Select-String -Pattern 'class\s+\w*Manager\b' |
             ForEach-Object { ($_.Line -replace '.*class\s+(\w+).*', '$1') } | Sort-Object -Unique)
Add-Raw "### Manager classes ($($managers.Count)) - skeleton is 4 (Game/Audio/Save/UI)"
$managers | ForEach-Object { Add-Raw "- $_" }
Add-Raw ''

# --- 3. Git drift ---
Push-Location $UnityProject
try {
  $branch    = (git rev-parse --abbrev-ref HEAD).Trim()
  $porcelain = @(git status --porcelain)
  $gitlog    = @(git log --oneline -8)
} finally { Pop-Location }
Add-Raw '## Git drift'
Add-Raw "- branch: $branch"
Add-Raw "- uncommitted changes: $($porcelain.Count)"
$porcelain | ForEach-Object { Add-Raw "  - $_" }
Add-Raw '- recent commits:'
$gitlog | ForEach-Object { Add-Raw "  - $_" }
Add-Raw ''

($raw -join "`r`n") | Set-Content -Path $rawPath -Encoding utf8
Write-Host "[auto-check] raw -> $(Rel $rawPath)"

# --- 4. claude summary (best-effort, read-only) ---
$reportBody = $null
# Resolve claude even when the scheduler's PATH is minimal (falls back to the known install dir).
$claudeCmd = (Get-Command claude -ErrorAction SilentlyContinue).Source
if (-not $claudeCmd) {
  $cand = Join-Path $env:USERPROFILE '.local\bin\claude.exe'
  if (Test-Path $cand) { $claudeCmd = $cand }
}
if (-not $NoClaude -and $claudeCmd) {
  $prompt = Get-Content (Join-Path $AutoDir 'check-prompt.md') -Raw -Encoding UTF8
  Push-Location $UnityProject
  try {
    $reportBody = $prompt | & $claudeCmd -p `
      --allowedTools Read Grep Glob 'Bash(git diff:*)' 'Bash(git log:*)' 'Bash(git status:*)' `
      --model $Model --max-budget-usd $MaxBudgetUsd | Out-String
  } catch {
    Write-Host "[auto-check] claude summary failed: $($_.Exception.Message)"
    $reportBody = $null
  } finally { Pop-Location }
}

# --- 5. Write report (script writes it, not claude) ---
if ([string]::IsNullOrWhiteSpace($reportBody)) {
  $reportBody = "> WARNING: claude summary not run/failed - deterministic raw below.`r`n`r`n" + ($raw -join "`r`n")
}
$header = "# Auto check - $today ($stamp)`r`n`r`n_generated by tools/auto-check/run-check.ps1 - read-only - uncommitted_`r`n`r`n---`r`n`r`n"
$full   = $header + $reportBody
$dated  = Join-Path $ChecksDir "$today-check.md"
$latest = Join-Path $ChecksDir '_latest.md'
$full | Set-Content -Path $dated  -Encoding utf8
$full | Set-Content -Path $latest -Encoding utf8
Write-Host "[auto-check] report -> $(Rel $dated)"
Write-Host "[auto-check] report -> $(Rel $latest)"
