<#
.SYNOPSIS
    Runs the Files hash benchmarks and produces a markdown report.

.DESCRIPTION
    Builds and runs Xamarin.Android.Tools.Benchmarks in Release mode,
    then copies the GitHub-flavored markdown results to
    tests/Xamarin.Android.Tools.Benchmarks/README.md.
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path (Join-Path $repoRoot 'tests') 'Xamarin.Android.Tools.Benchmarks'
$csproj = Join-Path $projectDir 'Xamarin.Android.Tools.Benchmarks.csproj'
$artifactsDir = Join-Path $projectDir 'BenchmarkDotNet.Artifacts'
$readme = Join-Path $projectDir 'README.md'

Write-Host "Building benchmarks in Release..."
dotnet build $csproj -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

Write-Host "Running benchmarks..."
dotnet run --project $csproj -c Release --no-build -- --filter '*' --exporters github --artifacts $artifactsDir
if ($LASTEXITCODE -ne 0) { throw "Benchmarks failed." }

# Find the generated markdown report
$mdFile = Get-ChildItem -Path (Join-Path $artifactsDir 'results') -Filter '*-report-github.md' |
    Sort-Object -Property LastWriteTime -Descending |
    Select-Object -First 1
if (-not $mdFile) { throw "No markdown report found in $artifactsDir\results" }

Copy-Item $mdFile.FullName $readme -Force
Write-Host "Results written to: $readme"

# Clean up artifacts
Remove-Item $artifactsDir -Recurse -Force
Write-Host "Cleaned up BenchmarkDotNet.Artifacts."
