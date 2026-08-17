# Sync shared TCG logic from Assets/TCG_Project (SSOT) to:
#   - TCG_Project/          (console mirror)
#   - Assets/Resources/GameData/ (Unity runtime JSON)
#
# Usage:
#   pwsh tools/sync-tcg-project.ps1
#   pwsh tools/sync-tcg-project.ps1 -Check

param(
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot "Assets\TCG_Project"
$mirrorRoot = Join-Path $repoRoot "TCG_Project"
$resourcesRoot = Join-Path $repoRoot "Assets\Resources\GameData"

$excludeRel = @(
    "Scripts\UI",
    "Scripts\Managers\BattleManager.cs",
    "Scripts\Managers\UnityResourceLoader.cs"
)

$sharedDirs = @(
    "Scripts\Abilities",
    "Scripts\Conditions",
    "Scripts\Core",
    "Scripts\Effects",
    "Scripts\Interfaces",
    "Scripts\Managers",
    "Scripts\Systems",
    "Scripts\Utils"
)

$sharedRootFiles = @(
    "ConsoleRunner.cs",
    "ConsoleFileLoader.cs",
    "Program.cs"
)

$jsonFiles = @(
    "Character.json",
    "CommonConfig.json",
    "ResourceCards.json",
    "RulebookCards.json"
)

$deleteFromMirror = @(
    "Scripts\Utils\DeckValidationResult.cs"
)

function Test-Excluded([string]$fullPath) {
    $rel = $fullPath.Substring($sourceRoot.Length).TrimStart("\", "/")
    foreach ($ex in $excludeRel) {
        if ($rel -eq $ex -or $rel.StartsWith($ex + "\")) { return $true }
    }
    return $false
}

function Collect-SourceFiles {
    $files = New-Object System.Collections.Generic.List[string]
    foreach ($dir in $sharedDirs) {
        $abs = Join-Path $sourceRoot $dir
        if (-not (Test-Path $abs)) { continue }
        Get-ChildItem $abs -Filter *.cs -File | ForEach-Object {
            if (-not (Test-Excluded $_.FullName)) { $files.Add($_.FullName) }
        }
    }
    foreach ($name in $sharedRootFiles) {
        $abs = Join-Path $sourceRoot $name
        if (Test-Path $abs) { $files.Add($abs) }
    }
    return $files
}

$mismatch = 0
$copied = 0

function Sync-File([string]$from, [string]$to) {
    $script:copied++
    if ($Check) {
        if (-not (Test-Path $to)) {
            Write-Host "MISSING  $to"
            $script:mismatch++
            return
        }
        $srcHash = (Get-FileHash $from -Algorithm SHA256).Hash
        $dstHash = (Get-FileHash $to -Algorithm SHA256).Hash
        if ($srcHash -ne $dstHash) {
            Write-Host "DIFF     $to"
            $script:mismatch++
        }
        return
    }

    $destDir = Split-Path $to -Parent
    if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir | Out-Null }
    Copy-Item -Path $from -Destination $to -Force
}

foreach ($src in (Collect-SourceFiles)) {
    $rel = $src.Substring($sourceRoot.Length).TrimStart("\", "/")
    Sync-File $src (Join-Path $mirrorRoot $rel)
}

foreach ($json in $jsonFiles) {
    $src = Join-Path $sourceRoot "Data\$json"
    if (-not (Test-Path $src)) { continue }
    Sync-File $src (Join-Path $mirrorRoot "Data\$json")
    Sync-File $src (Join-Path $resourcesRoot $json)
}

foreach ($rel in $deleteFromMirror) {
    $path = Join-Path $mirrorRoot $rel
    if (Test-Path $path) {
        if ($Check) {
            Write-Host "EXTRA    $path"
            $mismatch++
        }
        else {
            Remove-Item $path -Force
        }
    }
}

function Test-CardSprites {
    $jsons = @(
        (Join-Path $sourceRoot "Data\Character.json"),
        (Join-Path $sourceRoot "Data\ResourceCards.json"),
        (Join-Path $sourceRoot "Data\RulebookCards.json")
    )

    foreach ($json in $jsons) {
        if (-not (Test-Path $json)) { continue }
        $text = Get-Content $json -Raw -Encoding UTF8
        [regex]::Matches($text, '"imagePath"\s*:\s*"([^"]+)"') | ForEach-Object {
            $imagePath = $_.Groups[1].Value
            if ($imagePath -match 'GameDesign/') {
                Write-Host "SPRITE   legacy GameDesign path: $imagePath"
                $script:mismatch++
                return
            }
            if ($imagePath -match '/ELLIE/') {
                Write-Host "SPRITE   typo folder ELLIE (use ELLI): $imagePath"
                $script:mismatch++
                return
            }

            $rel = $imagePath -replace '/', '\'
            $full = Join-Path $repoRoot $rel
            if (-not (Test-Path -LiteralPath $full)) {
                Write-Host "SPRITE   missing $full"
                $script:mismatch++
            }
        }
    }
}

if ($Check) {
    Test-CardSprites
    if ($mismatch -gt 0) {
        Write-Host "sync-tcg-project --check failed: $mismatch mismatch(es)."
        exit 1
    }
    Write-Host "sync-tcg-project --check ok."
    exit 0
}

Write-Host "Synced $copied files from Assets/TCG_Project."
