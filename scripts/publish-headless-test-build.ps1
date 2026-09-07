[CmdletBinding()]
param(
    [switch]$NoRestore,

    [switch]$AllowDirty,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OutputDirectory = "artifacts/builds/headless-test"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$safeRepository = $repositoryRoot.Replace("\", "/")
$buildsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts/builds"))
$requestedOutput = if ([System.IO.Path]::IsPathFullyQualified($OutputDirectory)) {
    $OutputDirectory
}
else {
    Join-Path $repositoryRoot $OutputDirectory
}
$outputPath = [System.IO.Path]::GetFullPath($requestedOutput)
$isWindowsPlatform = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
$pathComparison = if ($isWindowsPlatform) {
    [System.StringComparison]::OrdinalIgnoreCase
}
else {
    [System.StringComparison]::Ordinal
}
$requiredPrefix = $buildsRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $outputPath.StartsWith($requiredPrefix, $pathComparison)) {
    throw "Test build output must be a child of artifacts/builds."
}

$archivePath = "${outputPath}.zip"
$revision = (git -c "safe.directory=$safeRepository" rev-parse --verify HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($revision)) {
    throw "Unable to identify the source revision."
}

$workingTreeChanges = @(git -c "safe.directory=$safeRepository" status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect the source working tree."
}

if ($workingTreeChanges.Count -gt 0 -and -not $AllowDirty) {
    throw "The source working tree is not clean. Commit or stash changes, or explicitly use -AllowDirty for a local provisional build."
}

$sourceRevision = if ($workingTreeChanges.Count -gt 0) { "${revision}-dirty" } else { $revision }

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string]$Command,

        [Parameter(ValueFromRemainingArguments)]
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Command $($Arguments -join ' ')"
    }
}

function Assert-OutputDoesNotExist {
    if ((Test-Path -LiteralPath $outputPath) -or (Test-Path -LiteralPath $archivePath)) {
        throw "Test build output already exists. Choose a new -OutputDirectory or remove the old ignored artifact manually."
    }
}

function Copy-SyntheticSamples {
    $sampleOutput = Join-Path $outputPath "samples"
    New-Item -ItemType Directory -Path $sampleOutput | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "samples/synthetic/minimal.project.json") -Destination $sampleOutput
    foreach ($directoryName in @("content", "scenarios", "sessions")) {
        Copy-Item `
            -LiteralPath (Join-Path $repositoryRoot "samples/synthetic/$directoryName") `
            -Destination $sampleOutput `
            -Recurse
    }
}

function Write-BuildReadme {
    $platformName = if ($isWindowsPlatform) { "Windows" } else { "the build host platform" }
    $gameCommand = if ($isWindowsPlatform) {
        ".\game\DisciplesRemaster.Godot.exe"
    }
    else {
        "./game/DisciplesRemaster.Godot"
    }
    $editorCommand = if ($isWindowsPlatform) {
        ".\editor\DisciplesRemaster.Editor.exe"
    }
    else {
        "./editor/DisciplesRemaster.Editor"
    }
    $readme = @"
Disciples Remaster Research Project - headless test build

Requirements:
- $platformName
- .NET 10 runtime

This package contains only independently written project code and synthetic samples.
It does not contain original Disciples II files, assets, maps, saves, or executable code.
It is a headless diagnostic build, not the future graphical Godot client.

Try the validated synthetic project:

  $gameCommand validate-project ./samples/minimal.project.json
  $gameCommand summary-project ./samples/minimal.project.json
  $gameCommand render-project ./samples/minimal.project.json --width 8 --height 6

Inspect and edit project-owned JSON metadata:

  $editorCommand validate ./samples/scenarios/minimal-scenario.json
  $editorCommand validate-content ./samples/content/synthetic.package.json

Generated from repository configuration: $Configuration
Source revision: $sourceRevision
"@
    [System.IO.File]::WriteAllText(
        (Join-Path $outputPath "BUILD-README.txt"),
        $readme.Replace("`r`n", "`n"),
        [System.Text.UTF8Encoding]::new($false))
}

function Write-Checksums {
    $files = @(Get-ChildItem -LiteralPath $outputPath -File -Recurse |
        Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
        Sort-Object -CaseSensitive { [System.IO.Path]::GetRelativePath($outputPath, $_.FullName) })
    $lines = @(
        $files | ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($outputPath, $_.FullName).Replace("\", "/")
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "${hash}  ${relativePath}"
        }
    )
    [System.IO.File]::WriteAllLines(
        (Join-Path $outputPath "SHA256SUMS.txt"),
        $lines,
        [System.Text.UTF8Encoding]::new($false))
}

Push-Location $repositoryRoot
try {
    Assert-OutputDoesNotExist
    New-Item -ItemType Directory -Path $outputPath | Out-Null

    if (-not $NoRestore) {
        Invoke-Checked dotnet restore (Join-Path $repositoryRoot "DisciplesRemaster.sln")
    }

    Invoke-Checked dotnet publish `
        (Join-Path $repositoryRoot "game/DisciplesRemaster.Godot/DisciplesRemaster.Godot.csproj") `
        --configuration $Configuration `
        --no-restore `
        --output (Join-Path $outputPath "game") `
        "-p:DebugType=None" `
        "-p:DebugSymbols=false"
    Invoke-Checked dotnet publish `
        (Join-Path $repositoryRoot "editor/DisciplesRemaster.Editor/DisciplesRemaster.Editor.csproj") `
        --configuration $Configuration `
        --no-restore `
        --output (Join-Path $outputPath "editor") `
        "-p:DebugType=None" `
        "-p:DebugSymbols=false"

    Copy-SyntheticSamples
    Write-BuildReadme

    $forbiddenSamples = @(Get-ChildItem -LiteralPath (Join-Path $outputPath "samples") -File -Recurse |
        Where-Object { $_.Extension -in @(".sg", ".sav") })
    if ($forbiddenSamples.Count -gt 0) {
        throw "Original-material candidate found in packaged samples."
    }

    Write-Checksums
    Compress-Archive -Path (Join-Path $outputPath "*") -DestinationPath $archivePath
    Write-Host "Headless test build created."
    Write-Host "Directory: $outputPath"
    Write-Host "Archive: $archivePath"
}
finally {
    Pop-Location
}
