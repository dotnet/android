# Collect once in the producer so all parallel slices use the same timing data.
# Empty timing files preserve count-based slicing when history is unavailable.
# https://learn.microsoft.com/rest/api/azure/devops/test/results/list
param (
	[Parameter(Mandatory)]
	[uri] $CollectionUri,
	[Parameter(Mandatory)]
	[string] $Project,
	[Parameter(Mandatory)]
	[ValidateRange(1, [int]::MaxValue)]
	[int] $DefinitionId,
	[Parameter(Mandatory)]
	[string] $OutputDirectory,
	[ValidateRange(1, 300)]
	[int] $TimeBudgetSeconds = 60
)

$ErrorActionPreference = 'Stop'
if ($CollectionUri.Scheme -ne 'https') {
	throw 'The Azure DevOps collection URI must use HTTPS.'
}

$api = "$($CollectionUri.AbsoluteUri.TrimEnd('/'))/$([uri]::EscapeDataString($Project))/_apis"
$clock = [Diagnostics.Stopwatch]::StartNew()
$groups = @(
	@{ Name = 'windows'; Prefix = 'Xamarin.Android.Build.Tests - Windows-' },
	@{ Name = 'macos'; Prefix = 'Xamarin.Android.Build.Tests - macOS-' },
	@{ Name = 'macos-device'; Prefix = 'MSBuildDeviceIntegration On Device - macOS-' }
)
foreach ($group in $groups) {
	$group.Timings = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
}

function Invoke-TimingRequest ([string] $Path) {
	$remaining = $TimeBudgetSeconds - $clock.Elapsed.TotalSeconds
	if ($remaining -le 0) {
		throw [TimeoutException]::new('The test timing history time budget was exhausted.')
	}
	$response = Invoke-RestMethod -Uri "$api/$Path" `
		-Headers @{ Authorization = "Bearer $env:SYSTEM_ACCESSTOKEN" } `
		-TimeoutSec ([int][Math]::Min(10, [Math]::Ceiling($remaining))) `
		-MaximumRedirection 0
	if ($null -eq $response.value) {
		throw [IO.InvalidDataException]::new('The test timing history response has no value array.')
	}
	return $response
}

$complete = $false
try {
	if ([string]::IsNullOrWhiteSpace($env:SYSTEM_ACCESSTOKEN)) {
		Write-Warning 'No Azure DevOps access token is available; using count-based test slicing.'
	} else {
		# Only use trusted main builds, never timing data supplied by another PR.
		$builds = Invoke-TimingRequest "build/builds?definitions=$DefinitionId&branchName=refs%2Fheads%2Fmain&statusFilter=completed&resultFilter=succeeded&queryOrder=finishTimeDescending&`$top=1&api-version=7.1"
		if (@($builds.value).Count -eq 0) {
			Write-Host 'No successful main build is available; using count-based test slicing.'
		} else {
			$buildId = [int] $builds.value[0].id
			Write-Host "Collecting test timings from successful main build $buildId."
			$runSkip = 0
			do {
				$runs = @((Invoke-TimingRequest "test/runs?buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F$buildId&includeRunDetails=true&`$top=100&`$skip=$runSkip&api-version=7.1").value)
				foreach ($run in $runs) {
					if ($run.state -ne 'Completed') {
						continue
					}
					$group = $groups | Where-Object { $run.name.StartsWith($_.Prefix, [StringComparison]::Ordinal) }
					if ($null -eq $group) {
						continue
					}
					$runId = [int] $run.id
					$resultSkip = 0
					do {
						$results = @((Invoke-TimingRequest "test/Runs/$runId/results?outcomes=Passed&`$top=1000&`$skip=$resultSkip&api-version=7.1").value)
						foreach ($result in $results) {
							if ($result.outcome -ne 'Passed') {
								continue
							}
							$name = $result.automatedTestName
							if ([string]::IsNullOrWhiteSpace($name) -or $null -eq $result.durationInMs) {
								throw [IO.InvalidDataException]::new("A passed test in run $runId has no name or duration.")
							}
							$duration = [double] $result.durationInMs
							if (-not [double]::IsFinite($duration) -or $duration -lt 0 -or $duration -gt [int]::MaxValue) {
								throw [IO.InvalidDataException]::new("A passed test in run $runId has an invalid duration.")
							}
							if (-not $group.Timings.ContainsKey($name)) {
								$group.Timings.Add($name, @{ Total = 0.0; Count = 0 })
							}
							$entry = $group.Timings[$name]
							$entry.Total += $duration
							$entry.Count++
						}
						$resultSkip += $results.Count
					} while ($results.Count -eq 1000)
				}
				$runSkip += $runs.Count
			} while ($runs.Count -eq 100)
		}
	}
	$complete = $true
} catch [System.Net.Http.HttpRequestException] {
	$reason = $_.Exception.GetType().Name
	if ($null -ne $_.Exception.StatusCode) {
		$reason = "HTTP $([int] $_.Exception.StatusCode)"
	}
	Write-Warning "Azure DevOps timing history could not be downloaded ($reason); using count-based test slicing."
} catch [OperationCanceledException] {
	Write-Warning 'The Azure DevOps timing history request timed out; using count-based test slicing.'
} catch [TimeoutException] {
	Write-Warning 'The test timing history time budget was exhausted; using count-based test slicing.'
}

[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
foreach ($group in $groups) {
	# A service failure must not leave a partially collected snapshot behind.
	if (-not $complete) {
		$group.Timings.Clear()
	}
	$settings = [Xml.XmlWriterSettings]::new()
	$settings.Indent = $true
	$settings.Encoding = [Text.UTF8Encoding]::new($false)
	$writer = [Xml.XmlWriter]::Create((Join-Path $OutputDirectory "$($group.Name).xml"), $settings)
	try {
		$writer.WriteStartElement('tests')
		$names = [string[]] @($group.Timings.Keys)
		[Array]::Sort($names, [StringComparer]::Ordinal)
		foreach ($name in $names) {
			$entry = $group.Timings[$name]
			$duration = [int] [Math]::Max(1, [Math]::Truncate($entry.Total / $entry.Count))
			$writer.WriteStartElement('test')
			$writer.WriteAttributeString('name', $name)
			$writer.WriteAttributeString('duration', $duration.ToString([Globalization.CultureInfo]::InvariantCulture))
			$writer.WriteEndElement()
		}
		$writer.WriteEndElement()
	} finally {
		$writer.Dispose()
	}
	Write-Host "$($group.Name): $($group.Timings.Count) test timings written."
	if ($group.Timings.Count -eq 0) {
		Write-Host "$($group.Name): no timing history; the slicer will balance by test count."
	}
}
