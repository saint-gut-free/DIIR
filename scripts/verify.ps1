[CmdletBinding()]
param(
    [switch]$NoRestore,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot "DisciplesRemaster.sln"
$safeRepository = $repositoryRoot.Replace("\", "/")

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

Push-Location $repositoryRoot
try {
    if (-not $NoRestore) {
        Invoke-Checked dotnet restore $solution
    }

    Invoke-Checked dotnet build $solution --configuration $Configuration --no-restore
    Invoke-Checked dotnet test $solution --configuration $Configuration --no-build --no-restore
    Invoke-Checked dotnet format $solution --verify-no-changes --no-restore --verbosity minimal
    Invoke-Checked -Command git -Arguments @("-c", "safe.directory=$safeRepository", "diff", "--check")

    $trackedFiles = @(git -c "safe.directory=$safeRepository" ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to enumerate tracked files for the repository audit."
    }

    $forbiddenTrackedFiles = $trackedFiles | Where-Object {
        $_ -match '(^|/)(original|reference|research-input|extracted-original-assets|original-game|original-assets)/' -or
        $_ -match '\.(sg|sav|exe|dll)$'
    }
    if ($forbiddenTrackedFiles.Count -gt 0) {
        throw "Forbidden original-material candidates are tracked: $($forbiddenTrackedFiles -join ', ')"
    }

    Write-Host "Repository verification completed successfully."
}
finally {
    Pop-Location
}
