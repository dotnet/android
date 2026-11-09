<#
.SYNOPSIS
    Checks compiler, analyzer, and MSBuild warnings without running benchmarks.

.DESCRIPTION
    Requires clean Debug and Release builds, then verifies that temporary
    CS1030, CA2014, and MSBuild warning probes fail with the expected errors.
    Removes the probes and builds again without them, including after a failure.

.EXAMPLE
    pwsh -NoProfile -File tests/Xamarin.Android.Tools.Benchmarks/check-warning-policy.ps1

.EXAMPLE
    ./check-warning-policy.ps1 -DotNet /path/to/dotnet
#>
param ([string] $DotNet = 'dotnet')

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$project = Join-Path $PSScriptRoot 'Xamarin.Android.Tools.Benchmarks.csproj'
$probeRoot = Join-Path $PSScriptRoot "obj/warning-policy-$([Guid]::NewGuid().ToString('N'))"
$probesStarted = $false

function Invoke-PolicyBuild {
    param ([string] $Configuration, [string[]] $ExtraArguments = @(), [string] $ExpectedCode = '')

    $buildArguments = @(
        'build', $project, '-c', $Configuration, '--nologo', '-v:minimal',
        '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '--disable-build-servers'
    ) + $ExtraArguments
    $previousLanguage = $env:DOTNET_CLI_UI_LANGUAGE
    try {
        $env:DOTNET_CLI_UI_LANGUAGE = 'en-US'
        $output = & $DotNet @buildArguments 2>&1 | Out-String
        $exitCode = $LASTEXITCODE
    } finally {
        $env:DOTNET_CLI_UI_LANGUAGE = $previousLanguage
    }

    $diagnostics = [regex]::Matches($output, '(?im)^[ \t]*(?:.*?:[ \t]+)?(error|warning)[ \t]*(?:([a-z]+\d+)[ \t]*)?:')
    if ($ExpectedCode) {
        $unexpected = @($diagnostics | Where-Object {
            $_.Groups[1].Value -ne 'error' -or $_.Groups[2].Value -ne $ExpectedCode
        })
        if ($exitCode -eq 0 -or $diagnostics.Count -eq 0 -or $unexpected.Count -ne 0) {
            throw "$Configuration must fail only with error $ExpectedCode (exit $exitCode).`n$output"
        }
        Write-Host "Passed: $Configuration rejects $ExpectedCode."
    } else {
        if ($exitCode -ne 0 -or $diagnostics.Count -ne 0) {
            throw "$Configuration must build without warnings or errors (exit $exitCode).`n$output"
        }
        Write-Host "Passed: clean $Configuration build."
    }
}

$probes = @(
    @{ Code = 'CS1030'; Source = '#warning Compiler warning policy probe' },
    @{ Code = 'CA2014'; Source = @'
using System;

internal static class WarningPolicyProbe
{
	public static int Run (int count)
	{
		int total = 0;
		for (int i = 0; i < count; i++) {
			Span<byte> buffer = stackalloc byte [16];
			buffer [0] = (byte) i;
			total += buffer [0];
		}
		return total;
	}
}
'@ },
    @{ Code = 'BP0001'; Source = '' }
)

New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null
try {
    foreach ($configuration in 'Debug', 'Release') {
        Invoke-PolicyBuild -Configuration $configuration
        foreach ($probe in $probes) {
            $directory = Join-Path $probeRoot "$configuration-$($probe.Code)"
            New-Item -ItemType Directory -Path $directory | Out-Null
            $targets = Join-Path $directory 'Probe.targets'
            if ($probe.Source) {
                Set-Content -LiteralPath (Join-Path $directory 'Probe.cs') -Value $probe.Source -Encoding UTF8
                @'
<Project>
  <ItemGroup Condition=" '$(MSBuildProjectName)' == 'Xamarin.Android.Tools.Benchmarks' ">
    <Compile Include="$(MSBuildThisFileDirectory)Probe.cs" />
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath $targets -Encoding UTF8
            } else {
                @'
<Project>
  <Target Name="_AndroidWarningPolicyProbe" BeforeTargets="CoreCompile" Condition=" '$(MSBuildProjectName)' == 'Xamarin.Android.Tools.Benchmarks' ">
    <Warning Code="BP0001" Text="MSBuild warning policy probe" />
  </Target>
</Project>
'@ | Set-Content -LiteralPath $targets -Encoding UTF8
            }
            $probesStarted = $true
            Invoke-PolicyBuild -Configuration $configuration -ExpectedCode $probe.Code `
                -ExtraArguments "-p:CustomAfterMicrosoftCommonTargets=$targets"
        }
    }
} finally {
    Remove-Item -LiteralPath $probeRoot -Recurse -Force
    if ($probesStarted) {
        foreach ($configuration in 'Debug', 'Release') {
            Invoke-PolicyBuild -Configuration $configuration
        }
    }
}
Write-Host 'Warning policy checks passed for Debug and Release.'
