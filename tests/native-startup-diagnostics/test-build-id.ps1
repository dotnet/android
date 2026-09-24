param (
    [Parameter(Mandatory = $true)][string] $MSBuildPath,
    [string] $TestOutputPath = "$PSScriptRoot\..\..\bin\guest-readiness-build-id"
)
$ErrorActionPreference = 'Stop'
$TestOutputPath = [IO.Path]::GetFullPath($TestOutputPath)
$project = "$PSScriptRoot\build-id-test.proj"
$stamp = "$TestOutputPath\startup-diagnostics-build-id.txt"
$oldTime = [DateTime]::new(2001, 1, 1, 0, 0, 0, [DateTimeKind]::Utc)

function Invoke-Observe([string] $Marker, [bool] $ExpectSuccess = $true) {
    $output = & $MSBuildPath $project /nologo /v:minimal /t:Observe "/p:TestOutputPath=$TestOutputPath" "/p:AndroidStartupDiagnosticsBuildId=$Marker" 2>&1
    if (($LASTEXITCODE -eq 0) -ne $ExpectSuccess) {
        throw "Unexpected MSBuild result: $output"
    }
}

Invoke-Observe 'test-initial-state'
foreach ($marker in @('', 'android-d549-diag1', 'android-d549-diag2', '')) {
    if (Test-Path $stamp) { [IO.File]::SetLastWriteTimeUtc($stamp, $oldTime) }
    Invoke-Observe $marker
    if ((Get-Content $stamp -Raw).Trim() -cne "marker=$marker") { throw 'Wrong configure input value' }
    if ((Get-Content "$TestOutputPath\argument.txt" -Raw).Trim() -cne "-DXA_STARTUP_DIAGNOSTICS_BUILD_ID=`"$marker`"") {
        throw 'CMake argument must pass exact marker, including empty disable value'
    }
    if (-not ((Get-Content "$TestOutputPath\inputs.txt") -contains $stamp)) { throw 'Marker missing from incremental inputs' }
    if ([IO.File]::GetLastWriteTimeUtc($stamp) -eq $oldTime) { throw 'Changed marker did not invalidate configuration' }
    [IO.File]::SetLastWriteTimeUtc($stamp, $oldTime)
    Invoke-Observe $marker
    if ([IO.File]::GetLastWriteTimeUtc($stamp) -ne $oldTime) { throw 'Unchanged marker forced unnecessary configuration' }
}
foreach ($invalid in @('Uppercase', '-prefix', 'contains space', ('a' * 65), 'quote"value', "trailing`n", "trailing`r")) {
    Invoke-Observe $invalid $false
    if ([IO.File]::GetLastWriteTimeUtc($stamp) -ne $oldTime) { throw 'Invalid marker reached configuration input mutation' }
}
Write-Output 'Build marker validation and empty/set/change/disable incremental tests passed.'
exit 0
