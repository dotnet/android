#requires -Version 7.3
param (
    [Parameter(Mandatory)][string] $GuardianTargets,
    [string] $GuardianCli
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'This regression requires Windows.' }
function Assert ($Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$out = Join-Path $root ("bin/guest-readiness-roslyn-tests/" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$out/root/nested", "$out/results" -Force | Out-Null
$isolatedRoot = Join-Path $out 'source'
$scripts = Join-Path $isolatedRoot 'build-tools/scripts'
New-Item -ItemType Directory $scripts -Force | Out-Null
foreach ($file in @('guest-readiness-roslyn.ps1', 'GuestReadinessRoslynOutput.targets', 'guest-readiness-runtime-pack.psm1')) {
    Copy-Item "$root/build-tools/scripts/$file" $scripts
}
$captureScript = Join-Path $scripts 'guest-readiness-roslyn.ps1'
# These scopes share a project name, TFM and configuration; only compilation instance differs.
$project = Join-Path $out 'root/fixture.proj'
$nested = Join-Path $out 'root/nested/fixture.proj'
Copy-Item "$PSScriptRoot/roslyn-output-fixture.proj" $project
Copy-Item "$PSScriptRoot/roslyn-output-fixture.proj" $nested
$source = Join-Path $out 'Warning.cs'
'#warning retained-diagnostic-warning
public class DiagnosticFixture {
    public byte[] WeakHash(byte[] data) {
        using (var hash = System.Security.Cryptography.SHA1.Create()) { return hash.ComputeHash(data); }
    }
}' | Set-Content $source
# A real SDK security analyzer finding exercises the unchanged Guardian filter.
'<RuleSet Name="Security finding fixture" ToolsVersion="17.0">
  <Rules AnalyzerId="Microsoft.CodeAnalysis.NetAnalyzers" RuleNamespace="Microsoft.CodeAnalysis.NetAnalyzers">
    <Rule Id="CA5350" Action="Warning" />
  </Rules>
</RuleSet>' | Set-Content "$source.ruleset"
$sdk = (& dotnet --list-sdks | Select-Object -Last 1).Split(' ')[0]
@{ sdk = @{ version = $sdk } } | ConvertTo-Json | Set-Content "$out/global.json"
$reference = Get-ChildItem "$env:ProgramFiles/dotnet/packs/Microsoft.NETCore.App.Ref/*/ref/net10.0/System.Runtime.dll" |
    Select-Object -First 1 -ExpandProperty FullName
Assert ($null -ne $reference) 'An existing .NET 10 reference pack is required'
$savedHook = $env:CustomAfterMicrosoftCommonTargets
try {
    $env:CustomAfterMicrosoftCommonTargets = ''
    $configure = & $captureScript -Phase Configure
    $prefix = '##vso[task.setvariable variable=CustomAfterMicrosoftCommonTargets]'
    Assert ($configure.StartsWith($prefix)) 'Configure uses the supported job variable'
    $env:CustomAfterMicrosoftCommonTargets = $configure.Substring($prefix.Length)
    $rejected = $false
    try { & $captureScript -Phase Configure }
    catch { $rejected = $_.Exception.Message -ceq 'An existing CustomAfterMicrosoftCommonTargets must not be overwritten.' }
    Assert $rejected 'Existing hook is rejected rather than overwritten'
    $arguments = @($project, '/nologo', '/m:3', '/t:Concurrent', '/v:minimal',
        "/p:GuardianTargets=$([IO.Path]::GetFullPath($GuardianTargets))",
        "/p:FixtureSource=$source", "/p:FixtureReference=$reference", "/p:FixtureOutput=$out/results",
        "/p:NestedProject=$nested", "/bl:$out/concurrent.binlog")
    & dotnet msbuild @arguments *> "$out/concurrent.log"
    Assert ($LASTEXITCODE -eq 0) "Actual concurrent Csc failed; see $out/concurrent.log"
    $logs = @()
    foreach ($instance in @('one', 'two', 'nested')) {
        $result = Get-Content "$out/results/$instance/result.txt"
        $path = $result[0].Split(',')[0]
        Assert ($path -cmatch '\.csproj\.[0-9a-f]{32}\.gdn\.sarif$') 'Unique leaf preserves Guardian collection wildcard'
        Assert ($result -contains 'true') 'Guardian analyzer enablement remains intact'
        Assert ($result[-1] -ceq $env:CustomAfterMicrosoftCommonTargets) 'Root and nested scopes import the same hook'
        $sarif = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        Assert ($sarif.version -ceq '1.0.0') 'Original Guardian SARIF format is preserved'
        $warnings = @($sarif.runs[0].results | Where-Object { $_.ruleId -ceq 'CS1030' -and $_.message.Contains('retained-diagnostic-warning') })
        Assert ($warnings.Count -eq 1) 'Each actual Csc warning survives independently'
        Assert (@($sarif.runs[0].results | Where-Object ruleId -CEQ 'CA5350').Count -eq 1) 'Each compilation emits the real SDK security-analyzer finding'
        $logs += $path
    }
    Assert (@($logs | Sort-Object -Unique).Count -eq 3) 'Same-TFM/config/root/nested invocations never share a log'
    & dotnet msbuild $project /nologo /t:_FilterRestoreGraphProjectInputItems /v:q `
        "/p:GuardianTargets=$([IO.Path]::GetFullPath($GuardianTargets))" `
        /p:RestoreUseCustomAfterTargets=true "/p:FixtureRestoreReceipt=$out/restore-hook.txt" `
        "/bl:$out/restore-hook.binlog" *> "$out/restore-hook.log"
    Assert ($LASTEXITCODE -eq 0 -and (Test-Path "$out/restore-hook.txt")) 'Real NuGet restore traversal must invoke the override assertion'
    $restoreHook = Get-Content "$out/restore-hook.txt"
    Assert ($restoreHook[0].EndsWith('\NuGet.targets') -and $restoreHook[1] -ceq 'true') 'NuGet task-level hook and import exclusion remain authoritative'
    $sanitizerResult = 'not-run'
    $sanitizerMappings = @()
    if (-not [string]::IsNullOrEmpty($GuardianCli)) {
        $rawHashes = @{}
        foreach ($path in $logs) { $rawHashes[$path] = (Get-FileHash $path).Hash.ToLowerInvariant() }
        & $GuardianCli --copy-logs-only --sources-directory $isolatedRoot `
            --log-root-directory $isolatedRoot --output-directory "$out/sanitized" *> "$out/sanitizer.log"
        Assert ($LASTEXITCODE -eq 0) 'Actual Guardian copy/sanitize command failed'
        $sanitized = @(Get-ChildItem "$out/sanitized" -Recurse -File -Filter '*.sarif')
        Assert ($sanitized.Count -eq 3) 'The real collector must retain all three independently named logs'
        foreach ($file in $sanitized) {
            $sarif = Get-Content $file.FullName -Raw | ConvertFrom-Json
            Assert ($sarif.version -ceq '2.1.0') 'The actual sanitizer must finish converting every log'
            $warnings = @($sarif.runs[0].results | Where-Object {
                $_.ruleId -ceq 'CA5350'
            })
            Assert ($warnings.Count -eq 1) 'Every real security-analyzer finding must survive actual Guardian sanitization'
            $original = @($logs | Where-Object { [IO.Path]::GetFileName($_) -ceq $file.Name })
            Assert ($original.Count -eq 1) 'Each sanitized GUID leaf maps to exactly one original compilation'
            Assert ((Get-FileHash $original[0]).Hash.ToLowerInvariant() -ceq $rawHashes[$original[0]]) 'The collector leaves original source-root/bin logs unchanged'
            $raw = Get-Content $original[0] -Raw | ConvertFrom-Json
            $finding = @($raw.runs[0].results | Where-Object ruleId -CEQ 'CA5350')[0]
            $beforeLocation = $finding.locations[0].resultFile
            $afterLocation = $warnings[0].locations[0].physicalLocation
            Assert ($finding.message -ceq $warnings[0].message.text) 'The security finding message survives'
            Assert ($beforeLocation.uri -ceq $afterLocation.artifactLocation.uri) 'The security finding source URI survives'
            foreach ($field in @('startLine', 'startColumn', 'endLine', 'endColumn')) {
                Assert ($beforeLocation.region.$field -eq $afterLocation.region.$field) "The security finding $field survives"
            }
            Assert (@($sarif.runs[0].results | Where-Object ruleId -CEQ 'CS1030').Count -eq 0) 'Expected ordinary-compiler-warning filtering is preserved'
            $sanitizerMappings += [pscustomobject]@{
                input = $original[0]; output = $file.FullName; ruleId = 'CA5350'
                inputSha256 = (Get-FileHash $original[0]).Hash.ToLowerInvariant()
                outputSha256 = (Get-FileHash $file.FullName).Hash.ToLowerInvariant()
                sourceUri = $afterLocation.artifactLocation.uri; region = $afterLocation.region
            }
        }
        Assert (@($sanitizerMappings.input | Sort-Object -Unique).Count -eq 3) 'Every input log has a unique sanitized output'
        $sanitizerResult = (Get-FileHash $GuardianCli).Hash.ToLowerInvariant()
    }
    $explicit = Join-Path $out 'explicit.csproj.user.sarif'
    & dotnet msbuild $project /nologo /t:CoreCompile /v:q "/p:GuardianTargets=$([IO.Path]::GetFullPath($GuardianTargets))" `
        "/p:FixtureSource=$source" "/p:FixtureReference=$reference" "/p:FixtureOutput=$out/results" `
        /p:Instance=explicit "/p:ErrorLog=$explicit" *> "$out/explicit.log"
    Assert ($LASTEXITCODE -eq 0 -and (Test-Path $explicit)) 'Explicit ErrorLog remains authoritative'
    $env:CustomAfterMicrosoftCommonTargets = ''
    & dotnet msbuild $project /nologo /t:CoreCompile /v:q "/p:GuardianTargets=$([IO.Path]::GetFullPath($GuardianTargets))" `
        "/p:FixtureSource=$source" "/p:FixtureReference=$reference" "/p:FixtureOutput=$out/results" `
        /p:Instance=off *> "$out/off.log"
    Assert ($LASTEXITCODE -eq 0) 'OFF compilation succeeds'
    Assert ((Get-Content "$out/results/off/result.txt")[0] -ceq 'fixture.csproj.gdn.sarif,version=1.0') 'OFF retains original Guardian default'
    & $captureScript -Phase Capture -Destination "$out/capture-absent"
    $absent = Get-Content "$out/capture-absent/capture.json" -Raw | ConvertFrom-Json
    Assert (-not $absent.rootSarifPresent -and $absent.files.Count -eq 3) 'Capture records exact root absence and all owned logs'
    $rootSarif = Join-Path $isolatedRoot '.sarif'
    [byte[]] $unknownBytes = @(0, 255, 1, 10, 13, 42)
    [IO.File]::WriteAllBytes($rootSarif, $unknownBytes)
    $unknownHash = (Get-FileHash $rootSarif).Hash.ToLowerInvariant()
    & $captureScript -Phase Capture -Destination "$out/capture-present"
    $present = Get-Content "$out/capture-present/capture.json" -Raw | ConvertFrom-Json
    Assert ($present.rootSarifPresent -and $present.files.Count -eq 4) 'Unknown bare root bytes are retained without asserting valid SARIF'
    Assert ($present.notice.Contains('No scanner processing or policy success is asserted.')) 'Capture makes no processing claim'
    foreach ($record in $present.files) {
        Assert ((Get-FileHash $record.sourcePath).Hash.ToLowerInvariant() -ceq $record.sha256) 'Every source remains untouched'
        Assert ((Get-FileHash "$out/capture-present/$($record.fileName)").Hash.ToLowerInvariant() -ceq $record.sha256) 'Every copied byte is hash-linked'
        Assert ((Get-Item $record.sourcePath).Length -eq $record.sizeBytes) 'Captured size is exact'
        Assert (-not [string]::IsNullOrEmpty($record.lastWriteTimeUtc)) 'Source write time is retained'
    }
    Assert ((Get-FileHash "$out/capture-present/unexplained-root.sarif").Hash.ToLowerInvariant() -ceq $unknownHash) 'Exact unexplained bytes retained'
    foreach ($case in @('destination-parent-junction', 'source-parent-junction')) {
        $caseRoot = Join-Path $out $case
        $caseScripts = Join-Path $caseRoot 'build-tools/scripts'
        New-Item -ItemType Directory $caseScripts -Force | Out-Null
        Copy-Item "$scripts/*" $caseScripts
        $destination = Join-Path $out "rejected-$case"
        if ($case -eq 'destination-parent-junction') {
            $junction = Join-Path $caseRoot 'redirect'
            New-Item -ItemType Junction -Path $junction -Target $caseScripts | Out-Null
            $destination = Join-Path $junction 'not-created/capture'
            $sourceParent = Join-Path $caseRoot 'bin'
        } else {
            $sourceParent = Join-Path $out 'source-parent-target'
            New-Item -ItemType Directory $sourceParent | Out-Null
            New-Item -ItemType Junction -Path "$caseRoot/bin" -Target $sourceParent | Out-Null
        }
        $caseSource = Join-Path $sourceParent 'guest-readiness-roslyn'
        New-Item -ItemType Directory $caseSource -Force | Out-Null
        $leaf = Join-Path $caseSource 'fixture.csproj.00000000000000000000000000000000.gdn.sarif'
        'untouched-evidence' | Set-Content $leaf
        $before = @{}
        foreach ($file in @(Get-ChildItem $caseScripts -File) + @(Get-Item $leaf)) {
            $before[$file.FullName] = (Get-FileHash $file.FullName).Hash
        }
        $rejected = $false
        try { & "$caseScripts/guest-readiness-roslyn.ps1" -Phase Capture -Destination $destination }
        catch { $rejected = $_.Exception.Message -ceq 'Roslyn evidence paths must not traverse reparse points.' }
        Assert $rejected "Capture must reject $case before any writes"
        Assert (-not (Test-Path $destination)) 'Rejected ancestor cannot create the destination or any copied evidence'
        Assert (-not (Test-Path "$caseScripts/not-created")) 'A destination junction cannot create even the first missing source-tree directory'
        Assert (@(Get-ChildItem $caseScripts -Force).Count -eq 3) 'The script directory remains unchanged'
        Assert (@(Get-ChildItem $caseSource -Force).Count -eq 1) 'The original source directory remains unchanged'
        foreach ($path in $before.Keys) {
            Assert ((Get-FileHash $path).Hash -ceq $before[$path]) 'Original source and script bytes remain unchanged'
        }
    }
    foreach ($case in @('unrelated', 'directory', 'reparse', 'oversize', 'file-count', 'active-writer', 'existing-destination', 'inside-source')) {
        $caseRoot = Join-Path $out $case
        $caseScripts = Join-Path $caseRoot 'build-tools/scripts'
        $caseSource = Join-Path $caseRoot 'bin/guest-readiness-roslyn'
        New-Item -ItemType Directory $caseScripts, $caseSource -Force | Out-Null
        Copy-Item "$scripts/*" $caseScripts
        $leaf = Join-Path $caseSource 'fixture.csproj.00000000000000000000000000000000.gdn.sarif'
        $destination = "$out/rejected-$case"
        $expected = 'Unexpected or oversized Roslyn evidence file.'
        $writer = $null
        switch ($case) {
            unrelated { 'unrelated' | Set-Content "$caseSource/unrelated.sarif" }
            directory { New-Item -ItemType Directory $leaf | Out-Null }
            reparse { New-Item -ItemType Junction -Path $leaf -Target $scripts | Out-Null }
            oversize {
                $stream = [IO.File]::Create($leaf)
                try { $stream.SetLength(67108865) } finally { $stream.Dispose() }
            }
            file-count {
                for ($i = 0; $i -le 10000; $i++) {
                    [IO.File]::Create((Join-Path $caseSource "fixture.csproj.$($i.ToString('x32')).gdn.sarif")).Dispose()
                }
                $expected = 'Roslyn evidence exceeds the file-count bound.'
            }
            active-writer {
                $writer = [IO.File]::Open($leaf, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
            }
            existing-destination {
                New-Item -ItemType Directory $destination | Out-Null
                $expected = 'Roslyn evidence destination must be new and outside its source directory.'
            }
            inside-source {
                $destination = Join-Path $caseSource 'copy'
                $expected = 'Roslyn evidence destination must be new and outside its source directory.'
            }
        }
        $rejected = $false
        try { & "$caseScripts/guest-readiness-roslyn.ps1" -Phase Capture -Destination $destination }
        catch {
            $rejected = if ($case -eq 'active-writer') { $_.Exception.InnerException -is [IO.IOException] }
                else { $_.Exception.Message -ceq $expected }
        } finally {
            if ($null -ne $writer) { $writer.Dispose() }
        }
        Assert $rejected "Capture must reject $case"
        Assert (-not (Test-Path "$destination/capture.json")) 'Rejected capture cannot produce a success receipt'
    }
    [pscustomobject]@{
        kind = 'real-concurrent-csc-output-regression'; output = $out; logs = $logs
        guardianTargetsSha256 = (Get-FileHash $GuardianTargets).Hash.ToLowerInvariant()
        guardianCliSha256 = $sanitizerResult
        sanitizerMappings = $sanitizerMappings
        notice = 'Real Csc tasks and retained actual injector target. Optional real collector result recorded separately; not a full product build or security-policy qualification.'
    } | ConvertTo-Json -Depth 8 | Set-Content "$out/receipt.json"
    Write-Output "PASS: real concurrent Csc, NuGet hook precedence, explicit/OFF controls, bounded capture. Guardian CLI: $sanitizerResult. Evidence: $out"
} finally {
    $env:CustomAfterMicrosoftCommonTargets = $savedHook
}
exit 0
