$ErrorActionPreference = 'Stop'
$environmentScript = Join-Path $PSScriptRoot 'ConfigureMauiR2RHelixEnvironment.ps1'
$powerShellExe = (Get-Process -Id $PID).Path
if (-not $powerShellExe) {
	$powerShellExe = 'powershell.exe'
}

$childScript = Join-Path ([IO.Path]::GetTempPath()) "$([IO.Path]::GetRandomFileName()).ps1"
try {
	@'
param (
	[string] $ExpectAccessToken
)

$hasAccessToken = Test-Path Env:SYSTEM_ACCESSTOKEN
if ($hasAccessToken -ne [Convert]::ToBoolean($ExpectAccessToken)) {
	Write-Error "Expected inherited SYSTEM_ACCESSTOKEN presence to be '$ExpectAccessToken', but it was '$hasAccessToken'."
	exit 1
}
'@ | Set-Content -LiteralPath $childScript -Encoding ASCII

	$testCases = @(
		@{ IsFork = 'True'; ExpectAccessToken = 'False' },
		@{ IsFork = 'False'; ExpectAccessToken = 'True' }
	)

	foreach ($testCase in $testCases) {
		$env:SYSTEM_PULLREQUEST_ISFORK = $testCase.IsFork
		$env:SYSTEM_ACCESSTOKEN = 'test-token'
		. $environmentScript

		& $powerShellExe -NoLogo -NoProfile -File $childScript $testCase.ExpectAccessToken
		if ($LASTEXITCODE -ne 0) {
			throw "MAUI R2R Helix environment test failed for fork value '$($testCase.IsFork)'."
		}
	}
} finally {
	Remove-Item -LiteralPath $childScript -Force -ErrorAction Ignore
}
