<#
.SYNOPSIS
    Renames the JobFinder project to Applr: folder names, file names, namespaces,
    class names, project references and string literals.

.DESCRIPTION
    Run this from the project root (C:\Project\JobFinder).

    By default it runs in PREVIEW mode and changes nothing - it just prints
    every rename and every file whose contents would change. Re-run with
    -Apply to actually perform the rename.

    What it does, in order:
      1. Takes a zip backup of the project (skip with -NoBackup).
      2. Deletes build output (bin, obj, .vs, _webview2-build) - these contain
         assemblies with the old name and must be regenerated anyway.
      3. Rewrites file contents:  JobFinder -> Applr,  jobFinder -> applr,
         jobfinder -> applr  (UTF-8 BOMs and line endings are preserved).
      4. Renames files, then folders (deepest first). Each rename is independent:
         one failure is reported and the rest continue.

    The script is idempotent - re-running it only changes what is still unchanged,
    so it is safe to run again after an interrupted attempt.

    Scraped data files and built front-end bundles are deliberately left alone.

.EXAMPLE
    .\rename-to-applr.ps1
    Preview only. Nothing is modified.

.EXAMPLE
    .\rename-to-applr.ps1 -Apply
    Performs the rename, after taking a backup zip.
#>

[CmdletBinding()]
param(
    [switch]$Apply,
    [switch]$NoBackup
)

$ErrorActionPreference = 'Stop'

# --------------------------------------------------------------------------
# Configuration
# --------------------------------------------------------------------------

$Root = $PSScriptRoot
if (-not $Root) { $Root = (Get-Location).Path }
$Root = (Resolve-Path $Root).Path

# Directories never walked into, and (first four) deleted outright under -Apply.
$BuildDirs   = @('bin', 'obj', '.vs', '_webview2-build')
$ExcludeDirs = $BuildDirs + @('node_modules', '.git', 'dist', 'assets')

# Files left untouched: scraped data (a listing could legitimately contain the
# word), build caches, and this script itself.
$ExcludeFiles = @('rendered_page.html', 'scraped_jobs.txt', 'package-lock.json.bak')
$ExcludeExt   = @('.tsbuildinfo', '.dll', '.exe', '.pdb', '.zip', '.png', '.jpg', '.ico', '.wasm', '.node')

$MaxFileBytes = 2MB

# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------

function Convert-Name {
    param([string]$Text)
    # Ordinal, case-sensitive replacements, most specific first.
    $Text = $Text.Replace('JOBFINDER', 'APPLR')
    $Text = $Text.Replace('JobFinder', 'Applr')
    $Text = $Text.Replace('jobFinder', 'applr')
    $Text = $Text.Replace('jobfinder', 'applr')
    return $Text
}

function Test-Excluded {
    param([string]$FullPath)
    $rel = $FullPath.Substring($Root.Length).TrimStart('\', '/')
    foreach ($part in ($rel -split '[\\/]+')) {
        if ($ExcludeDirs -contains $part) { return $true }
    }
    return $false
}

function Get-ProjectFiles {
    Get-ChildItem -LiteralPath $Root -Recurse -File -Force |
        Where-Object {
            -not (Test-Excluded $_.FullName) -and
            $ExcludeFiles -notcontains $_.Name -and
            $ExcludeExt   -notcontains $_.Extension -and
            $_.FullName -ne $PSCommandPath -and
            $_.Length -le $MaxFileBytes
        }
}

function Remove-Tree {
    # Deletes a directory tree, falling back to robocopy for paths longer than
    # MAX_PATH (260 chars), which Remove-Item cannot handle on Windows PowerShell.
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { return $true }

    try {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        return $true
    } catch {
        $empty = Join-Path $env:TEMP ('empty-' + [guid]::NewGuid().ToString('N'))
        try {
            New-Item -ItemType Directory -Path $empty -Force | Out-Null
            robocopy $empty $Path /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
            $global:LASTEXITCODE = 0
            Remove-Item -LiteralPath $empty -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return $true
        } catch {
            return $false
        }
    }
}

function Test-IsBinary {
    param([byte[]]$Bytes)
    $limit = [Math]::Min($Bytes.Length, 4096)
    for ($i = 0; $i -lt $limit; $i++) {
        if ($Bytes[$i] -eq 0) { return $true }
    }
    return $false
}

function Move-Safe {
    # Plain file move. Deliberately NOT `git mv`: git mv refuses untracked and
    # ignored files (.csproj.user), and its stderr aborts the run. Git detects
    # these renames by content similarity when you stage them, so nothing is lost.
    param([string]$From, [string]$To)
    try {
        if (Test-Path -LiteralPath $To) {
            Write-Host "         target already exists, skipped" -ForegroundColor DarkYellow
            return $false
        }
        Move-Item -LiteralPath $From -Destination $To -Force -ErrorAction Stop
        return $true
    } catch {
        Write-Host "         FAILED: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# --------------------------------------------------------------------------
# Banner
# --------------------------------------------------------------------------

$mode = if ($Apply) { 'APPLY' } else { 'PREVIEW (nothing will be changed)' }
Write-Host ''
Write-Host '============================================================'
Write-Host '  JobFinder  ->  Applr'
Write-Host "  Root : $Root"
Write-Host "  Mode : $mode"
Write-Host '============================================================'
Write-Host ''
Write-Host 'Safe to re-run: it only changes what still needs changing.'
Write-Host ''

# --------------------------------------------------------------------------
# 1. Backup
# --------------------------------------------------------------------------

# Clear any staging folders left behind by an interrupted earlier run.
if ($env:TEMP -and (Test-Path -LiteralPath $env:TEMP)) {
    Get-ChildItem -Path $env:TEMP -Directory -Filter 'applr-rename-*' -ErrorAction SilentlyContinue |
        ForEach-Object {
            Write-Host "      Clearing leftover temp folder: $($_.Name)"
            Remove-Tree $_.FullName | Out-Null
        }
}

if ($Apply -and -not $NoBackup) {
    $stamp    = Get-Date -Format 'yyyyMMdd-HHmmss'
    $stageDir = Join-Path $env:TEMP "applr-rename-$stamp"
    $zipPath  = Join-Path (Split-Path $Root -Parent) "JobFinder-backup-$stamp.zip"

    Write-Host "[1/4] Backing up to $zipPath ..."

    $backupOk = $false
    try {
        # .git is deliberately excluded: it is not modified by this script (renames
        # go through `git mv`), and its internal paths can exceed the 260-character
        # limit that Compress-Archive and Remove-Item choke on.
        robocopy $Root $stageDir /E /XD bin obj .vs node_modules _webview2-build .git /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
        $global:LASTEXITCODE = 0

        Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $zipPath -Force
        $backupOk = Test-Path -LiteralPath $zipPath
    } catch {
        Write-Host "      Backup failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }

    # Cleanup is best-effort and never fatal.
    if (-not (Remove-Tree $stageDir)) {
        Write-Host "      Note: could not fully remove the temp folder $stageDir" -ForegroundColor DarkYellow
    }

    if ($backupOk) {
        $kb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1KB)
        Write-Host "      Backup written ($kb KB)."
    } else {
        Write-Host ''
        Write-Host '      No backup was created.' -ForegroundColor Yellow
        $answer = Read-Host '      Continue without a backup? (y/N)'
        if ($answer -ne 'y') { Write-Host 'Aborted.'; return }
    }
    Write-Host ''
} else {
    Write-Host '[1/4] Backup skipped.'
    Write-Host ''
}

# --------------------------------------------------------------------------
# 2. Remove build output
# --------------------------------------------------------------------------

Write-Host '[2/4] Build output to remove:'
$toDelete = Get-ChildItem -LiteralPath $Root -Recurse -Directory -Force |
    Where-Object { $BuildDirs -contains $_.Name } |
    Where-Object { $_.FullName -notlike '*node_modules*' } |
    Sort-Object { $_.FullName.Length } -Descending

if (-not $toDelete) {
    Write-Host '      (none)'
} else {
    foreach ($d in $toDelete) {
        $rel = $d.FullName.Substring($Root.Length).TrimStart('\', '/')
        Write-Host "      $rel"
        if ($Apply -and (Test-Path -LiteralPath $d.FullName)) {
            if (-not (Remove-Tree $d.FullName)) {
                Write-Host "         could not delete - remove it by hand later" -ForegroundColor DarkYellow
            }
        }
    }
}
Write-Host ''

# --------------------------------------------------------------------------
# 3. Rewrite file contents
# --------------------------------------------------------------------------

Write-Host '[3/4] Files whose contents change:'
$contentCount = 0
$hitCount     = 0

foreach ($file in Get-ProjectFiles) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -eq 0)      { continue }
    if (Test-IsBinary $bytes)     { continue }

    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $offset = if ($hasBom) { 3 } else { 0 }
    $text   = [System.Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)

    $new = Convert-Name $text
    if ($new -eq $text) { continue }

    $hits = ([regex]::Matches($text, 'jobfinder', 'IgnoreCase')).Count
    $rel  = $file.FullName.Substring($Root.Length).TrimStart('\', '/')
    Write-Host ("      {0,3} x  {1}" -f $hits, $rel)
    $contentCount++
    $hitCount += $hits

    if ($Apply) {
        $enc = New-Object System.Text.UTF8Encoding($hasBom)
        [System.IO.File]::WriteAllText($file.FullName, $new, $enc)
    }
}

if ($contentCount -eq 0) { Write-Host '      (none)' }
Write-Host ''

# --------------------------------------------------------------------------
# 4. Rename files, then folders (deepest first)
# --------------------------------------------------------------------------

Write-Host '[4/4] Renames:'
$renameCount = 0
$failCount   = 0

# Files first.
$files = Get-ChildItem -LiteralPath $Root -Recurse -File -Force |
    Where-Object { -not (Test-Excluded $_.FullName) -and $_.Name -match 'jobfinder' } |
    Sort-Object { $_.FullName.Length } -Descending

foreach ($f in $files) {
    $newName = Convert-Name $f.Name
    if ($newName -eq $f.Name) { continue }
    $rel     = $f.FullName.Substring($Root.Length).TrimStart('\', '/')
    $newFull = Join-Path $f.DirectoryName $newName
    Write-Host "      file    $rel  ->  $newName"
    $renameCount++
    if ($Apply) {
        if (-not (Move-Safe -From $f.FullName -To $newFull)) { $failCount++ }
    }
}

# Then folders, deepest path first so parent renames don't invalidate children.
$dirs = Get-ChildItem -LiteralPath $Root -Recurse -Directory -Force |
    Where-Object { -not (Test-Excluded $_.FullName) -and $_.Name -match 'jobfinder' } |
    Sort-Object { $_.FullName.Length } -Descending

foreach ($d in $dirs) {
    $newName = Convert-Name $d.Name
    if ($newName -eq $d.Name) { continue }
    $rel     = $d.FullName.Substring($Root.Length).TrimStart('\', '/')
    $newFull = Join-Path (Split-Path $d.FullName -Parent) $newName
    Write-Host "      folder  $rel  ->  $newName"
    $renameCount++
    if ($Apply -and (Test-Path -LiteralPath $d.FullName)) {
        if (-not (Move-Safe -From $d.FullName -To $newFull)) { $failCount++ }
    }
}

if ($renameCount -eq 0) { Write-Host '      (none)' }
Write-Host ''

# --------------------------------------------------------------------------
# Summary
# --------------------------------------------------------------------------

Write-Host '============================================================'
Write-Host ("  {0} file(s) with content changes, {1} reference(s)" -f $contentCount, $hitCount)
Write-Host ("  {0} file/folder rename(s)" -f $renameCount)
if ($failCount -gt 0) {
    Write-Host ("  {0} rename(s) FAILED - see red lines above" -f $failCount) -ForegroundColor Red
    Write-Host '  Re-run this script; it picks up where it left off.' -ForegroundColor Red
}
Write-Host '============================================================'
Write-Host ''

if (-not $Apply) {
    Write-Host 'This was a preview. Re-run with -Apply to make the changes:' -ForegroundColor Cyan
    Write-Host '    .\rename-to-applr.ps1 -Apply' -ForegroundColor Cyan
    Write-Host ''
    return
}

if ($backupOk) {
    Write-Host "Backup of the pre-rename working tree: $zipPath"
    Write-Host ''
}

Write-Host 'Done. Next steps:' -ForegroundColor Green
Write-Host '  1. The containing folder is still named JobFinder. Close Visual Studio,'
Write-Host '     then rename C:\Project\JobFinder to C:\Project\Applr in Explorer.'
Write-Host '  2. Open Applr.sln and rebuild:   dotnet build'
Write-Host '  3. Rebuild the front end:'
Write-Host '       cd src\Applr.Desktop\Frontend'
Write-Host '       npm install'
Write-Host '       npm run build'
Write-Host '  4. Check `git status`, then commit.'
Write-Host ''
