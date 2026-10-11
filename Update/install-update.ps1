param(
    [Parameter(Mandatory=$true)][string]$JobFile,
    [switch]$NoRestart,
    [switch]$TestFailAfterBuildSwap
)

$ErrorActionPreference = 'Stop'
$jobRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$logPath = Join-Path $jobRoot 'update-result.log'
$oldBuildMoved = $false
$changedFiles = [Collections.Generic.List[object]]::new()
$installRoot = $null
$backupRoot = $null

function Log([string]$Message) {
    Add-Content -LiteralPath $logPath -Value ((Get-Date -Format o) + ' ' + $Message) -Encoding UTF8
}
function FileSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','') }
    finally { $stream.Dispose();$hasher.Dispose() }
}
function Contained([string]$Root, [string]$Candidate) {
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath($Candidate)
    if (!$full.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw "Path is outside its expected folder: $full" }
    return $full
}
function RejectLinks([string]$Root, [string]$Path) {
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $current = [IO.Path]::GetFullPath($Path)
    while ($current.Length -ge $base.Length) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Linked folders are not supported for updates: $current" }
        }
        if ($current.Equals($base,[StringComparison]::OrdinalIgnoreCase)) { break }
        $current = [IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($current)) { break }
    }
}
function RestartGame {
    if (!$NoRestart) {
        $exe = Contained $installRoot (Join-Path $installRoot 'Build\GRIP_BREAK.exe')
        $restartArguments = [string]$job.restartArguments
        if ([string]::IsNullOrWhiteSpace($restartArguments)) { $restartArguments = '-screen-width 1920 -screen-height 1080 -screen-fullscreen 0' }
        Start-Process -FilePath $exe -WorkingDirectory $installRoot -ArgumentList $restartArguments
    }
}
function VerifyStagedPackage([string]$ArchivePath, [string]$PackageRoot, [object[]]$Entries) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $expectedFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $allowedDirectories = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$expanded = 0
    try {
        if ($archive.Entries.Count -gt 20000) { throw 'Too many update archive entries.' }
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\','/')
            if (!$name.StartsWith('GRIP_BREAK/',[StringComparison]::Ordinal) -or $name.Contains(':') -or $name.Contains([char]0) -or $name.StartsWith('GRIP_BREAK/Source/',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected archive path.' }
            foreach ($part in $name.Split('/')) { if ($part -eq '..' -or $part -eq '.') { throw 'Unsafe archive path.' } }
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'Archive links are unsupported.' }
            $relative = $name.Substring('GRIP_BREAK/'.Length).TrimEnd('/').Replace('/','\')
            if (!$seen.Add($relative)) { throw 'Duplicate archive path.' }
            $expanded += $entry.Length
            if ($expanded -gt 1536L*1024*1024) { throw 'Expanded update is too large.' }
            if ([string]::IsNullOrEmpty($relative)) {
                if ($entry.Length -ne 0 -or !$name.EndsWith('/')) { throw 'Invalid package root entry.' }
                continue
            }
            $expected = Contained $PackageRoot (Join-Path $PackageRoot $relative)
            RejectLinks $PackageRoot $expected
            $parent = [IO.Path]::GetDirectoryName($relative)
            while (![string]::IsNullOrEmpty($parent)) { $null=$allowedDirectories.Add($parent);$parent=[IO.Path]::GetDirectoryName($parent) }
            if ($name.EndsWith('/')) {
                if ($entry.Length -ne 0) { throw 'Archive folder contains file bytes.' }
                $null=$allowedDirectories.Add($relative)
                continue
            }
            $null=$expectedFiles.Add($relative)
            if (!(Test-Path -LiteralPath $expected -PathType Leaf)) { throw "Staged file is missing: $relative" }
            if ((Get-Item -LiteralPath $expected -Force).Length -ne $entry.Length) { throw "Staged file length differs: $relative" }
            $stream = $entry.Open()
            $hasher = [Security.Cryptography.SHA256]::Create()
            try { $entryHash=[BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','') }
            finally { $stream.Dispose();$hasher.Dispose() }
            if ((FileSha256 $expected) -ne $entryHash) { throw "Staged file checksum differs: $relative" }
        }
        foreach ($item in $Entries) {
            $relative = $item.FullName.Substring($PackageRoot.Length).TrimStart('\')
            if ($item.PSIsContainer) {
                if (!$allowedDirectories.Contains($relative)) { throw "Unexpected staged folder: $relative" }
            } elseif (!$expectedFiles.Contains($relative)) { throw "Unexpected staged file: $relative" }
        }
        Log ("Verified staged package bytes against archive: " + $expectedFiles.Count + ' files.')
    } finally { $archive.Dispose() }
}

try {
    $jobPath = [IO.Path]::GetFullPath($JobFile)
    if (!$jobPath.Equals((Join-Path $jobRoot 'update-job.json'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Installer job must be beside the bundled helper.' }
    $job = Get-Content -LiteralPath $jobPath -Raw | ConvertFrom-Json
    $installRoot = [IO.Path]::GetFullPath([string]$job.installRoot).TrimEnd('\')
    if ($installRoot.Length -le [IO.Path]::GetPathRoot($installRoot).Length) { throw 'A drive root cannot be a game installation.' }
    $installBuild = Contained $installRoot (Join-Path $installRoot 'Build')
    $exePath = Contained $installRoot (Join-Path $installBuild 'GRIP_BREAK.exe')
    if (!(Test-Path -LiteralPath $exePath -PathType Leaf) -or !(Test-Path -LiteralPath (Join-Path $installBuild 'GRIP_BREAK_Data') -PathType Container)) { throw 'The current game installation is missing.' }
    RejectLinks $installRoot $installBuild
    # Keep the backup on the installation volume, allowing directory rename and
    # rollback even when the game and persistent-data folder are on different drives.
    $backupRoot = Contained $installRoot (Join-Path $installRoot ('.grip-update-backup-' + [Guid]::NewGuid().ToString('N')))
    $packageRoot = [IO.Path]::GetFullPath([string]$job.packageRoot)
    $expectedPackage = Join-Path $jobRoot 'stage\GRIP_BREAK'
    if (!$packageRoot.Equals($expectedPackage,[StringComparison]::OrdinalIgnoreCase)) { throw 'The staged package path does not match this job.' }
    $zipPath = [IO.Path]::GetFullPath([string]$job.zipPath)
    if (!$zipPath.Equals((Join-Path $jobRoot 'update.zip'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected update archive path.' }
    if ([string]$job.expectedSha -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid checksum in update job.' }
    if ((FileSha256 $zipPath) -ne [string]$job.expectedSha) { throw 'The downloaded update checksum changed.' }
    RejectLinks $jobRoot $packageRoot
    $packageEntries = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -Force)
    foreach ($item in $packageEntries) { RejectLinks $packageRoot $item.FullName }
    $packageFiles = @($packageEntries | Where-Object { !$_.PSIsContainer })
    VerifyStagedPackage $zipPath $packageRoot $packageEntries
    $packageBuild = Contained $packageRoot (Join-Path $packageRoot 'Build')
    if (!(Test-Path -LiteralPath (Join-Path $packageBuild 'GRIP_BREAK.exe') -PathType Leaf) -or !(Test-Path -LiteralPath (Join-Path $packageBuild 'GRIP_BREAK_Data') -PathType Container)) { throw 'The replacement game is missing.' }
    if (Test-Path -LiteralPath (Join-Path $packageRoot 'Source')) { throw 'Runtime updates cannot overwrite editable source.' }
    $parentId = [int]$job.parentPid
    if ($parentId -le 0 -or $parentId -eq $PID) { throw 'Invalid game process id.' }
    Log "Waiting for game process $parentId to close."
    $deadline = (Get-Date).AddSeconds(45)
    while (Get-Process -Id $parentId -ErrorAction SilentlyContinue) {
        if ((Get-Date) -ge $deadline) { throw 'The game did not close; nothing was changed.' }
        Start-Sleep -Milliseconds 250
    }
    New-Item -ItemType Directory -Path $backupRoot -ErrorAction Stop | Out-Null
    $backupBuild = Contained $backupRoot (Join-Path $backupRoot 'Build')
    # Both paths are resolved and checked before moving the existing game.
    Move-Item -LiteralPath $installBuild -Destination $backupBuild
    $oldBuildMoved = $true
    Copy-Item -LiteralPath $packageBuild -Destination $installBuild -Recurse
    if ($TestFailAfterBuildSwap) { throw 'Requested verification failure after Build replacement.' }
    foreach ($file in $packageFiles) {
        $relative = $file.FullName.Substring($packageRoot.Length).TrimStart('\')
        if ($relative.StartsWith('Build\',[StringComparison]::OrdinalIgnoreCase)) { continue }
        $destination = Contained $installRoot (Join-Path $installRoot $relative)
        RejectLinks $installRoot $destination
        $hadFile = Test-Path -LiteralPath $destination -PathType Leaf
        $backupFile = Contained $backupRoot (Join-Path $backupRoot ('files\' + $relative))
        if ($hadFile) {
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($backupFile)) -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $backupFile
        }
        # Register before copying, so partial or failed copies also roll back.
        $changedFiles.Add([pscustomobject]@{ Destination=$destination; Backup=$backupFile; HadFile=$hadFile })
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }
    Log ("SUCCESS " + [string]$job.version + '. Previous files retained in ' + $backupRoot)
    RestartGame
    exit 0
} catch {
    Log ('FAILED ' + $_.Exception.Message)
    $rollbackFailed = $false
    for ($i=$changedFiles.Count-1; $i -ge 0; $i--) {
        $change = $changedFiles[$i]
        try {
            $destination = Contained $installRoot $change.Destination
            if ($change.HadFile) { Copy-Item -LiteralPath $change.Backup -Destination $destination -Force }
            elseif (Test-Path -LiteralPath $destination -PathType Leaf) { Remove-Item -LiteralPath $destination -Force }
        } catch { $rollbackFailed=$true;Log ('FILE ROLLBACK FAILED ' + $_.Exception.Message) }
    }
    if ($oldBuildMoved) {
        try {
            $installBuild = Contained $installRoot (Join-Path $installRoot 'Build')
            $backupBuild = Contained $backupRoot (Join-Path $backupRoot 'Build')
            # Never delete outside the verified Build folder of this installation.
            if (Test-Path -LiteralPath $installBuild) { RejectLinks $installRoot $installBuild;Remove-Item -LiteralPath $installBuild -Recurse -Force }
            Move-Item -LiteralPath $backupBuild -Destination $installBuild
            Log 'Previous Build restored.'
        } catch { $rollbackFailed=$true;Log ('BUILD ROLLBACK FAILED ' + $_.Exception.Message) }
    }
    if (!$rollbackFailed -and $oldBuildMoved) {
        try { RestartGame } catch { Log ('RESTART FAILED ' + $_.Exception.Message) }
    }
    exit 1
}
