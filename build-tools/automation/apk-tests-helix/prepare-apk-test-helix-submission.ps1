[CmdletBinding()]
param (
	[Parameter(Mandatory)]
	[string] $WorkItemsDirectory,

	[Parameter(Mandatory)]
	[string] $ItemsPropsPath,

	[Parameter(Mandatory)]
	[string] $ResultsDirectory,

	[Parameter(Mandatory)]
	[string] $PlatformToolsDirectory,

	[ValidateRange(1, 1440)]
	[int] $TargetMinutes = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Copy-PayloadDirectory
{
	param (
		[Parameter(Mandatory)]
		[string] $Source,

		[Parameter(Mandatory)]
		[string] $Destination
	)

	New-Item -ItemType Directory -Force -Path $Destination | Out-Null
	if ($IsWindows) {
		& robocopy $Source $Destination /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /NFL /NDL /NJH /NJS /NP
		if ($LASTEXITCODE -gt 7) {
			throw "robocopy failed with exit code $LASTEXITCODE while copying '$Source'."
		}
		$global:LASTEXITCODE = 0
	} else {
		& cp -a "$Source/." "$Destination/"
		if ($LASTEXITCODE -ne 0) {
			throw "cp failed with exit code $LASTEXITCODE while copying '$Source'."
		}
	}
}

function Escape-PowerShellSingleQuotedString
{
	param (
		[Parameter(Mandatory)]
		[string] $Value
	)

	return $Value.Replace("'", "''")
}

$workItemsFullPath = [IO.Path]::GetFullPath($WorkItemsDirectory)
$itemsPropsFullPath = [IO.Path]::GetFullPath($ItemsPropsPath)
$resultsFullPath = [IO.Path]::GetFullPath($ResultsDirectory)

if (-not (Test-Path -LiteralPath $workItemsFullPath -PathType Container)) {
	throw "Work item directory '$workItemsFullPath' does not exist."
}
if (-not (Test-Path -LiteralPath $PlatformToolsDirectory -PathType Container)) {
	throw "Android platform-tools directory '$PlatformToolsDirectory' does not exist."
}

New-Item -ItemType Directory -Force -Path $resultsFullPath | Out-Null

$cases = [System.Collections.Generic.List[object]]::new()
foreach ($caseFile in Get-ChildItem -LiteralPath $workItemsFullPath -Filter 'case.json' -Recurse | Sort-Object FullName) {
	$case = Get-Content -LiteralPath $caseFile.FullName -Raw | ConvertFrom-Json
	$payloadDirectory = Split-Path -Parent $caseFile.FullName
	$apkPath = Join-Path $payloadDirectory 'app.apk'
	if (-not (Test-Path -LiteralPath $apkPath -PathType Leaf)) {
		throw "APK payload '$apkPath' does not exist."
	}
	Copy-PayloadDirectory -Source $PlatformToolsDirectory -Destination (Join-Path $payloadDirectory 'platform-tools')

	$packageName = Escape-PowerShellSingleQuotedString ([string] $case.packageName)
	$instrumentation = Escape-PowerShellSingleQuotedString ([string] $case.instrumentation)
	$script = @'
$ErrorActionPreference = 'Stop'
$upload = $env:HELIX_WORKITEM_UPLOAD_ROOT
$adb = Join-Path $PSScriptRoot 'platform-tools\adb.exe'
$apk = Join-Path $PSScriptRoot 'app.apk'
$packageName = '__PACKAGE_NAME__'
$instrumentation = '__INSTRUMENTATION__'
$exitCode = 1

if ([string]::IsNullOrWhiteSpace($upload)) {
	throw 'HELIX_WORKITEM_UPLOAD_ROOT is not set.'
}
New-Item -ItemType Directory -Force -Path $upload | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'case.json') -Destination (Join-Path $upload 'case.json')

try {
	$deviceLog = Join-Path $upload 'adb-devices.log'
	$deviceReady = $false
	for ($attempt = 1; $attempt -le 12; $attempt++) {
		$deviceOutput = @(& $adb devices -l 2>&1)
		"===== attempt $attempt =====" | Add-Content -LiteralPath $deviceLog
		$deviceOutput | Tee-Object -FilePath $deviceLog -Append
		if ($LASTEXITCODE -ne 0) {
			throw "adb devices failed with exit code $LASTEXITCODE."
		}
		$onlineDevices = @($deviceOutput | Where-Object { $_ -match '\sdevice(?:\s|$)' })
		if ($onlineDevices.Count -eq 1) {
			$deviceReady = $true
			break
		}
		if ($onlineDevices.Count -gt 1) {
			throw "Expected one Android device, found $($onlineDevices.Count)."
		}
		& $adb kill-server *> $null
		Start-Sleep -Seconds 10
	}
	if (-not $deviceReady) {
		throw 'No Android device became available within two minutes.'
	}

	& $adb uninstall $packageName *> $null
	& $adb install -r $apk
	if ($LASTEXITCODE -ne 0) {
		throw "adb install failed with exit code $LASTEXITCODE."
	}

	$deviceSdk = [int] ((& $adb shell getprop ro.build.version.sdk).Trim())
	if ($LASTEXITCODE -ne 0) {
		throw "Could not read the device SDK level (exit code $LASTEXITCODE)."
	}
	$localNetworkPermission = if ($deviceSdk -ge 37) {
		'android.permission.ACCESS_LOCAL_NETWORK'
	} elseif ($deviceSdk -eq 36) {
		'android.permission.NEARBY_WIFI_DEVICES'
	}
	if ($localNetworkPermission) {
		& $adb shell pm grant $packageName $localNetworkPermission
		if ($LASTEXITCODE -ne 0) {
			throw "Could not grant $localNetworkPermission on API $deviceSdk (exit code $LASTEXITCODE)."
		}
	}

	& $adb shell getprop | Set-Content -LiteralPath (Join-Path $upload 'getprop.log')
	& $adb shell dumpsys package $packageName | Set-Content -LiteralPath (Join-Path $upload 'package-state.log')

	$consoleLog = Join-Path $upload 'console.log'
	for ($attempt = 1; $attempt -le 2; $attempt++) {
		& $adb logcat -c
		$instrumentationOutput = @(& $adb shell am instrument -w -r "$packageName/$instrumentation" 2>&1)
		"===== instrumentation attempt $attempt =====" | Add-Content -LiteralPath $consoleLog
		$instrumentationOutput | Tee-Object -FilePath $consoleLog -Append
		if ($LASTEXITCODE -ne 0) {
			if ($attempt -lt 2) {
				continue
			}
			throw "adb instrument failed with exit code $LASTEXITCODE."
		}

		$resultPathMatch = [regex]::Match(($instrumentationOutput -join "`n"), '(?m)^INSTRUMENTATION_RESULT: resultsPath=(?<path>.+)$')
		if (-not $resultPathMatch.Success) {
			if ($attempt -lt 2) {
				continue
			}
			throw 'Instrumentation did not report a TRX result path.'
		}

		$deviceResultsPath = $resultPathMatch.Groups['path'].Value.Trim()
		$attemptResultsPath = Join-Path $upload "attempt-$attempt.trx"
		& $adb pull $deviceResultsPath $attemptResultsPath
		if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $attemptResultsPath -PathType Leaf)) {
			if ($attempt -lt 2) {
				continue
			}
			throw "Failed to pull TRX from '$deviceResultsPath'."
		}

		[xml] $trx = Get-Content -LiteralPath $attemptResultsPath -Raw
		$failed = @($trx.GetElementsByTagName('UnitTestResult') | Where-Object { $_.GetAttribute('outcome') -eq 'Failed' })
		if ($failed.Count -eq 0) {
			Copy-Item -LiteralPath $attemptResultsPath -Destination (Join-Path $upload 'results.trx')
			$exitCode = 0
			break
		}
		if ($attempt -eq 2) {
			Copy-Item -LiteralPath $attemptResultsPath -Destination (Join-Path $upload 'results.trx')
			throw "$($failed.Count) on-device test(s) failed after two attempts."
		}
	}
} catch {
	"ERROR: $($_.Exception.Message)" | Tee-Object -FilePath (Join-Path $upload 'work-item-error.log') -Append
} finally {
	$previousErrorActionPreference = $ErrorActionPreference
	$ErrorActionPreference = 'Continue'
	@(
		'===== get-state ====='
		(& $adb get-state 2>&1)
		'===== boot completion ====='
		(& $adb shell getprop sys.boot_completed 2>&1)
		'===== disk ====='
		(& $adb shell df /data 2>&1)
		'===== packages ====='
		(& $adb shell pm list packages -3 2>&1)
		'===== instrumentation ====='
		(& $adb shell pm list instrumentation 2>&1)
	) | Set-Content -LiteralPath (Join-Path $upload 'device-state.log')
	& $adb logcat -d -b all *> (Join-Path $upload 'logcat.log')
	& $adb uninstall $packageName *> $null
	$ErrorActionPreference = $previousErrorActionPreference
}

exit $exitCode
'@
	$script = $script.Replace('__PACKAGE_NAME__', $packageName).Replace('__INSTRUMENTATION__', $instrumentation)
	[IO.File]::WriteAllText((Join-Path $payloadDirectory 'run-apk-tests.ps1'), $script, [Text.UTF8Encoding]::new($false))

	$cases.Add([pscustomobject] @{
		Name = [string] $case.name
		DisplayName = [string] $case.displayName
		PayloadDirectory = $payloadDirectory
	})
}

if ($cases.Count -eq 0) {
	throw "No case.json files were found under '$workItemsFullPath'."
}

$settings = [Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [Text.UTF8Encoding]::new($false)
$writer = [Xml.XmlWriter]::Create($itemsPropsFullPath, $settings)
try {
	$writer.WriteStartElement('Project')
	$writer.WriteStartElement('ItemGroup')
	foreach ($case in $cases) {
		$writer.WriteStartElement('_ApkTestHelixWorkItem')
		$writer.WriteAttributeString('Include', "apk-tests-$($case.Name)")
		$writer.WriteElementString('PayloadDirectory', $case.PayloadDirectory)
		$writer.WriteElementString('Command', 'powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File run-apk-tests.ps1')
		$writer.WriteElementString('DownloadFilesFromResults', 'results.trx;console.log;logcat.log;device-state.log;getprop.log;package-state.log;case.json;work-item-error.log')
		$writer.WriteEndElement()
	}
	$writer.WriteEndElement()
	$writer.WriteEndElement()
} finally {
	$writer.Dispose()
}

[pscustomobject] @{
	targetMinutes = $TargetMinutes
	workItemCount = $cases.Count
	cases = $cases | ForEach-Object {
		[pscustomobject] @{
			name = $_.Name
			displayName = $_.DisplayName
		}
	}
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $resultsFullPath 'work-item-generation.json') -Encoding utf8NoBOM

Write-Host "Prepared $($cases.Count) APK test Helix work items."
Write-Host "##vso[task.setvariable variable=ApkTestsHelixItemsProps]$itemsPropsFullPath"
