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

	[ValidateSet('windows', 'linux')]
	[string] $WorkItemOS = 'windows',

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
if ($WorkItemOS -eq 'windows' -and -not (Test-Path -LiteralPath $PlatformToolsDirectory -PathType Container)) {
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
	if ($WorkItemOS -eq 'windows') {
		Copy-PayloadDirectory -Source $PlatformToolsDirectory -Destination (Join-Path $payloadDirectory 'platform-tools')
	}

	$packageName = Escape-PowerShellSingleQuotedString ([string] $case.packageName)
	$instrumentation = Escape-PowerShellSingleQuotedString ([string] $case.instrumentation)
	$script = @'
$ErrorActionPreference = 'Stop'
$upload = $env:HELIX_WORKITEM_UPLOAD_ROOT
$adb = if ($IsWindows) {
	Join-Path $PSScriptRoot 'platform-tools\adb.exe'
} else {
	Join-Path $PSScriptRoot 'platform-tools/adb'
}
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
		$previousErrorActionPreference = $ErrorActionPreference
		$ErrorActionPreference = 'Continue'
		$deviceOutput = @(& $adb devices -l 2>&1)
		$adbExitCode = $LASTEXITCODE
		$ErrorActionPreference = $previousErrorActionPreference
		"===== attempt $attempt =====" | Add-Content -LiteralPath $deviceLog
		$deviceOutput | Tee-Object -FilePath $deviceLog -Append
		if ($adbExitCode -ne 0) {
			throw "adb devices failed with exit code $adbExitCode."
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
		$previousErrorActionPreference = $ErrorActionPreference
		$ErrorActionPreference = 'Continue'
		$instrumentationOutput = @(& $adb shell am instrument -w -r "$packageName/$instrumentation" 2>&1)
		$adbExitCode = $LASTEXITCODE
		$ErrorActionPreference = $previousErrorActionPreference
		"===== instrumentation attempt $attempt =====" | Add-Content -LiteralPath $consoleLog
		$instrumentationOutput | Tee-Object -FilePath $consoleLog -Append
		if ($adbExitCode -ne 0) {
			if ($attempt -lt 2) {
				continue
			}
			throw "adb instrument failed with exit code $adbExitCode."
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

	$bashScript = @'
#!/usr/bin/env bash
set -uo pipefail

upload="${HELIX_WORKITEM_UPLOAD_ROOT:?HELIX_WORKITEM_UPLOAD_ROOT is not set}"
adb="$(command -v adb || true)"
apk="$PWD/app.apk"
package_name='__PACKAGE_NAME__'
instrumentation='__INSTRUMENTATION__'
exit_code=1

mkdir -p "$upload"
cp "$PWD/case.json" "$upload/case.json"

fail() {
	printf 'ERROR: %s\n' "$1" | tee -a "$upload/work-item-error.log"
	exit 1
}

cleanup() {
	trap - EXIT
	set +e
	{
		echo '===== get-state ====='
		"$adb" get-state 2>&1
		echo '===== boot completion ====='
		"$adb" shell getprop sys.boot_completed 2>&1
		echo '===== disk ====='
		"$adb" shell df /data 2>&1
		echo '===== packages ====='
		"$adb" shell pm list packages -3 2>&1
		echo '===== instrumentation ====='
		"$adb" shell pm list instrumentation 2>&1
	} > "$upload/device-state.log"
	"$adb" logcat -d -b all > "$upload/logcat.log" 2>&1
	"$adb" uninstall "$package_name" >/dev/null 2>&1
	exit "$exit_code"
}
trap cleanup EXIT

[[ -n "$adb" ]] || fail 'adb was not found on PATH.'

device_ready=false
for attempt in $(seq 1 12); do
	{
		echo "===== attempt $attempt ====="
		"$adb" devices -l 2>&1
	} | tee -a "$upload/adb-devices.log"
	device_count=$("$adb" devices | awk '$2 == "device" { count++ } END { print count + 0 }')
	if [[ "$device_count" -eq 1 ]]; then
		device_ready=true
		break
	fi
	[[ "$device_count" -lt 2 ]] || fail "Expected one Android device, found $device_count."
	"$adb" kill-server >/dev/null 2>&1
	sleep 10
done
[[ "$device_ready" == true ]] || fail 'No Android device became available within two minutes.'

"$adb" uninstall "$package_name" >/dev/null 2>&1
"$adb" install -r "$apk" || fail 'adb install failed.'

device_sdk=$("$adb" shell getprop ro.build.version.sdk | tr -d '\r')
if [[ "$device_sdk" -ge 37 ]]; then
	"$adb" shell pm grant "$package_name" android.permission.ACCESS_LOCAL_NETWORK ||
		fail "Could not grant ACCESS_LOCAL_NETWORK on API $device_sdk."
elif [[ "$device_sdk" -eq 36 ]]; then
	"$adb" shell pm grant "$package_name" android.permission.NEARBY_WIFI_DEVICES ||
		fail "Could not grant NEARBY_WIFI_DEVICES on API $device_sdk."
fi

"$adb" shell getprop > "$upload/getprop.log"
"$adb" shell dumpsys package "$package_name" > "$upload/package-state.log"

for attempt in 1 2; do
	"$adb" logcat -c
	set +e
	instrumentation_output=$("$adb" shell am instrument -w -r "$package_name/$instrumentation" 2>&1)
	adb_exit_code=$?
	set -e
	{
		echo "===== instrumentation attempt $attempt ====="
		printf '%s\n' "$instrumentation_output"
	} | tee -a "$upload/console.log"
	if [[ "$adb_exit_code" -ne 0 ]]; then
		[[ "$attempt" -lt 2 ]] && continue
		fail "adb instrument failed with exit code $adb_exit_code."
	fi

	device_results_path=$(printf '%s\n' "$instrumentation_output" |
		sed -n 's/^INSTRUMENTATION_RESULT: resultsPath=//p' | tail -1 | tr -d '\r')
	if [[ -z "$device_results_path" ]]; then
		[[ "$attempt" -lt 2 ]] && continue
		fail 'Instrumentation did not report a TRX result path.'
	fi

	attempt_results="$upload/attempt-$attempt.trx"
	if ! "$adb" pull "$device_results_path" "$attempt_results"; then
		[[ "$attempt" -lt 2 ]] && continue
		fail "Failed to pull TRX from '$device_results_path'."
	fi

	python="${HELIX_PYTHONPATH:-python3}"
	failed=$("$python" -c 'import sys, xml.etree.ElementTree as E; print(sum(1 for n in E.parse(sys.argv[1]).iter() if n.tag.endswith("UnitTestResult") and n.attrib.get("outcome") == "Failed"))' "$attempt_results")
	if [[ "$failed" -eq 0 ]]; then
		cp "$attempt_results" "$upload/results.trx"
		exit_code=0
		exit 0
	fi
	if [[ "$attempt" -eq 2 ]]; then
		cp "$attempt_results" "$upload/results.trx"
		fail "$failed on-device test(s) failed after two attempts."
	fi
done
'@
	$bashScript = $bashScript.Replace('__PACKAGE_NAME__', $packageName).Replace('__INSTRUMENTATION__', $instrumentation)
	$bashScript = $bashScript.Replace("`r`n", "`n")
	[IO.File]::WriteAllText((Join-Path $payloadDirectory 'run-apk-tests.sh'), $bashScript, [Text.UTF8Encoding]::new($false))

	$cases.Add([pscustomobject] @{
		Name = [string] $case.name
		DisplayName = [string] $case.displayName
		PayloadDirectory = $payloadDirectory
		Command = if ($WorkItemOS -eq 'windows') {
			'powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File run-apk-tests.ps1'
		} else {
			'bash run-apk-tests.sh'
		}
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
		$writer.WriteElementString('Command', $case.Command)
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
