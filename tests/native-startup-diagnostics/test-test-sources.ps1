#requires -Version 7.3
param ([switch] $CheckPayload)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not ($IsWindows -or $IsLinux)) { throw 'This pipeline configuration regression requires Windows or Linux.' }
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
    Write-Output "PASS: actual configuration script, space paths, malformed config/opt-in and existing args. Payload requested: $CheckPayload. Evidence: $out"
} finally {
    $env:NUNIT_MSBUILD_ARGS = $savedArgs
    $env:ANDROID_GUEST_READINESS_TEST_ACQUISITION = $savedOptIn
    $env:RESTORECONFIGFILE = $savedConfig
}
