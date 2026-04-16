#requires -Version 5.1
<#
.SYNOPSIS
  Publishes Yoink (single-file, self-contained) and zips the output.

.PARAMETER Rid
  Runtime identifier (default: win-x64). Examples: win-x86, win-arm64

.PARAMETER Configuration
  Build configuration (default: Release)

.PARAMETER SkipTests
  Skip dotnet test before publish

.PARAMETER PublishDir
  Folder for dotnet publish output (relative to repo root, default: release)

.PARAMETER DistDir
  Folder for the zip artifact (relative to repo root, default: dist)

.EXAMPLE
  .\scripts\build.ps1

.EXAMPLE
  .\scripts\build.ps1 -Rid win-arm64 -SkipTests
#>
param(
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Rid = "win-x64",

    [string]$Configuration = "Release",

    [switch]$SkipTests,

    [string]$PublishDir = "release",

    [string]$DistDir = "dist"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Csproj = Join-Path $RepoRoot "src\Yoink\Yoink.csproj"
$PublishPath = Join-Path $RepoRoot $PublishDir
$DistPath = Join-Path $RepoRoot $DistDir

if (-not (Test-Path $Csproj)) {
    throw "Project not found: $Csproj"
}

$projXml = [xml](Get-Content -LiteralPath $Csproj -Raw)
$version = $null
foreach ($pg in $projXml.Project.PropertyGroup) {
    if ($pg.Version) {
        $version = $pg.Version.Trim()
        break
    }
}
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read <Version> from Yoink.csproj"
}

Write-Host "Yoink $version - publish $Rid ($Configuration)" -ForegroundColor Cyan

Push-Location $RepoRoot
try {
    dotnet restore $Csproj -r $Rid
    if (-not $SkipTests) {
        $testProj = Join-Path $RepoRoot "tests\Yoink.Tests\Yoink.Tests.csproj"
        dotnet test $testProj -c $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    if (Test-Path $PublishPath) {
        Remove-Item -LiteralPath $PublishPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $PublishPath -Force | Out-Null

    dotnet publish $Csproj `
        -c $Configuration `
        -r $Rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -o $PublishPath

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    New-Item -ItemType Directory -Path $DistPath -Force | Out-Null
    $zipName = "Yoink-$version-$Rid.zip"
    $zipPath = Join-Path $DistPath $zipName

    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $PublishPath "*") -DestinationPath $zipPath -CompressionLevel Optimal

    Write-Host "Published: $PublishPath" -ForegroundColor Green
    Write-Host "Zip:       $zipPath" -ForegroundColor Green
}
finally {
    Pop-Location
}
