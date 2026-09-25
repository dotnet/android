$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..\..")
Import-Module "$root\build-tools\scripts\guest-readiness-runtime-pack.psm1" -Force

function Assert([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}
function Expect-Failure {
    param ([Parameter(Mandatory)][scriptblock] $Action, [Parameter(Mandatory)][string] $ExpectedMessage)
    $failed = $false
    try { & $Action | Out-Null } catch {
        if ($ExpectedMessage -and $_.Exception.Message -cne $ExpectedMessage) { throw }
        $failed = $true
    }
    Assert $failed 'Expected validation failure'
}

$plan = New-GuestRuntimePackPlan $root '12345' '1'
Assert ($plan.commands.Count -eq 4) 'Closed normal command graph'
Assert ($plan.rids.Count -eq 2 -and $plan.rids[0] -ceq 'android-arm64' -and $plan.rids[1] -ceq 'android-x64') 'Exact two RIDs'
Assert ($plan.version -ceq '36.1.69-guest.12345.1' -and $plan.admission -ceq 'not-qualified') 'Unique non-held version, no admission'
Assert ((New-GuestRuntimePackPlan $root '12345' '2').version -cne $plan.version) 'Attempt-specific version'
foreach ($invalid in @('0', '01', '-1', '1.2', '1+2', '1;echo', '1234567890123', "1`n")) {
    Expect-Failure { New-GuestRuntimePackPlan $root $invalid '1' } 'Build number and attempt must be bounded positive decimal identities.'
}
Expect-Failure { New-GuestRuntimePackPlan $root '1' '0001' } 'Build number and attempt must be bounded positive decimal identities.'
Expect-Failure { New-GuestRuntimePackPlan $root "1`r" '1' } 'Build number and attempt must be bounded positive decimal identities.'
if ($IsWindows) {
    foreach ($control in @("`r", "`n")) {
        Expect-Failure {
            & "$root\build-tools\scripts\guest-readiness-runtime-pack.ps1" -Execute -BuildNumber 1 -Attempt 1 `
                -ExpectedSourceCommit (('0' * 40) + $control) -ExpectedPatchSha256 ('0' * 64)
        } 'Explicit reviewed source and binary patch identities are required.'
        Expect-Failure {
            & "$root\build-tools\scripts\guest-readiness-runtime-pack.ps1" -Execute -BuildNumber 1 -Attempt 1 `
                -ExpectedSourceCommit ('0' * 40) -ExpectedPatchSha256 (('0' * 64) + $control)
        } 'Explicit reviewed source and binary patch identities are required.'
    }
}
foreach ($command in $plan.commands) {
    Assert ($command.arguments -contains "-p:AndroidPackVersionLong=$($plan.version)") 'Global long version on every process'
    Assert ($command.arguments -contains "-p:PackageVersion=$($plan.version)") 'Global package version on every process'
    Assert ($command.arguments -contains "-p:AndroidStartupDiagnosticsBuildId=$($plan.buildId)") 'Marker reaches normal build'
    Assert ($command.arguments -contains '-p:RunningOnCI=true') 'No local workload setup'
    foreach ($arg in $command.arguments) {
        Assert ($arg -notmatch 'PackDotNet|BuildDotNet|CreateAllPacks|ExtractWorkloadPacks|PushManifest|skip-sign|NuGetAudit=false|DisableApiCompatibilityCheck|no-dependencies') 'No forbidden build/publish/bypass entry point'
    }
}
for ($index = 2; $index -lt 4; ++$index) {
    $command = $plan.commands[$index]
    Assert ($command.arguments[0] -ceq 'build' -and $command.arguments[1].EndsWith('\Microsoft.Android.Runtime.proj')) 'Normal pack project, no repacking'
    Assert ($command.arguments -contains '-p:AndroidRuntime=Mono' -and $command.arguments -contains '-p:AndroidApiLevel=36') 'Held API and runtime flavor'
    Assert ($command.arguments -contains "-p:AndroidRID=$($plan.rids[$index - 2])") 'One explicit RID per normal pack invocation'
}

# Real ZIP readers/hashers run against labeled synthetic packages, never treated as producer artifacts.
$fixtureRoot = "$root\bin\guest-runtime-pack-fixtures"
New-Item -ItemType Directory -Force $fixtureRoot | Out-Null
function New-Fixture([string] $Name, [string] $Mode = 'valid', [string] $Rid = 'android-arm64') {
    $path = "$fixtureRoot\$Name.nupkg"
    $stream = [IO.File]::Open($path, [IO.FileMode]::Create)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $version = if ($Mode -eq 'held') { '36.1.69' } else { $plan.version }
        $items = [ordered]@{
            'package.nuspec' = "<package><metadata><id>Microsoft.Android.Runtime.Mono.36.$Rid</id><version>$version</version></metadata></package>"
            'data/RuntimeList.xml' = '<FileList />'
        }
        foreach ($asset in @('libmono-android.debug.so', 'libmono-android.release.so', 'libxamarin-debug-app-helper.so',
            'libxamarin-native-tracing.so', 'libunwind_xamarin-debug.a', 'libunwind_xamarin-release.a',
            'libc.so', 'libdl.so', 'liblog.so', 'libm.so', 'libz.so', 'libarchive-dso-stub.so')) {
            if ($Mode -eq 'missing' -and $asset -eq 'libmono-android.debug.so') { continue }
            $items["runtimes/$Rid/native/$asset"] = 'synthetic-not-ELF'
        }
        if ($Mode -in @('host-metadata', 'host-metadata-directories')) {
            $items["runtimes/$Rid/lib/netstandard2.0/Microsoft.Android.Runtimes.deps.json"] = '{"fixture":"synthetic deps metadata"}'
            $items["runtimes/$Rid/lib/netstandard2.0/Microsoft.Android.Runtimes.runtimeconfig.json"] = '{"runtimeOptions":{"tfm":"netstandard2.0"}}'
        }
        if ($Mode -eq 'traversal') { $items['../escape'] = 'bad' }
        if ($Mode -eq 'wrong-rid') { $items['runtimes/android-x64/native/wrong.so'] = 'bad' }
        if ($Mode -eq 'signature') { $items['.signature.p7s'] = 'synthetic-not-a-signature' }
        foreach ($entry in $items.GetEnumerator()) {
            $writer = [IO.StreamWriter]::new($zip.CreateEntry($entry.Key).Open())
            try { $writer.Write($entry.Value) } finally { $writer.Dispose() }
        }
        if ($Mode -eq 'duplicate') {
            $writer = [IO.StreamWriter]::new($zip.CreateEntry('PACKAGE.NUSPEC').Open())
            $writer.Dispose()
        }
        $extraNames = switch ($Mode) {
            'empty-component' { 'bad//entry' }
            'dot' { 'bad/./entry' }
            'absolute' { '/bad' }
            'backslash' { 'bad\entry' }
            'drive' { 'C:bad' }
            'control' { "bad$([char]1)entry" }
            'del' { "bad$([char]127)entry" }
            'path-length' { 'a' * 1025 }
            'path-boundary' { 'a' * 1024 }
            'streaming' { 'multiple-blocks' }
            'trailing-space' { 'bad /entry' }
            'trailing-dot' { 'bad./entry' }
            'device' { 'bad/CON.txt' }
            'unicode-alias' { "caf$([char]0xe9)"; "cafe$([char]0x301)" }
            'file-directory' { 'collision'; 'collision/' }
            'parent-first' { 'collision'; 'collision/child' }
            'child-first' { 'collision/child'; 'collision' }
            'directory-data' { 'nonempty/' }
            'directory-flag' { 'not-a-directory' }
            'symlink' { 'link' }
            'signature-case' { '.SIGNATURE.P7S' }
            'signature-directory' { '.signature.p7s/' }
            'directories' { 'empty/'; 'runtimes/'; 'runtimes/android-arm64/'; 'runtimes/android-arm64/native/' }
            'host-metadata-directories' { 'runtimes/'; "runtimes/$Rid/"; "runtimes/$Rid/lib/"; "runtimes/$Rid/lib/netstandard2.0/" }
            'metadata-wrong-rid' { 'runtimes/android-x64/lib/netstandard2.0/Microsoft.Android.Runtimes.deps.json' }
            'metadata-wrong-tfm' { "runtimes/$Rid/lib/net10.0/Microsoft.Android.Runtimes.deps.json" }
            'metadata-wrong-case' { "runtimes/$Rid/lib/netstandard2.0/microsoft.Android.Runtimes.deps.json" }
            'metadata-extra-json' { "runtimes/$Rid/lib/netstandard2.0/other.json" }
            'metadata-managed-dll' { "runtimes/$Rid/lib/netstandard2.0/Microsoft.Android.dll" }
            'metadata-nested' { "runtimes/$Rid/lib/netstandard2.0/other/Microsoft.Android.Runtimes.deps.json" }
            'metadata-directory-leaf' { "runtimes/$Rid/lib/netstandard2.0/Microsoft.Android.Runtimes.deps.json/" }
            'metadata-file-parent' { "runtimes/$Rid/lib/netstandard2.0" }
            'metadata-foreign-directory' { 'runtimes/android-x64/lib/netstandard2.0/' }
            'metadata-directory-data' { "runtimes/$Rid/lib/netstandard2.0/" }
            'entry-count' { for ($i = 14; $i -lt 20001; ++$i) { "empty-$i" } }
            'count-boundary' { for ($i = 14; $i -lt 20000; ++$i) { "empty-$i" } }
        }
        foreach ($name in $extraNames) {
            $entry = $zip.CreateEntry($name)
            if ($Mode -eq 'symlink') { $entry.ExternalAttributes = 0xa1ff -shl 16 }
            if ($Mode -eq 'directory-flag') { $entry.ExternalAttributes = 0x10 }
            if ($Mode -in @('directory-data', 'metadata-directory-data')) {
                $writer = [IO.StreamWriter]::new($entry.Open())
                try { $writer.Write('not empty') } finally { $writer.Dispose() }
            }
            if ($Mode -eq 'streaming') {
                $writer = [IO.StreamWriter]::new($entry.Open())
                try { $writer.Write('0123456789abcdef' * 16384) } finally { $writer.Dispose() }
            }
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
    return $path
}
# Mutate real, small ZIP headers rather than mocking ZipArchiveEntry metadata.
function Set-FixtureMetadata([string] $Path, [string] $Mode, [int] $TargetIndex = 0) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $end = $bytes.Length - 22
    Assert ([BitConverter]::ToUInt32($bytes, $end) -eq 0x06054b50) 'Fixture has an unambiguous EOCD'
    $offset = [BitConverter]::ToUInt32($bytes, $end + 16)
    [long] $unchangedTotal = 0
    if ($Mode -eq 'total-byte-boundary') {
        $zip = [IO.Compression.ZipFile]::OpenRead($Path)
        try { for ($i = 4; $i -lt $zip.Entries.Count; ++$i) { $unchangedTotal += $zip.Entries[$i].Length } }
        finally { $zip.Dispose() }
    }
    $index = 0
    while ([BitConverter]::ToUInt32($bytes, $offset) -eq 0x02014b50) {
        $local = [BitConverter]::ToUInt32($bytes, $offset + 42)
        if ($Mode -eq 'encrypted' -and $index -eq 0) {
            $bytes[$offset + 8] = $bytes[$offset + 8] -bor 1
            $bytes[$local + 6] = $bytes[$local + 6] -bor 1
        }
        $length = $null
        if ($Mode -eq 'entry-bytes' -and $index -eq $TargetIndex) { $length = 536870913 }
        if ($Mode -eq 'entry-byte-boundary' -and $index -eq 0) { $length = 536870912 }
        if ($Mode -eq 'total-bytes' -and $index -lt 5) { $length = 536870912 }
        if ($Mode -eq 'total-byte-boundary' -and $index -lt 4) {
            $length = if ($index -eq 3) { 536870912 - $unchangedTotal } else { 536870912 }
        }
        if ($Mode -eq 'declared-size' -and $index -eq 0) { $length = [BitConverter]::ToUInt32($bytes, $offset + 24) + 1 }
        if ($Mode -eq 'declared-short' -and $index -eq 0) { $length = 1 }
        if ($Mode -eq 'bad-crc' -and $index -eq $TargetIndex) {
            $bytes[$offset + 16] = $bytes[$offset + 16] -bxor 1
            $bytes[$local + 14] = $bytes[$local + 14] -bxor 1
        }
        if ($null -ne $length) {
            [BitConverter]::GetBytes([uint32] $length).CopyTo($bytes, $offset + 24)
            [BitConverter]::GetBytes([uint32] $length).CopyTo($bytes, $local + 22)
        }
        $offset += 46 + [BitConverter]::ToUInt16($bytes, $offset + 28) +
            [BitConverter]::ToUInt16($bytes, $offset + 30) + [BitConverter]::ToUInt16($bytes, $offset + 32)
        ++$index
    }
    [IO.File]::WriteAllBytes($Path, $bytes)
}
$valid = New-Fixture 'valid'
[IO.File]::WriteAllBytes("$fixtureRoot\zero.nupkg", [byte[]]::new(0))
Expect-Failure { Read-GuestRuntimePackInventory "$fixtureRoot\zero.nupkg" 'android-arm64' $plan.version } 'Runtime pack exceeds the archive byte bound.'
$inventory = Read-GuestRuntimePackInventory $valid 'android-arm64' $plan.version
$crcInput = [Text.Encoding]::ASCII.GetBytes('123456789')
Assert ([GuestRuntimePack.Crc32]::Append(0, $crcInput, $crcInput.Length) -eq 0xcbf43926L) 'IEEE CRC32 known vector'
$crcPrefix = [Text.Encoding]::ASCII.GetBytes('1234')
$crcSuffix = [Text.Encoding]::ASCII.GetBytes('56789')
Assert ([GuestRuntimePack.Crc32]::Append([GuestRuntimePack.Crc32]::Append(0, $crcPrefix, 4), $crcSuffix, 5) -eq 0xcbf43926L) 'Incremental CRC32 known vector'
Assert ($inventory.entries.Count -eq 14 -and -not $inventory.signatureEntryPresent) 'Complete unsigned ZIP inventory'
Assert ($inventory.sha256 -ceq (Get-FileHash $valid).Hash.ToLowerInvariant()) 'Actual package byte identity'
Assert ($inventory.fileName -ceq 'valid.nupkg' -and $inventory.sizeBytes -eq (Get-Item $valid).Length) 'Actual filename and archive size'
Assert (($inventory.PSObject.Properties.Name | Sort-Object) -join ',' -ceq
    'entries,fileName,id,kind,rid,schemaVersion,sha256,signatureEntryPresent,sizeBytes,version') 'Exact shared inventory fields'
Assert ($inventory.schemaVersion -eq 1 -and $inventory.kind -ceq 'guest-runtime-package-inventory') 'Inventory schema identity'
foreach ($entry in $inventory.entries) {
    Assert ($entry.sha256 -cmatch '\A[0-9a-f]{64}\z' -and $entry.sizeBytes -gt 0) 'Per-entry hashes and sizes'
    Assert (($entry.PSObject.Properties.Name | Sort-Object) -join ',' -ceq 'path,sha256,sizeBytes') 'Exact shared entry fields'
}
[string[]] $sortedNames = @($inventory.entries.path)
[Array]::Sort($sortedNames, [StringComparer]::Ordinal)
Assert (($inventory.entries.path -join '|') -ceq ($sortedNames -join '|')) 'Ordinal entry order'
$failureCases = [ordered]@{
    'held' = 'Runtime pack ID/version does not match the explicit producer plan.'
    'missing' = 'Missing normal native asset: libmono-android.debug.so'
    'traversal' = 'Unsafe runtime pack entry path.'
    'wrong-rid' = 'Runtime pack contains assets outside the selected native RID.'
    'duplicate' = 'Duplicate normalized runtime pack path.'
    'unicode-alias' = 'Duplicate normalized runtime pack path.'
    'file-directory' = 'Duplicate normalized runtime pack path.'
    'parent-first' = 'Runtime pack file/directory collision.'
    'child-first' = 'Runtime pack file/directory collision.'
    'directory-data' = 'Noncanonical runtime pack directory.'
    'metadata-directory-data' = 'Noncanonical runtime pack directory.'
    'directory-flag' = 'Noncanonical runtime pack directory.'
    'symlink' = 'Symbolic link runtime pack entry.'
    'signature-case' = 'Noncanonical runtime pack signature member.'
    'signature-directory' = 'Noncanonical runtime pack signature member.'
    'entry-count' = 'Runtime pack exceeds the entry bound.'
}
foreach ($mode in @('empty-component', 'dot', 'absolute', 'backslash', 'drive', 'control', 'del', 'path-length', 'trailing-space', 'trailing-dot', 'device')) {
    $failureCases[$mode] = 'Unsafe runtime pack entry path.'
}
foreach ($mode in @('metadata-wrong-rid', 'metadata-wrong-tfm', 'metadata-wrong-case', 'metadata-extra-json',
    'metadata-managed-dll', 'metadata-nested', 'metadata-directory-leaf', 'metadata-file-parent', 'metadata-foreign-directory')) {
    $failureCases[$mode] = 'Runtime pack contains assets outside the selected native RID.'
}
foreach ($case in $failureCases.GetEnumerator()) {
    $fixture = New-Fixture $case.Key $case.Key
    Expect-Failure { Read-GuestRuntimePackInventory $fixture 'android-arm64' $plan.version } $case.Value
}
foreach ($case in @(
    @{ mode = 'encrypted'; error = 'Encrypted runtime pack entry.' },
    @{ mode = 'entry-bytes'; error = 'Runtime pack exceeds the entry byte bound.' },
    @{ mode = 'total-bytes'; error = 'Runtime pack exceeds the total byte bound.' },
    @{ mode = 'entry-byte-boundary'; error = 'Runtime pack entry does not match its declared size.' },
    @{ mode = 'total-byte-boundary'; error = 'Runtime pack entry does not match its declared size.' },
    @{ mode = 'declared-short'; error = 'Runtime pack entry CRC mismatch.' },
    @{ mode = 'bad-crc'; error = 'Runtime pack entry CRC mismatch.' },
    @{ mode = 'declared-size'; error = 'Runtime pack entry does not match its declared size.' }
)) {
    $fixture = New-Fixture $case.mode
    Set-FixtureMetadata $fixture $case.mode
    Expect-Failure { Read-GuestRuntimePackInventory $fixture 'android-arm64' $plan.version } $case.error
}
$streamingAssertions = 0
foreach ($mode in @('directories', 'path-boundary', 'count-boundary', 'streaming')) {
    $fixture = New-Fixture $mode $mode
    $result = Read-GuestRuntimePackInventory $fixture 'android-arm64' $plan.version
    if ($mode -eq 'count-boundary') { Assert ($result.entries.Count -eq 20000) 'Exact entry count bound accepted' }
    if ($mode -eq 'directories') {
        Assert (@($result.entries | Where-Object { $_.path.EndsWith('/') -and $_.sizeBytes -eq 0 }).Count -eq 4) 'Canonical directories inventoried'
    }
    if ($mode -eq 'streaming') {
        $expected = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes('0123456789abcdef' * 16384))).ToLowerInvariant()
        $entry = $result.entries | Where-Object { $_.path -ceq 'multiple-blocks' }
        Assert ($entry.sizeBytes -eq 262144 -and $entry.sha256 -ceq $expected) 'Multi-buffer streamed content hash'
        ++$streamingAssertions
    }
}
Assert ($streamingAssertions -eq 1) 'Existing streaming SHA assertion executes exactly once'
$metadataCases = 0
foreach ($rid in $plan.rids) {
    foreach ($metadataMode in @('host-metadata', 'host-metadata-directories')) {
        $fixture = New-Fixture "$rid-$metadataMode" $metadataMode $rid
        $result = Read-GuestRuntimePackInventory $fixture $rid $plan.version
        $zip = [IO.Compression.ZipFile]::OpenRead($fixture)
        try {
            foreach ($entry in $zip.Entries | Where-Object FullName -Match '\.(deps|runtimeconfig)\.json$') {
                $stream = $entry.Open()
                try { $digest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
                finally { $stream.Dispose() }
                $record = @($result.entries | Where-Object path -CEQ $entry.FullName)
                Assert ($record.Count -eq 1 -and $record[0].sha256 -ceq $digest -and $record[0].sizeBytes -eq $entry.Length) 'Host metadata fully inventoried, never skipped'
            }
        } finally { $zip.Dispose() }
        Assert ($result.entries.Count -eq $(if ($metadataMode -eq 'host-metadata') { 16 } else { 20 })) 'Exact metadata files and canonical ancestor directories accepted'
        ++$metadataCases
    }
    foreach ($fault in @('bad-crc', 'entry-bytes')) {
        $fixture = New-Fixture "$rid-metadata-$fault" 'host-metadata' $rid
        Set-FixtureMetadata $fixture $fault 15
        $expectedError = if ($fault -eq 'bad-crc') { 'Runtime pack entry CRC mismatch.' } else { 'Runtime pack exceeds the entry byte bound.' }
        Expect-Failure { Read-GuestRuntimePackInventory $fixture $rid $plan.version } $expectedError
    }
}
Assert ($metadataCases -eq 4) 'Host metadata matrix executes once per RID and layout'
Write-Host "PASS: streaming SHA assertions=$streamingAssertions; host metadata cases=$metadataCases."
foreach ($case in @(
    @{ leaf = ('a' * 234) + '.nupkg'; valid = $true },
    @{ leaf = ('a' * 235) + '.nupkg'; valid = $false },
    @{ leaf = 'bad name.nupkg'; valid = $false }
)) {
    $fixture = Join-Path $fixtureRoot $case.leaf
    Copy-Item -LiteralPath $valid -Destination $fixture -Force
    if ($case.valid) { Read-GuestRuntimePackInventory $fixture 'android-arm64' $plan.version | Out-Null }
    else { Expect-Failure { Read-GuestRuntimePackInventory $fixture 'android-arm64' $plan.version } 'Invalid runtime pack filename.' }
}
Expect-Failure { Read-GuestRuntimePackInventory $valid 'android-arm64' '036.1.69-guest.12345.1' } 'Expected version is not a canonical unique Android guest producer version.'
$signedFixture = New-Fixture 'signature' 'signature'
$signedInventory = Read-GuestRuntimePackInventory $signedFixture 'android-arm64' $plan.version
Assert $signedInventory.signatureEntryPresent 'Signature presence is not signature verification'
$inventory | ConvertTo-Json -Depth 8 | Set-Content "$fixtureRoot\synthetic-inventory-unsigned.json" -Encoding utf8
$signedInventory | ConvertTo-Json -Depth 8 | Set-Content "$fixtureRoot\synthetic-inventory-signature-present.json" -Encoding utf8
$verificationLog = "$fixtureRoot\synthetic-verification.log"
'Synthetic test output, not an actual signature verification.' | Set-Content $verificationLog -Encoding utf8
foreach ($case in @(
    @{ inventory = $inventory; code = 1; classification = 'unsigned' },
    @{ inventory = $signedInventory; code = 0; classification = 'signature-valid-policy-unqualified' },
    @{ inventory = $signedInventory; code = 1; classification = 'verification-failed' }
)) {
    $signature = New-GuestRuntimePackSignature $case.inventory $case.code 'synthetic-test-version' $verificationLog
    Assert ($signature.classification -ceq $case.classification -and $signature.verificationExitCode -eq $case.code) 'Signature result preserves verifier outcome'
    Assert ($signature.packageSha256 -ceq $case.inventory.sha256 -and
        $signature.outputSha256 -ceq (Get-FileHash $verificationLog).Hash.ToLowerInvariant()) 'Exact archive and output hash binding'
    Assert ($signature.arguments -join ',' -ceq "nuget,verify,--all,$($case.inventory.fileName)" -and
        $signature.outputFileName -ceq 'synthetic-verification.log' -and
        $signature.toolVersion -ceq 'synthetic-test-version') 'Verifier invocation and output identity'
    Assert (($signature.PSObject.Properties.Name | Sort-Object) -join ',' -ceq
        'arguments,classification,kind,outputFileName,outputSha256,packageSha256,schemaVersion,signatureEntryPresent,toolVersion,verificationExitCode') 'Exact shared signature fields'
    $signature | ConvertTo-Json -Depth 6 | Set-Content "$fixtureRoot\synthetic-signature-$($case.classification).json" -Encoding utf8
}

$commandReceiptPath = "$fixtureRoot\actual-command-test-receipt.json"
$commandReceipt = [pscustomobject]@{
    schema = 2; status = 'running'; failure = $null; packageAdmission = $false
    commands = [Collections.Generic.List[object]]::new()
}
$shell = (Get-Command pwsh -CommandType Application).Source
$arguments = @('-NoProfile', '-Command', "[Console]::Out.WriteLine('stdout fixture'); [Console]::Error.WriteLine('stderr fixture'); Write-Output (Get-Location).Path; exit 0")
$result = Invoke-GuestProducerCommand -Name 'success' -Tool $shell -Arguments $arguments -WorkingDirectory $fixtureRoot `
    -Receipt $commandReceipt -ReceiptPath $commandReceiptPath
Assert ($result.exitCode -eq 0 -and $result.result -ceq 'exited-zero') 'Actual successful process result'
Assert (($result.arguments -join '|') -ceq ($arguments -join '|') -and $result.resolvedTool -ceq $shell) 'Actual executable and argument binding'
$outputText = Get-Content "$fixtureRoot\success.log" -Raw
Assert ($outputText.Contains('stdout fixture') -and $outputText.Contains('stderr fixture')) 'Both process output streams retained'
Assert ($outputText.Contains($fixtureRoot)) 'Actual working directory matches receipt'
Copy-Item $commandReceiptPath "$fixtureRoot\actual-command-success-receipt.json" -Force
Expect-Failure {
    Invoke-GuestProducerCommand -Name 'nonzero' -Tool $shell -Arguments @('-NoProfile', '-Command', "Write-Output 'failed-stage fixture'; exit 7") `
        -WorkingDirectory $fixtureRoot -Receipt $commandReceipt -ReceiptPath $commandReceiptPath
} 'Producer command failed: nonzero (exited-nonzero).'
$persisted = Get-Content $commandReceiptPath -Raw | ConvertFrom-Json
Assert ($persisted.status -ceq 'failed' -and $persisted.failure.stage -ceq 'nonzero' -and
    $persisted.commands.Count -eq 2 -and $persisted.commands[1].exitCode -eq 7) 'Failed stage and prior result persisted before throw'
Copy-Item $commandReceiptPath "$fixtureRoot\actual-command-nonzero-receipt.json" -Force
Expect-Failure {
    Invoke-GuestProducerCommand -Name 'missing-tool' -Tool "$fixtureRoot\does-not-exist.exe" -WorkingDirectory $fixtureRoot `
        -Receipt $commandReceipt -ReceiptPath $commandReceiptPath
} 'Producer command failed: missing-tool (invocation-failed).'
$persisted = Get-Content $commandReceiptPath -Raw | ConvertFrom-Json
Assert ($persisted.commands.Count -eq 3 -and $null -eq $persisted.commands[2].exitCode -and
    $null -eq $persisted.commands[2].resolvedTool -and $persisted.commands[2].error) 'No fabricated process result on invocation failure'
foreach ($command in $persisted.commands) {
    Assert ($command.outputSha256 -ceq (Get-FileHash (Join-Path $fixtureRoot $command.outputFileName)).Hash.ToLowerInvariant()) 'Actual raw combined log hash'
    Assert ([DateTime]::Parse($command.endedAtUtc) -ge [DateTime]::Parse($command.startedAtUtc)) 'Actual command time bracket'
}
$allowedReceipt = [pscustomobject]@{
    schema = 2; status = 'running'; failure = $null; packageAdmission = $false
    commands = [Collections.Generic.List[object]]::new()
}
$allowed = Invoke-GuestProducerCommand -Name 'allowed-nonzero' -Tool $shell -Arguments @('-NoProfile', '-Command', 'exit 9') `
    -WorkingDirectory $fixtureRoot -Receipt $allowedReceipt -ReceiptPath "$fixtureRoot\actual-allowed-nonzero-receipt.json" -AllowNonzeroExit
Assert ($allowed.result -ceq 'exited-nonzero' -and $allowed.exitCode -eq 9 -and
    $allowedReceipt.status -ceq 'running' -and $null -eq $allowedReceipt.failure) 'Allowed verifier failure preserves nonzero result, never success'

$preview = & "$root\build-tools\scripts\guest-readiness-runtime-pack.ps1" -BuildNumber 12345 -Attempt 1 | ConvertFrom-Json
Assert ($LASTEXITCODE -eq 0 -and $preview.commands.Count -eq 4) 'Default entry only returns a plan'
Write-Output 'Producer input gates, actual malformed ZIP semantics/bounds, shared sidecars and real command success/failure receipts passed (synthetic packages only).'
exit 0
