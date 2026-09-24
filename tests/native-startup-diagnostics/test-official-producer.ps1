param ([string] $MSBuild = 'C:\Program Files\Microsoft Visual Studio\18\IntPreview\MSBuild\Current\Bin\MSBuild.exe')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$out = Join-Path $root 'bin\guest-readiness-official-tests'
New-Item -ItemType Directory -Force $out | Out-Null
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
Write-Output 'PASS: Build-to-Pack source/command/log binding, byte-identical distinct receipt alias and SignList retention/collision.'
Write-Output 'PASS: Mac-style LF/non-ASCII inventory through actual Input collector; deterministic UTF-8/no-BOM/LF/terminal newline and independent archive hash; raw Build/Pack/SignList copies unchanged.'
Write-Output 'PASS: exact manual gates, real nested MSBuild OFF/ON/rejections, real ZIP native metadata/deltas, synthetic post-sign constructors and actual standard verification failure.'
exit 0
