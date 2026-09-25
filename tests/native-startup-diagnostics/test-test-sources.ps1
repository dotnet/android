#requires -Version 7.3
param (
    [switch] $CheckPayload,
    [string] $BuildTestsAssembly,
    [string] $RestoreDotNet
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not ($IsWindows -or $IsLinux)) { throw 'This pipeline configuration regression requires Windows or Linux.' }
if ([bool]$BuildTestsAssembly -ne [bool]$RestoreDotNet) { throw 'Supply both the normally built test assembly and existing exact 10.0.401 dotnet host.' }
function Assert ($Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$out = Join-Path $root ('bin/guest-test-sources/' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $out 'source with spaces'
$scripts = Join-Path $fixture 'build-tools/scripts'
New-Item -ItemType Directory $scripts -Force | Out-Null
Copy-Item "$root/build-tools/scripts/guest-readiness-test-sources.ps1" $scripts
Copy-Item "$root/NuGet.config" $fixture
$savedArgs = $env:NUNIT_MSBUILD_ARGS
$savedOptIn = $env:ANDROID_GUEST_READINESS_TEST_ACQUISITION
$savedConfig = $env:RESTORECONFIGFILE
$hostArguments = @{ AllowLinux = $IsLinux }
try {
    $env:NUNIT_MSBUILD_ARGS = ''
    $env:ANDROID_GUEST_READINESS_TEST_ACQUISITION = ''
    $env:RESTORECONFIGFILE = Join-Path $fixture 'NuGet.config'
    $config = $env:RESTORECONFIGFILE
    if ($IsLinux) {
        $rejected = $false
        try { & "$scripts/guest-readiness-test-sources.ps1" }
        catch { $rejected = $_.Exception.Message -ceq 'Diagnostic test acquisition requires Windows or explicitly enabled Linux.' }
        Assert $rejected 'Linux requires explicit setup opt-in'
    }
    $lines = @(& "$scripts/guest-readiness-test-sources.ps1" @hostArguments)
    Assert ($lines.Count -eq 2) 'Configure emits exactly the two scoped variables'
    Assert ($lines[0] -ceq "##vso[task.setvariable variable=NUNIT_MSBUILD_ARGS]/p:RestoreConfigFile=`"$config`"") 'Quoted space path is preserved'
    Assert ($lines[1] -ceq '##vso[task.setvariable variable=ANDROID_GUEST_READINESS_TEST_ACQUISITION]1') 'Exact opt-in emitted'
    $argsPrefix = '##vso[task.setvariable variable=NUNIT_MSBUILD_ARGS]'
    # Builder writes NUNIT_MSBUILD_ARGS verbatim into its existing response file.
    $lines[0].Substring($argsPrefix.Length) | Set-Content "$out/project.rsp"
    & dotnet msbuild "$PSScriptRoot/test-sources-restore-property.proj" "@$out/project.rsp" `
        /nologo /t:Verify "/p:ExpectedConfig=$config" "/p:ReceiptDirectory=$out" /p:Instance=parent `
        "/bl:$out/nested-restore-property.binlog" *> "$out/nested-restore-property.log"
    Assert ($LASTEXITCODE -eq 0) 'Actual MSBuild response-file parsing and nested property inheritance succeed'
    foreach ($instance in @('parent', 'child')) {
        Assert ((Get-Content "$out/$instance.txt" -Raw).Trim() -ceq $config) 'Both project instances retain the exact root config despite a local override'
    }
    # DotNetCLI inherits RESTORECONFIGFILE but does not consume Builder's response-file arguments.
    & dotnet msbuild "$PSScriptRoot/test-sources-restore-property.proj" `
        /nologo /t:Verify /p:VerifyInheritedConfig=true "/p:ExpectedConfig=$config" `
        "/p:ReceiptDirectory=$out" /p:Instance=environment-parent `
        "/bl:$out/inherited-restore-property.binlog" *> "$out/inherited-restore-property.log"
    Assert ($LASTEXITCODE -eq 0) 'A fresh child MSBuild inherits the root config without a response-file or explicit RestoreConfigFile argument'
    Assert ((Get-Content "$out/environment-parent.txt" -Raw).Trim() -ceq $config) 'Inherited root config retains spaces'
    $cases = @('prior-args', 'malformed-optin', 'relative-config', 'missing-config', 'different-config')
    if ($IsLinux) { $cases += 'case-mismatch' }
    foreach ($case in $cases) {
        $env:NUNIT_MSBUILD_ARGS = if ($case -eq 'prior-args') { '/p:Existing=preserve' } else { '' }
        $env:ANDROID_GUEST_READINESS_TEST_ACQUISITION = if ($case -eq 'malformed-optin') { 'true' } else { '' }
        $env:RESTORECONFIGFILE = switch ($case) {
            relative-config { 'NuGet.config' }
            missing-config { Join-Path $fixture 'missing.config' }
            different-config { Join-Path $root 'NuGet.config' }
            case-mismatch { Join-Path $fixture 'nuget.config' }
            default { $config }
        }
        $expected = switch ($case) {
            prior-args { 'Existing NUNIT_MSBUILD_ARGS must not be overwritten.' }
            malformed-optin { 'ANDROID_GUEST_READINESS_TEST_ACQUISITION must be absent or exactly 1.' }
            default { 'Diagnostic test acquisition requires the unchanged repository-root NuGet.config.' }
        }
        $rejected = $false
        $emitted = [Collections.Generic.List[object]]::new()
        $priorArgs = $env:NUNIT_MSBUILD_ARGS
        try { & "$scripts/guest-readiness-test-sources.ps1" @hostArguments | ForEach-Object { $emitted.Add($_) } }
        catch { $rejected = $_.Exception.Message -ceq $expected }
        Assert ($rejected -and $emitted.Count -eq 0) "Invalid $case fails before emitting any variables"
        Assert ($env:NUNIT_MSBUILD_ARGS -ceq $priorArgs) 'Rejected configuration leaves existing arguments untouched'
    }
    if ($BuildTestsAssembly) {
        $assembly = (Resolve-Path $BuildTestsAssembly).Path
        $restoreHost = (Resolve-Path $RestoreDotNet).Path
        $sdkRoot = Split-Path $restoreHost
        Assert (Test-Path (Join-Path $sdkRoot 'sdk/10.0.401/MSBuild.dll')) 'The existing exact held SDK must be available; never acquire one'
        $package = Join-Path $root '.packages/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg'
        Assert (Test-Path $package) 'A genuine existing package is required; never acquire or manufacture one'
        $packageHash = (Get-FileHash $package).Hash
        $testOut = Join-Path $out 'generated-tests'
        $env:ANDROID_GUEST_READINESS_TEST_ACQUISITION = ''
        $env:RESTORECONFIGFILE = ''
        & dotnet test $assembly --filter 'FullyQualifiedName~GuestReadinessSdkProjectTests' `
            --logger 'trx;LogFileName=projects.trx' --results-directory $testOut -- "NUnit.WorkDirectory=$testOut" *> "$out/project-tests.log"
        Assert ($LASTEXITCODE -eq 0) 'Actual owning-assembly project-generation tests pass'
        [xml]$trx = Get-Content "$testOut/projects.trx" -Raw
        Assert ($trx.TestRun.ResultSummary.Counters.passed -eq '6' -and $trx.TestRun.ResultSummary.Counters.total -eq '6') 'All six real project tests executed, not skipped'
        $generated = Join-Path $testOut 'guest readiness sdk projects & literal'
        $localFeed = Join-Path $generated 'local artifact feed'
        $sourceCopy = Join-Path $localFeed 'newtonsoft.json.13.0.3.nupkg'
        # Copy only to the project's declared source feed; NuGet must populate each new cache itself.
        Copy-Item $package $sourceCopy
        Assert ((Get-FileHash $sourceCopy).Hash -ceq $packageHash) 'The source package remains genuine and byte-identical'
        $emptyFeed = Join-Path $out 'empty root feed'
        New-Item -ItemType Directory $emptyFeed | Out-Null
        $offlineConfig = Join-Path $out 'offline root NuGet.config'
        $escapedFeed = [Security.SecurityElement]::Escape($emptyFeed)
        "<configuration><packageSources><clear/><add key=`"root`" value=`"$escapedFeed`"/></packageSources></configuration>" | Set-Content $offlineConfig
        $configHash = (Get-FileHash $offlineConfig).Hash
        '{"sdk":{"version":"10.0.401","rollForward":"disable","allowPrerelease":false}}' | Set-Content "$out/global.json"
        $savedEnvironment = @{}
        foreach ($name in @('NUGET_PACKAGES', 'DOTNET_ROOT', 'DOTNET_HOST_PATH', 'DOTNET_MULTILEVEL_LOOKUP', 'PATH')) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
        }
        $restores = @()
        try {
            $env:DOTNET_ROOT = $sdkRoot
            $env:DOTNET_HOST_PATH = $restoreHost
            $env:DOTNET_MULTILEVEL_LOOKUP = '0'
            $env:PATH = $sdkRoot + [IO.Path]::PathSeparator + $savedEnvironment.PATH
            foreach ($route in @('environment', 'explicit-property')) {
                foreach ($mode in @('ordinary', 'diagnostic')) {
                    $directory = Join-Path $out "$route-$mode"
                    Copy-Item (Join-Path $generated $mode) $directory -Recurse
                    $project = Join-Path $directory 'GuestReadinessSources.csproj'
                    $projectHash = (Get-FileHash $project).Hash
                    $cache = Join-Path $directory 'fresh cache'
                    Assert (-not (Test-Path $cache)) 'Each restore cache starts absent'
                    $env:NUGET_PACKAGES = $cache
                    $env:RESTORECONFIGFILE = if ($route -eq 'environment') { $offlineConfig } else { '' }
                    Push-Location $directory
                    try {
                        $version = & $restoreHost --version
                        Assert ($LASTEXITCODE -eq 0 -and $version -ceq '10.0.401') 'Each fresh child resolves exact 10.0.401'
                        & $restoreHost --info > "$directory/dotnet-info.txt"
                        Assert ($LASTEXITCODE -eq 0) 'Record actual SDK base path'
                        $arguments = @('msbuild', $project, '/t:Restore', '/nologo', '/v:normal',
                            '/p:ImportDirectoryBuildProps=false', '/p:ImportDirectoryBuildTargets=false', "/bl:$directory/restore.binlog")
                        if ($route -eq 'explicit-property') { $arguments += "/p:RestoreConfigFile=$offlineConfig" }
                        & $restoreHost @arguments *> "$directory/restore.log"
                        $code = $LASTEXITCODE
                    } finally { Pop-Location }
                    $positive = $mode -eq 'diagnostic'
                    Assert (($positive -and $code -eq 0) -or (-not $positive -and $code -ne 0)) "Expected restore outcome for $route/$mode"
                    if (-not $positive) {
                        Assert ((Get-Content "$directory/restore.log" -Raw) -match 'error NU1101: Unable to find package Newtonsoft.Json') 'Ordinary negative fails at the hidden local source, not a different boundary'
                    }
                    $spec = Get-Content "$directory/obj/GuestReadinessSources.csproj.nuget.dgspec.json" -Raw | ConvertFrom-Json -AsHashtable
                    $restore = @($spec.projects.Values)[0].restore
                    Assert ($restore.configFilePaths.Count -eq 1 -and $restore.configFilePaths[0] -ceq $offlineConfig) 'Only the unchanged explicit root config is effective'
                    $expectedSources = @($emptyFeed)
                    if ($positive) { $expectedSources += $localFeed }
                    Assert (@(Compare-Object $expectedSources @($restore.sources.Keys) -CaseSensitive).Count -eq 0) 'Only root plus the same single project-local feed are effective'
                    Assert ($restore.restoreAuditProperties.enableAudit -ceq 'true' -and
                        $restore.restoreAuditProperties.auditLevel -ceq 'low' -and
                        $restore.restoreAuditProperties.auditMode -ceq 'all') 'Audit remains true/low/all'
                    Assert ((Get-FileHash $project).Hash -ceq $projectHash) 'Restore does not rewrite the genuinely generated project'
                    if ($positive) {
                        Assert ((Get-FileHash (Join-Path $cache 'newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg')).Hash -ceq $packageHash) 'NuGet restores the exact genuine package to its fresh cache'
                    }
                    $restores += [ordered]@{
                        route = $route; mode = $mode; sdkVersion = $version; sdkInfo = "$directory/dotnet-info.txt"
                        exitCode = $code; arguments = $arguments; projectSha256 = $projectHash
                        configFilePaths = $restore.configFilePaths; packageSources = @($restore.sources.Keys)
                        audit = $restore.restoreAuditProperties
                        serializedAuditSources = $restore['auditSources']; fallbackFolders = $restore['fallbackFolders']
                        auditSourcesEvidence = 'Fixture config has no explicit auditSources; package-source fallback is the default semantics, not an observed vulnerability-data retrieval.'
                    }
                }
            }
            Assert ((Get-FileHash $offlineConfig).Hash -ceq $configHash -and (Get-FileHash $package).Hash -ceq $packageHash) 'Original config and package are unchanged'
            [ordered]@{
                host = if ($IsWindows) { 'Windows' } else { 'Linux' }
                projectKind = 'ProjectTools-generated plain net10.0 library with inert Android item metadata; no Android workload targets imported or executed.'
                sourcePathCase = 'Literal ampersand and spaces; decoded project XML and effective restore sources retain the original path.'
                testAssembly = $assembly; testAssemblySha256 = (Get-FileHash $assembly).Hash
                packageSha256 = $packageHash; cases = $restores
                limitations = 'Offline plain-.NET restore mechanism only, not hosted Linux 10.0.301, full Android restore/native workload success, or vulnerability-data qualification.'
            } | ConvertTo-Json -Depth 10 | Set-Content "$out/generated-restore-receipt.json"
        } finally {
            foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
        }
    }
    if ($CheckPayload) {
        Add-Type -Path "$root/src/Xamarin.Android.Build.Tasks/Tests/Xamarin.ProjectTools/Common/GuestReadinessTestSources.cs",
            "$root/src/Xamarin.Android.Build.Tasks/Tests/Xamarin.ProjectTools/Common/DownloadedCache.cs"
        $url = 'https://repo1.maven.org/maven2/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar'
        $downloads = @()
        foreach ($mode in @('ordinary', 'diagnostic')) {
            $env:ANDROID_GUEST_READINESS_TEST_ACQUISITION = if ($mode -eq 'diagnostic') { '1' } else { '' }
            $cache = [Xamarin.ProjectTools.DownloadedCache]::new((Join-Path $out $mode))
            $path = $cache.GetAsFile($url)
            $firstHash = (Get-FileHash $path).Hash.ToLowerInvariant()
            Assert ($cache.GetAsFile($url) -ceq $path) 'Existing real cache entry retains ordinary semantics'
            Assert ((Get-FileHash $path).Hash.ToLowerInvariant() -ceq $firstHash) 'Cache reuse preserves bytes'
            $downloads += [pscustomobject]@{ mode = $mode; path = $path; sha256 = $firstHash; sizeBytes = (Get-Item $path).Length }
        }
        Assert ($downloads[0].sha256 -ceq $downloads[1].sha256) 'Approved mirror serves exact original material-menu payload'
        $downloads | ConvertTo-Json | Set-Content "$out/payload-receipt.json"
    }
    Write-Output "PASS: actual configuration script, space paths, malformed config/opt-in and existing args. Generated-project restore requested: $([bool]$BuildTestsAssembly). Payload requested: $CheckPayload. Evidence: $out"
} finally {
    $env:NUNIT_MSBUILD_ARGS = $savedArgs
    $env:ANDROID_GUEST_READINESS_TEST_ACQUISITION = $savedOptIn
    $env:RESTORECONFIGFILE = $savedConfig
}
