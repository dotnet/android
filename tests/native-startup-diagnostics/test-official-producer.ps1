param ([string] $MSBuild = 'C:\Program Files\Microsoft Visual Studio\18\IntPreview\MSBuild\Current\Bin\MSBuild.exe')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$out = Join-Path $root 'bin\guest-readiness-official-tests'
New-Item -ItemType Directory -Force $out | Out-Null
& (Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })) -NoProfile -File "$PSScriptRoot\test-runtime-pack-producer.ps1"
if ($LASTEXITCODE -ne 0) { throw "Runtime pack fixture preparation failed with exit code $LASTEXITCODE." }
Import-Module "$root\build-tools\scripts\guest-readiness-runtime-pack.psm1" -Force
function Assert([bool] $Value, [string] $Message) { if (-not $Value) { throw $Message } }
function Reject([scriptblock] $Action, [string] $Message) {
    try { & $Action | Out-Null } catch {
        Assert ($_.Exception.Message -ceq $Message) "Wrong failure: $($_.Exception.Message)"
        return
    }
    throw "Did not reject: $Message"
}
$identity = Get-GuestRuntimePackIdentity 12345 1
$environment = @{
    BUILD_BUILDID = '12345'; GUEST_ATTEMPT = '1'; BUILD_REASON = 'Manual'; BUILD_DEFINITIONNAME = 'Xamarin.Android'
    SYSTEM_DEFINITIONID = '11410'; BUILD_REPOSITORY_NAME = 'dotnet/android'; BUILD_SOURCEBRANCH = 'refs/heads/diagnostic-fixture'
    GUEST_SKIP_COMPLIANCE = 'false'; GUEST_CONFIGURATION = 'Debug'; SYSTEM_JOBATTEMPT = '1'; SYSTEM_JOBNAME = 'sign_net_mac_win'
    BUILD_SOURCEVERSION = (& git -C $root rev-parse HEAD)
    GUEST_YAML_VERSION = ('1' * 40); GUEST_1ES_VERSION = ('2' * 40)
}
foreach ($entry in $environment.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value) }
& "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Validate
Assert ($LASTEXITCODE -eq 0) 'Valid manual source gate'
foreach ($entry in @{
    BUILD_REASON = 'PullRequest'; BUILD_DEFINITIONNAME = 'Other'; SYSTEM_DEFINITIONID = '1'
    BUILD_REPOSITORY_NAME = 'davidnguyen-tech/android'; BUILD_SOURCEBRANCH = 'refs/heads/release/10.0'; GUEST_SKIP_COMPLIANCE = 'true'
}.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
    Reject { & "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Validate } `
        'Guest readiness requires manual definition11410, upstream branch, Test signing and unchanged compliance.'
    [Environment]::SetEnvironmentVariable($entry.Key, $environment[$entry.Key])
}
$env:BUILD_SOURCEVERSION = ('a' * 40) + "`n"
Reject { & "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Validate } 'Exact source and resolved template commits are required.'
$env:BUILD_SOURCEVERSION = $environment.BUILD_SOURCEVERSION
$env:SYSTEM_JOBATTEMPT = '2147483648'
Reject { & "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Validate } 'Invalid provider numeric identity.'
$env:SYSTEM_JOBATTEMPT = '1'

$properties = @('/p:AndroidGuestReadinessBuild=true', "/p:AndroidPackVersionLong=$($identity.version)",
    "/p:PackageVersion=$($identity.version)", "/p:AndroidStartupDiagnosticsBuildId=$($identity.buildId)")
$project = "$PSScriptRoot\official-pack-properties.proj"
$result = Join-Path $out 'nested-properties.txt'
& $MSBuild $project /nologo /v:q /p:Configuration=Debug "/p:ResultPath=$result" @properties
Assert ($LASTEXITCODE -eq 0) 'Diagnostic nested MSBuild'
Assert ((Get-Content $result -Raw).Trim() -ceq "true|$($identity.version)|$($identity.version)|$($identity.buildId)|true|Debug") 'Every nested property forwarded'
& $MSBuild $project /nologo /v:q /p:Configuration=Debug "/p:ResultPath=$result"
Assert ($LASTEXITCODE -eq 0 -and (Get-Content $result -Raw).Trim() -ceq '|||||Debug') 'Default-off nested MSBuild'
foreach ($case in @(
    @{ argument = '/p:PackageVersion=36.1.69'; error = 'Guest readiness package versions must agree.' },
    @{ argument = '/p:AndroidStartupDiagnosticsBuildId=android-d549-12345-2'; error = 'Guest readiness version and marker must bind the same build and attempt.' },
    @{ argument = '/p:AndroidStartupDiagnosticsBuildId=android-d549-12345-1%0A'; error = 'Guest readiness requires its validated producer marker.' }
)) {
    $lines = & $MSBuild $project /nologo /v:q /p:Configuration=Debug "/p:ResultPath=$result" @properties $case.argument 2>&1
    Assert ($LASTEXITCODE -ne 0 -and ($lines -join "`n").Contains($case.error)) 'Precise property rejection'
}

# Actual ZIP bytes containing deliberately synthetic ELF headers, not executable Android evidence.
function Native-Fixture([string] $Name, [string] $Rid = 'android-arm64', [string] $Fault = '') {
    $path = Join-Path $out "$Name.nupkg"
    $source = [IO.Compression.ZipFile]::OpenRead("$root\bin\guest-runtime-pack-fixtures\valid.nupkg")
    $stream = [IO.File]::Create($path)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $source.Entries) {
            $name = $entry.FullName.Replace('android-arm64', $Rid)
            $inputStream = $entry.Open()
            $memory = [IO.MemoryStream]::new()
            try { $inputStream.CopyTo($memory); $bytes = $memory.ToArray() } finally { $inputStream.Dispose(); $memory.Dispose() }
            if ($name -match 'libmono-android\.(debug|release)\.so\z') {
                $bytes = [byte[]]::new(128)
                [byte[]] @(127, 69, 76, 70, 2, 1, 1) | ForEach-Object -Begin { $i = 0 } -Process { $bytes[$i++] = $_ }
                $bytes[16] = 3; $bytes[18] = $(if ($Rid -eq 'android-arm64') { 183 } else { 62 })
                [Text.Encoding]::ASCII.GetBytes($identity.buildId).CopyTo($bytes, 64)
                switch ($Fault) {
                    'boundary' {
                        $bytes = [byte[]] ($bytes + [byte[]]::new(65536))
                        [Array]::Clear($bytes, 64, $identity.buildId.Length)
                        [Text.Encoding]::ASCII.GetBytes($identity.buildId).CopyTo($bytes, 65590)
                    }
                    'machine' { $bytes[18] = 1 }
                    'marker' { $bytes[64] = 0 }
                    'class' { $bytes[4] = 1 }
                    'endian' { $bytes[5] = 2 }
                    'magic' { $bytes[0] = 0 }
                }
            } elseif ($name.EndsWith('.nuspec')) {
                $bytes = [Text.Encoding]::UTF8.GetBytes([Text.Encoding]::UTF8.GetString($bytes).Replace('android-arm64', $Rid))
            }
            $destination = $zip.CreateEntry($name).Open()
            try { $destination.Write($bytes) } finally { $destination.Dispose() }
        }
        if ($Fault -eq 'signature') {
            $destination = $zip.CreateEntry('.signature.p7s').Open()
            try { $destination.Write([byte[]] @(1, 2, 3)) } finally { $destination.Dispose() }
        }
        $destination = $zip.CreateEntry("data/caf$([char]0xe9).txt").Open()
        try { $destination.Write([Text.Encoding]::UTF8.GetBytes('synthetic non-ASCII member name')) } finally { $destination.Dispose() }
    } finally { $zip.Dispose(); $stream.Dispose(); $source.Dispose() }
    return $path
}
foreach ($rid in @('android-arm64', 'android-x64')) {
    $file = Native-Fixture "native-$rid" $rid
    $inventory = Read-GuestRuntimePackInventory $file $rid $identity.version
    $carriers = @(Read-GuestRuntimePackNativeCarriers $file $inventory $identity.buildId)
    Assert ($carriers.Count -eq 2 -and $carriers[0].elfClass -eq 2 -and $carriers[0].endianness -eq 1) 'Typed actual member metadata'
    [pscustomobject]@{
        schemaVersion = 1; kind = 'guest-runtime-native-provenance'; id = $inventory.id; rid = $rid; version = $identity.version
        packageSha256 = $inventory.sha256; inventory = [pscustomobject]@{ fileName = "synthetic-inventory-$rid.json"; sha256 = ('0' * 64) }
        sourceCommit = $environment.BUILD_SOURCEVERSION; patchSha256 = ('0' * 64); buildMarker = $identity.buildId
        configuration = [pscustomobject]@{ msbuildProperty = 'AndroidStartupDiagnosticsBuildId'; cmakeVariable = 'XA_STARTUP_DIAGNOSTICS_BUILD_ID'
            value = $identity.buildId; evidence = @([pscustomobject]@{ fileName = 'synthetic-CMakeCache.txt'; sha256 = ('0' * 64) }) }
        carriers = $carriers
    } | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $out "synthetic-native-$rid.json") -Encoding utf8
}
$boundary = Native-Fixture 'native-boundary' 'android-arm64' 'boundary'
$inventory = Read-GuestRuntimePackInventory $boundary 'android-arm64' $identity.version
Assert (@(Read-GuestRuntimePackNativeCarriers $boundary $inventory $identity.buildId).Count -eq 2) 'Marker spans streaming buffer boundary'
foreach ($fault in @('machine', 'class', 'endian', 'magic', 'marker')) {
    $file = Native-Fixture "native-$fault" 'android-arm64' $fault
    $inventory = Read-GuestRuntimePackInventory $file 'android-arm64' $identity.version
    $message = if ($fault -eq 'marker') { 'Native carrier lacks the compiled build marker.' } else { 'Native carrier is not the expected ELF64 little-endian shared object.' }
    Reject { Read-GuestRuntimePackNativeCarriers $file $inventory $identity.buildId } $message
}
$beforeFile = Native-Fixture 'native-before'
$afterFile = Native-Fixture 'native-after' 'android-arm64' 'signature'
$before = Read-GuestRuntimePackInventory $beforeFile 'android-arm64' $identity.version
$after = Read-GuestRuntimePackInventory $afterFile 'android-arm64' $identity.version
$delta = @(Get-GuestRuntimePackMemberDelta $before $after)
Assert ($delta.Count -eq 1 -and $delta[0].change -ceq 'added' -and $delta[0].path -ceq '.signature.p7s') 'Real ZIP added-member delta'
$reverse = @(Get-GuestRuntimePackMemberDelta $after $before)
Assert ($reverse.Count -eq 1 -and $reverse[0].change -ceq 'removed') 'Real ZIP removed-member delta'
$changedFile = Native-Fixture 'native-changed' 'android-arm64' 'marker'
$changed = Read-GuestRuntimePackInventory $changedFile 'android-arm64' $identity.version
Assert (@(Get-GuestRuntimePackMemberDelta $before $changed).Count -eq 2) 'Real ZIP changed-member delta'
$reference = [pscustomobject]@{ fileName = 'synthetic-reference.json'; sha256 = ('0' * 64) }
$parameters = @{
    InputInventory = $before; OutputInventory = $after
    Source = [pscustomobject]@{ repository = 'dotnet/android'; baselineCommit = 'd549e1dc4e2a083b08b4f24cb5495e81b99d79b5'; sourceCommit = $environment.BUILD_SOURCEVERSION; patchSha256 = ('0' * 64) }
    BuildReceipt = $reference; InputReference = $reference; OutputReference = $reference; SignatureReference = $reference
    Signer = [pscustomobject]@{ organization = 'devdiv'; project = 'DevDiv'; definitionId = 11410; buildId = 12345; jobName = 'sign_net_mac_win'; jobAttempt = 1
        pipelinePath = 'build-tools/automation/azure-pipelines.yaml'; pipelineCommit = $environment.BUILD_SOURCEVERSION; requestedSignType = 'Test'
        templates = @([pscustomobject]@{ repository = 'DevDiv/Xamarin.yaml-templates'; commit = ('1' * 40); path = 'sign-artifacts/jobs/v4.yml'; sha256 = ('0' * 64) }) }
    OperationEvidence = @(
        [pscustomobject]@{ role = 'sign'; evidenceType = 'build-binlog'; reference = $reference },
        [pscustomobject]@{ role = 'verify'; evidenceType = 'command-receipt'; reference = $reference })
    MemberDelta = $reference; NativeProvenance = $reference
}
foreach ($exitCode in @(0, 1)) {
    $log = Join-Path $out "synthetic-verifier-$exitCode.log"
    'Synthetic status-constructor input, not a verifier invocation.' | Set-Content $log
    $parameters.Signature = New-GuestRuntimePackSignature $after $exitCode '10.0.100' $log
    $postsign = New-GuestRuntimePackagePostSign @parameters
    $expected = if ($exitCode -eq 0) { 'verified-policy-unqualified' } else { 'produced-verification-failed' }
    Assert ($postsign.status -ceq $expected) 'Typed verification outcome'
    $postsign | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $out "synthetic-postsign-$exitCode.json") -Encoding utf8
}

# Exercise the real pre-sign collector's filename/identity/binary binding; no build or signer is mocked as successful.
$caseRoot = Join-Path $out ("input-case-" + [Guid]::NewGuid().ToString('N'))
$buildDirectory = Join-Path $caseRoot 'build'
New-Item -ItemType Directory -Force $buildDirectory | Out-Null
$patch = Join-Path $caseRoot 'fixture.source.patch'
& git -C $root diff --binary "--output=$patch" d549e1dc4e2a083b08b4f24cb5495e81b99d79b5 HEAD
Assert ($LASTEXITCODE -eq 0) 'Actual fixture source patch'
$build = [pscustomobject]@{
    schema = 2; kind = 'android-official-guest-readiness-phase'; phase = 'Pack'; status = 'completed-unadmitted'
    baseline = 'd549e1dc4e2a083b08b4f24cb5495e81b99d79b5'; sourceCommit = $environment.BUILD_SOURCEVERSION
    version = $identity.version; buildId = $identity.buildId; patchSha256 = (Get-FileHash $patch).Hash.ToLowerInvariant()
    templates = @(
        [pscustomobject]@{ repository = 'DevDiv/Xamarin.yaml-templates'; commit = $environment.GUEST_YAML_VERSION },
        [pscustomobject]@{ repository = '1ESPipelineTemplates/MicroBuildTemplate'; commit = $environment.GUEST_1ES_VERSION })
    files = [Collections.Generic.List[object]]::new(); packages = [Collections.Generic.List[object]]::new()
}
foreach ($rid in @('android-arm64', 'android-x64')) {
    $id = "Microsoft.Android.Runtime.Mono.36.$rid"
    $file = Native-Fixture "$id.$($identity.version)" $rid
    $inventory = Read-GuestRuntimePackInventory $file $rid $identity.version
    $name = "build.inventory.$id.json"
    # Mac-style raw JSON bytes deliberately differ from Windows-native pretty JSON.
    $json = ($inventory | ConvertTo-Json -Depth 12).Replace("`r`n", "`n").Replace("`r", "`n").TrimEnd([char]10) + "`n"
    [IO.File]::WriteAllBytes((Join-Path $buildDirectory $name), [Text.UTF8Encoding]::new($false).GetBytes($json))
    Assert ($json.Contains("data/caf$([char]0xe9).txt")) 'Non-ASCII member is retained without host-specific escaping'
    $windowsJson = $json.Replace("`n", "`r`n")
    [IO.File]::WriteAllBytes((Join-Path $buildDirectory "windows-native.$id.json"), [Text.UTF8Encoding]::new($false).GetBytes($windowsJson))
    Assert ($windowsJson.Length -gt $json.Length) 'Cross-host regression has different serialized bytes'
    $build.files.Add([pscustomobject]@{ fileName = [IO.Path]::GetFileName($file); sizeBytes = (Get-Item $file).Length; sha256 = $inventory.sha256 })
    $build.packages.Add([pscustomobject]@{ id = $id; inventory = [pscustomobject]@{
        fileName = $name; sha256 = (Get-FileHash (Join-Path $buildDirectory $name)).Hash.ToLowerInvariant() } })
}
$packJson = ($build | ConvertTo-Json -Depth 12).Replace("`r`n", "`n").Replace("`r", "`n").TrimEnd([char]10) + "`n"
[IO.File]::WriteAllBytes((Join-Path $buildDirectory 'pack-receipt.json'), [Text.UTF8Encoding]::new($false).GetBytes($packJson))
[IO.File]::WriteAllBytes((Join-Path $buildDirectory 'pack-receipt.mac-lf.json'), [Text.UTF8Encoding]::new($false).GetBytes($packJson))
$env:GUEST_BUILD_RECEIPT_DIRECTORY = $buildDirectory
$env:GUEST_UNSIGNED_DIRECTORY = $out
$env:GUEST_SIGN_RECEIPT_DIRECTORY = Join-Path $caseRoot 'accepted-input'
& "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Input
$inputReceipt = Get-Content (Join-Path $env:GUEST_SIGN_RECEIPT_DIRECTORY 'input-receipt.json') -Raw | ConvertFrom-Json
Assert ($inputReceipt.packages.Count -eq 2 -and $inputReceipt.status -ceq 'completed-unadmitted') 'Actual expected signed-job input filename collection'
foreach ($package in $inputReceipt.packages) {
    $original = Join-Path $buildDirectory "build.inventory.$($package.id).json"
    $collected = Join-Path $env:GUEST_SIGN_RECEIPT_DIRECTORY $package.inventory.fileName
    $bytes = [IO.File]::ReadAllBytes($collected)
    Assert ([Convert]::ToBase64String($bytes) -ceq [Convert]::ToBase64String([IO.File]::ReadAllBytes($original))) 'Mac LF inventory equals actual Windows Input serialization byte-for-byte'
    Assert ($bytes[0] -eq 123 -and $bytes -notcontains 13 -and $bytes[-1] -eq 10 -and $bytes[-2] -ne 10) 'Canonical UTF-8 no BOM, LF only, exactly one terminal LF'
    Assert ($package.inventory.sha256 -ceq (Get-FileHash $original).Hash.ToLowerInvariant()) 'Representation hash is independently preserved'
    $parsed = Get-Content $collected -Raw | ConvertFrom-Json
    Assert ($parsed.sha256 -ceq (Get-FileHash (Join-Path $out $parsed.fileName)).Hash.ToLowerInvariant()) 'Archive byte hash remains independent of JSON representation hash'
}
Assert ((Get-FileHash (Join-Path $env:GUEST_SIGN_RECEIPT_DIRECTORY 'build-receipt.json')).Hash -ceq
    (Get-FileHash (Join-Path $buildDirectory 'pack-receipt.json')).Hash) 'Input copies Pack root bytes without reserialization'
$build.files[0].sha256 = '0' * 64
$build | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $buildDirectory 'pack-receipt.json') -Encoding utf8
$env:GUEST_SIGN_RECEIPT_DIRECTORY = Join-Path $caseRoot 'rejected-input'
Reject { & "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Input } 'Unsigned package differs from build output.'
$failedReceipt = Get-Content (Join-Path $env:GUEST_SIGN_RECEIPT_DIRECTORY 'input-receipt.json') -Raw | ConvertFrom-Json
Assert ($failedReceipt.status -ceq 'failed' -and $failedReceipt.commands.Count -eq 2) 'Failure persists source command ledger'

# Run the standard verifier, unchanged trust, on an intentionally invalid signature.
# The test-local SDK selection avoids the repository build SDK pin; production uses the signer staging cwd.
$sdk = (& dotnet --list-sdks | Select-Object -Last 1).Split(' ')[0]
@{ sdk = @{ version = $sdk } } | ConvertTo-Json | Set-Content (Join-Path $out 'global.json')
$commandReceipt = [pscustomobject]@{ schema = 2; status = 'running'; failure = $null; commands = [Collections.Generic.List[object]]::new() }
$commandPath = Join-Path $out 'actual-synthetic-verifier-receipt.json'
$verify = Invoke-GuestProducerCommand -Name actual-invalid-signature -Tool dotnet `
    -Arguments @('nuget', 'verify', '--all', [IO.Path]::GetFileName($afterFile)) -WorkingDirectory $out `
    -Receipt $commandReceipt -ReceiptPath $commandPath -AllowNonzeroExit
Assert ($verify.result -ceq 'exited-nonzero' -and $verify.exitCode -ne 0) 'Actual invalid signature rejected'
$verifyLog = Join-Path $out $verify.outputFileName
Assert ((Get-Content $verifyLog -Raw).Contains("NU3005: The package signature file entry is invalid.")) 'Verifier actually inspected invalid compressed signature'
$actualSignature = New-GuestRuntimePackSignature $after $verify.exitCode $sdk $verifyLog
$actualSignature | ConvertTo-Json | Set-Content (Join-Path $out 'actual-synthetic-signature.json') -Encoding utf8
$commandReceipt.status = 'produced-verification-failed'
Write-GuestProducerReceipt $commandReceipt $commandPath

# Load the exact pure receipt helpers, not the CI entry point or a copied validator.
$parseErrors = $null; $parseTokens = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    "$root\build-tools\scripts\guest-readiness-official.ps1", [ref] $parseTokens, [ref] $parseErrors)
Assert ($parseErrors.Count -eq 0) 'Official script parses'
$definitions = $ast.FindAll({
    param ($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -in @('Get-VerifiedGuestBuildReceipt', 'Copy-GuestReceiptFile')
}, $false)
Assert ($definitions.Count -eq 2) 'Production receipt helpers found'
foreach ($definition in $definitions) { . ([scriptblock]::Create($definition.Extent.Text)) }
$bindingDirectory = Join-Path $out ('binding-case-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $bindingDirectory | Out-Null
$expectedArguments = @('jenkins', 'CONFIGURATION=Debug', 'PREPARE_CI=1', 'PREPARE_AUTOPROVISION=1',
    "MSBUILD_ARGS=-p:AndroidPackVersionLong=$($identity.version) -p:PackageVersion=$($identity.version) -p:AndroidStartupDiagnosticsBuildId=$($identity.buildId) -p:AndroidGuestReadinessBuild=true -p:RunningOnCI=true")
$syntheticBuild = [pscustomobject]@{
    schema = 2; kind = 'android-official-guest-readiness-phase'; component = 'android'; phase = 'Build'
    baseline = $build.baseline; sourceCommit = $build.sourceCommit; version = $build.version
    buildId = $build.buildId; patchSha256 = $build.patchSha256; definitionId = '11410'
    pipelinePath = 'build-tools/automation/azure-pipelines.yaml'; sourceBranch = $environment.BUILD_SOURCEBRANCH
    buildNumber = '12345'; jobName = 'synthetic-not-executed'; jobAttempt = '1'; requestedSignType = 'Test'
    templates = $build.templates; commands = [Collections.Generic.List[object]]::new()
    files = @(); packages = @(); status = 'completed-unadmitted'; failure = $null; packageAdmission = $false
}
foreach ($name in @('build-source-patch', 'build-submodules', 'build-make-jenkins')) {
    $log = Join-Path $bindingDirectory "$name.log"
    'SYNTHETIC receipt-validator fixture. This command was NOT executed.' | Set-Content $log
    $syntheticBuild.commands.Add([pscustomobject]@{
        name = $name; requestedTool = $(if ($name -eq 'build-make-jenkins') { 'make' } else { 'git' })
        resolvedTool = 'synthetic-not-executed'
        arguments = $(if ($name -eq 'build-make-jenkins') { $expectedArguments } else { @('synthetic') })
        workingDirectory = $bindingDirectory; startedAtUtc = '2026-01-01T00:00:00Z'; endedAtUtc = '2026-01-01T00:00:01Z'
        result = 'exited-zero'; exitCode = 0; error = $null; outputFileName = "$name.log"
        outputSha256 = (Get-FileHash $log).Hash.ToLowerInvariant(); outputStreams = 'combined stdout/stderr'
    })
}
$buildRootPath = Join-Path $bindingDirectory 'synthetic-original-build-receipt.json'
$buildJson = ($syntheticBuild | ConvertTo-Json -Depth 15).Replace("`r`n", "`n").Replace("`r", "`n").TrimEnd([char]10) + "`n"
[IO.File]::WriteAllBytes($buildRootPath, [Text.UTF8Encoding]::new($false).GetBytes($buildJson))
$originalHash = (Get-FileHash $buildRootPath).Hash
$verified = Get-VerifiedGuestBuildReceipt $buildRootPath $syntheticBuild $expectedArguments
Assert ($verified.commands.Count -eq 3) 'Complete synthetic Build ledger accepted by actual validator'
$linked = Copy-GuestReceiptFile $buildRootPath (Join-Path $bindingDirectory 'build-phase-receipt.json')
Assert ($linked.fileName -ceq 'build-phase-receipt.json' -and $linked.sha256 -ceq $originalHash.ToLowerInvariant()) 'Distinct Build leaf and identical bytes'
Assert ((Get-FileHash $buildRootPath).Hash -ceq $originalHash) 'Earlier Build root never rewritten'
Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes($buildRootPath)) -ceq
    [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $bindingDirectory 'build-phase-receipt.json')))) 'Mac LF Build root copied byte-for-byte on Windows'
$syntheticPack = $syntheticBuild | ConvertTo-Json -Depth 15 | ConvertFrom-Json
$syntheticPack.phase = 'Pack'; $syntheticPack.files = @($linked); $syntheticPack.commands = @()
$syntheticPack | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $bindingDirectory 'synthetic-pack-root-shape.json') -Encoding utf8

foreach ($field in @('sourceCommit', 'version', 'buildId', 'patchSha256')) {
    $expected = $syntheticBuild | ConvertTo-Json -Depth 15 | ConvertFrom-Json
    $expected.$field = 'different'
    Reject { Get-VerifiedGuestBuildReceipt $buildRootPath $expected $expectedArguments } "Earlier Build receipt identity differs: $field"
}
$expected = $syntheticBuild | ConvertTo-Json -Depth 15 | ConvertFrom-Json
$expected.templates[0].commit = 'a' * 40
Reject { Get-VerifiedGuestBuildReceipt $buildRootPath $expected $expectedArguments } 'Earlier Build resolved templates differ.'
Reject { Get-VerifiedGuestBuildReceipt $buildRootPath $syntheticBuild @('wrong') } 'Earlier Build command did not use the expected normal build properties.'
$bad = $syntheticBuild | ConvertTo-Json -Depth 15 | ConvertFrom-Json
$bad.commands[2].exitCode = 7
$badPath = Join-Path $bindingDirectory 'invalid-build.json'
$bad | ConvertTo-Json -Depth 15 | Set-Content $badPath -Encoding utf8
Reject { Get-VerifiedGuestBuildReceipt $badPath $syntheticBuild $expectedArguments } 'Earlier Build command did not complete successfully.'
$logPath = Join-Path $bindingDirectory 'build-make-jenkins.log'
$savedLog = [IO.File]::ReadAllBytes($logPath)
'changed' | Set-Content $logPath
Reject { Get-VerifiedGuestBuildReceipt $buildRootPath $syntheticBuild $expectedArguments } 'Earlier Build command log differs from its receipt.'
[IO.File]::WriteAllBytes($logPath, $savedLog)
$signListSource = Join-Path $bindingDirectory 'source-SignList.xml'
$signListBytes = [Text.UTF8Encoding]::new($false).GetBytes("<Project>`n<!-- synthetic policy-copy fixture, not signing policy -->`n</Project>`n")
[IO.File]::WriteAllBytes($signListSource, $signListBytes)
$signListDestination = Join-Path $bindingDirectory 'SignList.xml'
$signListRef = Copy-GuestReceiptFile $signListSource $signListDestination
Assert ($signListRef.sha256 -ceq (Get-FileHash $signListSource).Hash.ToLowerInvariant()) 'Retained exact SignList reference'
Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes($signListDestination)) -ceq [Convert]::ToBase64String($signListBytes)) 'Mac LF SignList copied byte-for-byte on Windows'
Copy-GuestReceiptFile $signListSource $signListDestination | Out-Null
$collisionDestination = Join-Path $bindingDirectory 'collision-SignList.xml'
'different policy' | Set-Content $collisionDestination
Reject { Copy-GuestReceiptFile $signListSource $collisionDestination } 'Retained receipt file collides with different bytes.'
Assert ((Get-Content $collisionDestination -Raw).Trim() -ceq 'different policy') 'Collision not overwritten'

# Run the complete Output entry point against synthetic signing inputs. Only Git and
# standard NuGet verification execute; template files/binlogs/configs do not attest a real signer/build.
$failureRoot = Join-Path $out ('output-failure-case-' + [Guid]::NewGuid().ToString('N'))
$packed = Join-Path $failureRoot 'packed'
$unsigned = Join-Path $failureRoot 'unsigned'
$failureBuildDirectory = Join-Path $failureRoot 'build'
$signReceiptDirectory = Join-Path $failureRoot 'receipts'
$retained = Join-Path $failureRoot 'retained'
$templateRoot = Join-Path $failureRoot 'yaml-templates'
$microbuildRoot = Join-Path $failureRoot 'microbuild'
$signBinlogs = Join-Path $failureRoot 'signing-binlogs'
foreach ($directory in @($packed, $unsigned, $failureBuildDirectory, $signReceiptDirectory, $templateRoot, $microbuildRoot, $signBinlogs)) {
    New-Item -ItemType Directory -Force $directory | Out-Null
}
@{ kind = 'synthetic-output-failure-fixture'; normalBuildOrSignerExecuted = $false
   templateGitContext = 'Fixture folders inherit this test repository HEAD; not actual template checkouts.'
   priorJobStatus = 'Failed'; normalCopySignedOutputDirectoryPresent = $false
} | ConvertTo-Json | Set-Content (Join-Path $failureRoot 'fixture-scope.json')
foreach ($spec in @(
    @{ root = $templateRoot; paths = @('sign-artifacts\jobs\v4.yml', 'sign-artifacts\steps\v4.yml',
        'sign-artifacts\steps\v4-SignFiles.proj', 'sign-artifacts\steps\common\Extract.ps1',
        'sign-artifacts\steps\common\EscapeSignFiles.ps1') },
    @{ root = $microbuildRoot; paths = @('azure-pipelines\MicroBuild.1ES.Unofficial.yml',
        'azure-pipelines\Stages\Stage.yml', 'azure-pipelines\Jobs\Job.yml') }
)) {
    foreach ($relativePath in $spec.paths) {
        $destination = Join-Path $spec.root $relativePath
        New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
        'SYNTHETIC fixture, not actual template source.' | Set-Content $destination
    }
}
foreach ($name in @('SignPackageContents.binlog', 'SignNuGetPackages.binlog')) {
    'SYNTHETIC fixture, not a real signing binlog.' | Set-Content (Join-Path $signBinlogs $name)
}
$failureBuild = $build | ConvertTo-Json -Depth 15 | ConvertFrom-Json
foreach ($template in $failureBuild.templates) { $template.commit = $environment.BUILD_SOURCEVERSION }
$failureBuild.files = [Collections.Generic.List[object]]::new()
$failureBuild.packages = [Collections.Generic.List[object]]::new()
foreach ($rid in @('android-arm64', 'android-x64')) {
    $id = "Microsoft.Android.Runtime.Mono.36.$rid"
    $leaf = "$id.$($identity.version).nupkg"
    Copy-Item (Native-Fixture "output-before-$rid" $rid) (Join-Path $unsigned $leaf)
    Copy-Item (Native-Fixture "output-after-$rid" $rid 'signature') (Join-Path $packed $leaf)
    $inventory = Read-GuestRuntimePackInventory (Join-Path $unsigned $leaf) $rid $identity.version
    $inventoryName = "build.inventory.$id.json"
    $json = ($inventory | ConvertTo-Json -Depth 12).Replace("`r`n", "`n").Replace("`r", "`n").TrimEnd([char]10) + "`n"
    [IO.File]::WriteAllText((Join-Path $failureBuildDirectory $inventoryName), $json, [Text.UTF8Encoding]::new($false))
    $failureBuild.files.Add([pscustomobject]@{ fileName = $leaf; sizeBytes = $inventory.sizeBytes; sha256 = $inventory.sha256 })
    $failureBuild.packages.Add([pscustomobject]@{ id = $id; inventory = [pscustomobject]@{
        fileName = $inventoryName; sha256 = (Get-FileHash (Join-Path $failureBuildDirectory $inventoryName)).Hash.ToLowerInvariant() } })
    $abi = if ($rid -eq 'android-arm64') { 'arm64-v8a' } else { 'x86_64' }
    foreach ($configuration in @('Debug', 'Release')) {
        foreach ($name in @("CMakeCache-$abi-$configuration.txt", "CMakeCCompiler-$abi-$configuration.cmake", "CMakeCXXCompiler-$abi-$configuration.cmake")) {
            $file = Join-Path $failureBuildDirectory $name
            'SYNTHETIC fixture, not an actual compiler configuration.' | Set-Content $file
            $failureBuild.files.Add([pscustomobject]@{ fileName = $name; sizeBytes = (Get-Item $file).Length; sha256 = (Get-FileHash $file).Hash.ToLowerInvariant() })
        }
    }
}
$failureBuild | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $failureBuildDirectory 'pack-receipt.json') -Encoding utf8
$env:GUEST_YAML_VERSION = $environment.BUILD_SOURCEVERSION
$env:GUEST_1ES_VERSION = $environment.BUILD_SOURCEVERSION
$env:GUEST_BUILD_RECEIPT_DIRECTORY = $failureBuildDirectory
$env:GUEST_UNSIGNED_DIRECTORY = $unsigned
$env:GUEST_SIGN_RECEIPT_DIRECTORY = $signReceiptDirectory
$env:GUEST_TEMPLATE_CHECKOUT = $templateRoot
$env:GUEST_1ES_CHECKOUT = $microbuildRoot
$env:GUEST_SIGN_BINLOG_DIRECTORY = $signBinlogs
$env:GUEST_SIGNING_OUTPUT_DIRECTORY = $packed
$env:GUEST_RETAINED_OUTPUT_DIRECTORY = $retained
$env:GUEST_SIGNING_JOB_STATUS = 'Failed'
& "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Input
Reject { & "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Output } 'Normal output retained, but standard verification failed; no policy admission.'
$captured = Get-Content (Join-Path $signReceiptDirectory 'output-receipt.json') -Raw | ConvertFrom-Json
Assert ($captured.status -ceq 'produced-verification-failed' -and $captured.failure.stage -ceq 'prior-signing-job') 'Original failed job remains failed with actual post-verifier failures'
$contextRef = @($captured.files | Where-Object { $_.fileName -ceq 'output-signing-context.json' })
Assert ($contextRef.Count -eq 1) 'Pre-hook observation bound in existing Output files'
$contextPath = Join-Path $signReceiptDirectory $contextRef[0].fileName
Assert ((Get-FileHash $contextPath).Hash.ToLowerInvariant() -ceq $contextRef[0].sha256) 'Observed signing context hash bound'
$context = Get-Content $contextPath -Raw | ConvertFrom-Json
Assert ($context.priorJobStatus -ceq 'Failed' -and $context.observationPhase -ceq 'Output-hook-entry') 'Exact observed prior job status and phase'
Assert ([DateTimeOffset]::Parse($context.observedAtUtc) -le [DateTimeOffset]::Parse($captured.commands[0].startedAtUtc)) 'Observation predates capture commands'
Assert (-not @($captured.files | Where-Object { $_.fileName.StartsWith('postsign.') }).Count) 'No reverse post-sign receipt hash cycle'
Assert (-not (Test-Path (Join-Path $failureRoot 'signed'))) 'Normal CopySignedOutput destination is absent'
Assert (@(Get-ChildItem $retained -File).Count -eq 2) 'Only the two expected produced archives retained'
foreach ($rid in @('android-arm64', 'android-x64')) {
    $id = "Microsoft.Android.Runtime.Mono.36.$rid"
    $leaf = "$id.$($identity.version).nupkg"
    Assert ((Get-FileHash (Join-Path $packed $leaf)).Hash -ceq (Get-FileHash (Join-Path $retained $leaf)).Hash) 'Retained archive is byte-identical to normal packed output'
    $signature = Get-Content (Join-Path $signReceiptDirectory "output.signature.$id.json") -Raw | ConvertFrom-Json
    $command = @($captured.commands | Where-Object { $_.name -ceq "output-verify-$rid" })[0]
    Assert ($signature.verificationExitCode -ne 0 -and $signature.verificationExitCode -eq $command.exitCode) 'Actual fresh verifier exit retained'
    Assert ($command.workingDirectory -ceq $packed -and $command.arguments[-1] -ceq $leaf) 'Verification uses exact normal packed path and leaf'
    Assert ($signature.outputSha256 -ceq (Get-FileHash (Join-Path $signReceiptDirectory $signature.outputFileName)).Hash.ToLowerInvariant()) 'Actual failure log retained'
    $postsign = Get-Content (Join-Path $signReceiptDirectory "postsign.$id.json") -Raw | ConvertFrom-Json
    Assert ($postsign.status -ceq 'produced-verification-failed') 'Common sidecar does not invent success'
    $verifyRef = @($postsign.operationEvidence | Where-Object { $_.role -ceq 'verify' })[0].reference
    Assert ($verifyRef.sha256 -ceq (Get-FileHash (Join-Path $signReceiptDirectory 'output-receipt.json')).Hash.ToLowerInvariant()) 'Post-sign verify reference binds finalized Output in one direction'
}

# Exercise the production status assignment with modeled flags; no successful verifier is fabricated.
$statusAssignment = $ast.Find({
    param ($node)
    $node -is [Management.Automation.Language.AssignmentStatementAst] -and
        $node.Left.Extent.Text -ceq '$receipt.status' -and $node.Right.Extent.Text.Contains('$verificationFailed')
}, $true)
Assert ($null -ne $statusAssignment) 'Production Output status assignment found'
foreach ($case in @(
    @{ verifyFailed = $false; priorFailed = $false; status = 'completed-unadmitted' },
    @{ verifyFailed = $false; priorFailed = $true; status = 'failed' },
    @{ verifyFailed = $true; priorFailed = $false; status = 'produced-verification-failed' },
    @{ verifyFailed = $true; priorFailed = $true; status = 'produced-verification-failed' }
)) {
    $receipt = [pscustomobject]@{ status = 'running' }
    $verificationFailed = $case.verifyFailed; $priorSigningFailed = $case.priorFailed
    & ([scriptblock]::Create($statusAssignment.Extent.Text))
    Assert ($receipt.status -ceq $case.status) 'Fresh verification success cannot erase inherited failure'
}
$constructed = $captured | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$constructed.status = 'completed-unadmitted'; $constructed.failure = $null
$constructed.commands = @(); $constructed.files = @(); $constructed.packages = @()
@{
    fixtureKind = 'constructed-success-root-shape-only'
    notice = 'Modeled root shape with no commands, files or packages; NOT actual successful build/sign/verification evidence and NOT admissible.'
    root = $constructed
} | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $failureRoot 'constructed-success-root-shape.json') -Encoding utf8

# Missing packed outputs never fall back to available unsigned inputs.
$env:GUEST_SIGN_RECEIPT_DIRECTORY = Join-Path $failureRoot 'missing-packed-receipts'
$env:GUEST_RETAINED_OUTPUT_DIRECTORY = Join-Path $failureRoot 'missing-packed-retained'
$env:GUEST_SIGNING_OUTPUT_DIRECTORY = Join-Path $failureRoot 'missing-packed'
Reject { & "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Output } 'Producer command failed: output-verification-tool-version (invocation-failed).'
Assert (@(Get-ChildItem $env:GUEST_RETAINED_OUTPUT_DIRECTORY -File).Count -eq 0) 'No unsigned fallback when normal outputs are missing'
$missing = Get-Content (Join-Path $env:GUEST_SIGN_RECEIPT_DIRECTORY 'output-receipt.json') -Raw | ConvertFrom-Json
Assert ($missing.status -ceq 'failed' -and $missing.failure.message.Contains('Prior signing job status: Failed')) 'Missing output retains explicit failed provenance'

# A normal packed candidate without a signature is observed and verified, never copied as signed output.
$unsignedPacked = Join-Path $failureRoot 'unsigned-packed'
New-Item -ItemType Directory $unsignedPacked | Out-Null
foreach ($rid in @('android-arm64', 'android-x64')) {
    $leaf = "Microsoft.Android.Runtime.Mono.36.$rid.$($identity.version).nupkg"
    Copy-Item (Join-Path $unsigned $leaf) (Join-Path $unsignedPacked $leaf)
}
$env:GUEST_SIGN_RECEIPT_DIRECTORY = Join-Path $failureRoot 'unsigned-output-receipts'
New-Item -ItemType Directory $env:GUEST_SIGN_RECEIPT_DIRECTORY | Out-Null
foreach ($rid in @('android-arm64', 'android-x64')) {
    $name = "input.inventory.Microsoft.Android.Runtime.Mono.36.$rid.json"
    Copy-Item (Join-Path $signReceiptDirectory $name) (Join-Path $env:GUEST_SIGN_RECEIPT_DIRECTORY $name)
}
$env:GUEST_RETAINED_OUTPUT_DIRECTORY = Join-Path $failureRoot 'unsigned-output-retained'
$env:GUEST_SIGNING_OUTPUT_DIRECTORY = $unsignedPacked
Reject { & "$root\build-tools\scripts\guest-readiness-official.ps1" -Phase Output } 'Normal packed output has no signature entry; unsigned output is not retained as signed output.'
Assert (@(Get-ChildItem $env:GUEST_RETAINED_OUTPUT_DIRECTORY -File).Count -eq 0) 'Missing signatures are not laundered into retained signed output'
$unsignedSignature = Get-Content (Join-Path $env:GUEST_SIGN_RECEIPT_DIRECTORY 'output.signature.Microsoft.Android.Runtime.Mono.36.android-arm64.json') -Raw | ConvertFrom-Json
Assert ($unsignedSignature.classification -ceq 'unsigned' -and $unsignedSignature.verificationExitCode -ne 0) 'Unsigned failure classification and actual verifier result retained'
Write-Output 'PASS: actual Output failure path with absent normal signed directory, two retained invalid-signature archives, original failed status, missing-packed and unsigned-output rejection.'
Write-Output 'PASS: Build-to-Pack source/command/log binding, byte-identical distinct receipt alias and SignList retention/collision.'
Write-Output 'PASS: Mac-style LF/non-ASCII inventory through actual Input collector; deterministic UTF-8/no-BOM/LF/terminal newline and independent archive hash; raw Build/Pack/SignList copies unchanged.'
Write-Output 'PASS: exact manual gates, real nested MSBuild OFF/ON/rejections, real ZIP native metadata/deltas, synthetic post-sign constructors and actual standard verification failure.'
exit 0
