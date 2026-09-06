[CmdletBinding()]
param(
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot "DisciplesRemaster.sln"

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

    Invoke-Checked dotnet build $solution --no-restore
    Invoke-Checked dotnet test $solution --no-build --no-restore
    Invoke-Checked git diff --check

    $forbiddenTrackedFiles = @(git ls-files) | Where-Object {
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
