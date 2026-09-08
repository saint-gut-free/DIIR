#requires -Version 7.0
[CmdletBinding()]
param()

# Isolated regression checks: an ignored synthetic repository and a publish stub.
# No production assembly, original material, or installed game is needed here.
$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$fixtureRoot = Join-Path $repositoryRoot ("artifacts/packaging-tests/" + [Guid]::NewGuid().ToString("N"))
$fixtureRepository = Join-Path $fixtureRoot "repository"
$fixtureScript = Join-Path $fixtureRepository "scripts/publish-headless-test-build.ps1"
$stubState = @{ PublishCalls = 0; FailPublish = $false }
$checks = 0

function Write-SyntheticFile {
    param([string]$Path, [string]$Text)
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Path)) | Out-Null
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Invoke-FixtureGit {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    & git -C $fixtureRepository -c "safe.directory=$($fixtureRepository.Replace('\', '/'))" @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Synthetic repository Git command failed." }
}

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
    $script:checks++
}

function Assert-PublishRejected {
    param([string]$OutputDirectory, [string]$ExpectedMessage)
    $failure = $null
    try {
        & $fixtureScript -NoRestore -AllowDirty -OutputDirectory $OutputDirectory
    }
    catch {
        $failure = $_.Exception.Message
    }

    Assert-Condition ($null -ne $failure -and $failure.Contains($ExpectedMessage)) `
        "Expected packaging rejection: $ExpectedMessage. Actual: $failure"
}

# Deliberately replace only dotnet within this test's scope. Packaging still uses real
# Git, files, junction/link checks, checksums, and ZIP serialization.
function dotnet {
    if ($args[0] -ne "publish") { throw "Unexpected stub invocation." }
    $stubState.PublishCalls++
    if ($stubState.FailPublish) {
        $global:LASTEXITCODE = 23
        return
    }

    $outputIndex = [Array]::IndexOf([object[]]$args, "--output")
    if ($outputIndex -lt 0) { throw "Publish must specify an output directory." }
    Write-SyntheticFile (Join-Path $args[$outputIndex + 1] "synthetic-app.txt") "Synthetic publish output."
    $global:LASTEXITCODE = 0
}

try {
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $fixtureScript)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "publish-headless-test-build.ps1") -Destination $fixtureScript
    Write-SyntheticFile (Join-Path $fixtureRepository ".gitignore") "artifacts/`n*.ignored.json`n"
    foreach ($relativeSample in @(
        "minimal.project.json", "content/synthetic.package.json", "scenarios/minimal-scenario.json", "sessions/minimal-session.json")) {
        Write-SyntheticFile (Join-Path $fixtureRepository "samples/synthetic/$relativeSample") '{"synthetic":true}'
    }

    Invoke-FixtureGit init --quiet
    Invoke-FixtureGit add -- .
    Invoke-FixtureGit -c user.name=Synthetic -c user.email=synthetic@example.invalid commit --quiet -m synthetic
    $expectedRevision = (Invoke-FixtureGit rev-parse HEAD).Trim()

    # Invoking from the parent repository must still identify the synthetic repository.
    Push-Location $repositoryRoot
    try { & $fixtureScript -NoRestore -OutputDirectory "artifacts/builds/clean/" }
    finally { Pop-Location }
    $cleanOutput = Join-Path $fixtureRepository "artifacts/builds/clean"
    $readme = [System.IO.File]::ReadAllText((Join-Path $cleanOutput "BUILD-README.txt"))
    Assert-Condition ($readme.Contains("Source revision: $expectedRevision")) "Source revision came from the wrong repository."
    Assert-Condition (Test-Path -LiteralPath "$cleanOutput.zip" -PathType Leaf) "Trailing separator placed ZIP incorrectly."
    Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $cleanOutput ".zip"))) "ZIP must not be inside package."
    Assert-PublishRejected "artifacts/builds/clean" "output already exists"

    Write-SyntheticFile (Join-Path $fixtureRepository "samples/synthetic/content/private.ignored.json") '{"localOnly":true}'
    Write-SyntheticFile (Join-Path $fixtureRepository "samples/synthetic/scenarios/untracked.json") '{"localOnly":true}'
    Write-SyntheticFile (Join-Path $fixtureRepository "samples/synthetic/sessions/untracked.sg") "Entirely synthetic sentinel."
    & $fixtureScript -NoRestore -AllowDirty -OutputDirectory "artifacts/builds/selection [test]"
    $selectionOutput = Join-Path $fixtureRepository "artifacts/builds/selection [test]"
    $sampleFiles = @(Get-ChildItem -LiteralPath (Join-Path $selectionOutput "samples") -File -Recurse)
    Assert-Condition ($sampleFiles.Count -eq 4) "Untracked/ignored local samples leaked into package."
    Assert-Condition (-not ($sampleFiles.Name -contains "private.ignored.json")) "Ignored local JSON was packaged."
    $archive = [System.IO.Compression.ZipFile]::OpenRead("$selectionOutput.zip")
    try { Assert-Condition ($archive.Entries.Count -eq 8) "Archive content differs from the bounded package files." }
    finally { $archive.Dispose() }

    $publishCallsBefore = $stubState.PublishCalls
    Assert-PublishRejected "artifacts/builds" "must be a child"
    Assert-PublishRejected "artifacts/builds/../../outside" "must be a child"
    Assert-PublishRejected "artifacts/builds-other/package" "must be a child"
    Assert-Condition ($stubState.PublishCalls -eq $publishCallsBefore) "Invalid output reached publishing."

    Write-SyntheticFile (Join-Path $fixtureRepository ".gitignore") "artifacts/*`n!artifacts/builds/`nartifacts/builds/*`n*.ignored.json`n!artifacts/builds/not-ignored.zip`n"
    Assert-PublishRejected "artifacts/builds/not-ignored" "must remain ignored"
    Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $fixtureRepository "artifacts/builds/not-ignored"))) `
        "Non-ignored output was created before rejection."
    Write-SyntheticFile (Join-Path $fixtureRepository ".gitignore") "artifacts/`n*.ignored.json`n"

    $stubState.FailPublish = $true
    Assert-PublishRejected "artifacts/builds/failed-native" "Command failed with exit code 23"
    Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $fixtureRepository "artifacts/builds/failed-native.zip"))) `
        "A failed publish produced a distribution archive."
    $stubState.FailPublish = $false

    $linkTarget = Join-Path $fixtureRoot "synthetic-target"
    [System.IO.Directory]::CreateDirectory($linkTarget) | Out-Null
    $outputLink = Join-Path $fixtureRepository "artifacts/builds/link"
    $linkAvailable = $false
    try {
        $linkType = if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) { "Junction" } else { "SymbolicLink" }
        New-Item -ItemType $linkType -Path $outputLink -Target $linkTarget | Out-Null
        $linkAvailable = $true
    }
    catch { Write-Host "SKIP: platform could not create a synthetic directory link." }
    if ($linkAvailable) {
        Assert-PublishRejected "artifacts/builds/link/package" "must not traverse"
        Assert-Condition (@(Get-ChildItem -LiteralPath $linkTarget -Force).Count -eq 0) "Output link target was modified."
        [System.IO.Directory]::Delete($outputLink)

        $sampleDirectory = Join-Path $fixtureRepository "samples/synthetic/content"
        $sampleBackup = Join-Path $fixtureRepository "samples/synthetic/content-backup"
        [System.IO.Directory]::Move($sampleDirectory, $sampleBackup)
        New-Item -ItemType $linkType -Path $sampleDirectory -Target $sampleBackup | Out-Null
        try { Assert-PublishRejected "artifacts/builds/sample-link" "must not traverse" }
        finally {
            [System.IO.Directory]::Delete($sampleDirectory)
            [System.IO.Directory]::Move($sampleBackup, $sampleDirectory)
        }
        Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $fixtureRepository "artifacts/builds/sample-link"))) `
            "Linked sample was rejected only after output creation."
    }

    Invoke-FixtureGit add -- samples/synthetic/sessions/untracked.sg
    Assert-PublishRejected "artifacts/builds/forbidden-tracked" "Only project-owned tracked JSON"

    Write-Host "Headless packaging regression checks passed: $checks."
}
finally {
    # The validated target is a unique child of the ignored test root, never a user input.
    $cleanupParent = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts/packaging-tests"))
    $resolvedFixture = [System.IO.Path]::GetFullPath($fixtureRoot)
    if ([System.IO.Path]::GetDirectoryName($resolvedFixture) -ne $cleanupParent) {
        throw "Refusing cleanup outside the synthetic packaging test root."
    }

    if (Test-Path -LiteralPath $resolvedFixture) { Remove-Item -LiteralPath $resolvedFixture -Recurse -Force }
}
