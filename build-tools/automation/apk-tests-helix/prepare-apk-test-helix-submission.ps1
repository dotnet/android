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
	& $adb devices -l | Tee-Object -FilePath (Join-Path $upload 'adb-devices.log')
	if ($LASTEXITCODE -ne 0) {
		throw "adb devices failed with exit code $LASTEXITCODE."
	}

	& $adb uninstall $packageName *> $null
	& $adb install -r $apk
	if ($LASTEXITCODE -ne 0) {
		throw "adb install failed with exit code $LASTEXITCODE."
	}

	& $adb logcat -c
	$instrumentationOutput = @(& $adb shell am instrument -w -r "$packageName/$instrumentation" 2>&1)
	$instrumentationOutput | Tee-Object -FilePath (Join-Path $upload 'console.log')
	if ($LASTEXITCODE -ne 0) {
		throw "adb instrument failed with exit code $LASTEXITCODE."
	}

	$resultPathMatch = [regex]::Match(($instrumentationOutput -join "`n"), '(?m)^INSTRUMENTATION_RESULT: resultsPath=(?<path>.+)$')
	if (-not $resultPathMatch.Success) {
		throw 'Instrumentation did not report a TRX result path.'
	}

	$deviceResultsPath = $resultPathMatch.Groups['path'].Value.Trim()
	$localResultsPath = Join-Path $upload 'results.trx'
	& $adb pull $deviceResultsPath $localResultsPath
	if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $localResultsPath -PathType Leaf)) {
		throw "Failed to pull TRX from '$deviceResultsPath'."
	}

	[xml] $trx = Get-Content -LiteralPath $localResultsPath -Raw
	$failed = @($trx.GetElementsByTagName('UnitTestResult') | Where-Object { $_.GetAttribute('outcome') -eq 'Failed' })
	if ($failed.Count -gt 0) {
		throw "$($failed.Count) on-device test(s) failed."
	}

	$exitCode = 0
} catch {
	"ERROR: $($_.Exception.Message)" | Tee-Object -FilePath (Join-Path $upload 'work-item-error.log') -Append
} finally {
	@(
		'===== get-state ====='
		(& $adb get-state 2>&1)
		'===== boot completion ====='
		(& $adb shell getprop sys.boot_completed 2>&1)
		'===== disk ====='
		(& $adb shell df /data 2>&1)
		'===== packages ====='
		(& $adb shell pm list packages -3 2>&1)
	) | Set-Content -LiteralPath (Join-Path $upload 'device-state.log')
	& $adb logcat -d -b all *> (Join-Path $upload 'logcat.log')
	& $adb uninstall $packageName *> $null
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
		$writer.WriteElementString('DownloadFilesFromResults', 'results.trx;console.log;logcat.log;device-state.log;case.json;work-item-error.log')
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
