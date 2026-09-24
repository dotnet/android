#requires -Version 7.3
Set-StrictMode -Version Latest

function Get-GuestRuntimePackIdentity {
    param ([Parameter(Mandatory)][string] $BuildNumber, [Parameter(Mandatory)][string] $Attempt)
    if ($BuildNumber -cnotmatch '\A[1-9][0-9]{0,11}\z' -or $Attempt -cnotmatch '\A[1-9][0-9]{0,3}\z') {
        throw 'Build number and attempt must be bounded positive decimal identities.'
    }
    [pscustomobject]@{ version = "36.1.69-guest.$BuildNumber.$Attempt"; buildId = "android-d549-$BuildNumber-$Attempt" }
}

function New-GuestRuntimePackPlan {
    param (
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [Parameter(Mandatory)][string] $BuildNumber,
        [Parameter(Mandatory)][string] $Attempt
    )
    $identity = Get-GuestRuntimePackIdentity $BuildNumber $Attempt
    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $version = $identity.version
    $marker = $identity.buildId
    $output = Join-Path $root 'bin\guest-readiness-producer'
    $common = @(
        '-c', 'Release', '-nodeReuse:false', '-p:RunningOnCI=true',
        "-p:AndroidPackVersionLong=$version", "-p:PackageVersion=$version",
        "-p:AndroidStartupDiagnosticsBuildId=$marker",
        '-p:AndroidSupportedTargetJitAbis=arm64-v8a:x86_64',
        "-p:AndroidToolchainDirectory=$root\bin\guest-readiness-toolchain",
        "-p:AndroidToolchainCacheDirectory=$root\bin\guest-readiness-archives"
    )
    $commands = @(
        [pscustomobject]@{
            name = 'prepare'; tool = 'dotnet'
            arguments = @('build', "$root\Xamarin.Android.sln", '-t:Prepare', '-p:AutoProvision=true') + $common + @("-bl:$output\prepare.binlog")
        },
        [pscustomobject]@{
            name = 'build'; tool = "$root\dotnet-local.cmd"
            arguments = @('build', "$root\Xamarin.Android.sln") + $common + @("-bl:$output\build.binlog")
        }
    )
    foreach ($rid in @('android-arm64', 'android-x64')) {
        $commands += [pscustomobject]@{
            name = "pack-$rid"; tool = "$root\dotnet-local.cmd"
            arguments = @('build', "$root\build-tools\create-packs\Microsoft.Android.Runtime.proj",
                '-p:AndroidRuntime=Mono', '-p:AndroidApiLevel=36', "-p:AndroidRID=$rid",
                "-p:OutputPath=$output\packages\") + $common + @("-bl:$output\pack-$rid.binlog")
        }
    }
    [pscustomobject]@{
        schema = 1
        baseline = 'd549e1dc4e2a083b08b4f24cb5495e81b99d79b5'
        version = $version
        buildId = $marker
        runtime = 'Mono'
        apiLevel = 36
        rids = @('android-arm64', 'android-x64')
        artifactDirectory = $output
        commands = $commands
        admission = 'not-qualified'
    }
}

function Read-GuestRuntimePackInventory {
    param (
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][ValidateSet('android-arm64', 'android-x64')][string] $Rid,
        [Parameter(Mandatory)][string] $Version
    )
    if ($Version -cnotmatch '\A36\.1\.69-guest\.[1-9][0-9]{0,11}\.[1-9][0-9]{0,3}\z') {
        throw 'Expected version is not a canonical unique Android guest producer version.'
    }
    $leaf = [IO.Path]::GetFileName($Path)
    if ($leaf.Length -gt 240 -or $leaf -cnotmatch '\A[A-Za-z0-9][A-Za-z0-9._-]*\z') {
        throw 'Invalid runtime pack filename.'
    }
    $archive = [IO.File]::OpenRead($Path)
    try {
    $archiveSize = $archive.Length
    if ($archiveSize -lt 1 -or $archiveSize -gt 2147483648) { throw 'Runtime pack exceeds the archive byte bound.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # The bootstrap Force.Crc32 dependency is unavailable before Prepare. Check the ZIP's
    # IEEE CRC32 locally because framework readers can clamp reads to a forged short size.
    if (-not ('GuestRuntimePack.Crc32' -as [type])) {
        Add-Type -TypeDefinition @'
namespace GuestRuntimePack {
	public static class Crc32 {
		static readonly uint [] table = CreateTable ();

		static uint [] CreateTable ()
		{
			var values = new uint [256];
			for (uint i = 0; i < values.Length; ++i) {
				uint value = i;
				for (int bit = 0; bit < 8; ++bit)
					value = (value >> 1) ^ ((value & 1) != 0 ? 0xedb88320u : 0);
				values [i] = value;
			}
			return values;
		}

		public static uint Append (uint crc, byte [] buffer, int count)
		{
			if (buffer == null || count < 0 || count > buffer.Length)
				throw new System.ArgumentException ("Invalid CRC input.");
			uint value = ~crc;
			for (int i = 0; i < count; ++i)
				value = (value >> 8) ^ table [(value ^ buffer [i]) & 255];
			return ~value;
		}
	}
}
'@
    }
    $zip = [IO.Compression.ZipArchive]::new($archive, [IO.Compression.ZipArchiveMode]::Read, $true)
    try {
        if ($zip.Entries.Count -gt 20000) { throw 'Runtime pack exceeds the entry bound.' }
        $paths = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::OrdinalIgnoreCase)
        $members = [Collections.Generic.Dictionary[string, IO.Compression.ZipArchiveEntry]]::new([StringComparer]::Ordinal)
        $entries = [Collections.Generic.List[object]]::new()
        $nuspec = $null
        [long] $total = 0
        # Validate the whole metadata set before opening any decompression stream.
        # Existing bootstrap ZIP helpers extract files; this inventory deliberately never extracts.
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ($name.Length -eq 0 -or $name.Length -gt 1024 -or $name -match '[\x00-\x1f\x7f\\:]' -or $name.StartsWith('/')) {
                throw 'Unsafe runtime pack entry path.'
            }
            $directory = $name.EndsWith('/')
            $pathKey = if ($directory) { $name.Substring(0, $name.Length - 1) } else { $name }
            $parts = $pathKey.Split('/')
            foreach ($part in $parts) {
                if ($part.Length -eq 0 -or $part -in @('.', '..') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or
                    $part -match '\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|\z)') {
                    throw 'Unsafe runtime pack entry path.'
                }
            }
            $pathKey = $pathKey.Normalize([Text.NormalizationForm]::FormC)
            if ($paths.ContainsKey($pathKey)) { throw 'Duplicate normalized runtime pack path.' }
            $paths.Add($pathKey, $directory)
            $members.Add($name, $entry)
            if ($entry.IsEncrypted) { throw 'Encrypted runtime pack entry.' }
            $fileType = ($entry.ExternalAttributes -shr 16) -band 0xf000
            if ($fileType -eq 0xa000) { throw 'Symbolic link runtime pack entry.' }
            if ($fileType -notin @(0, 0x8000, 0x4000)) { throw 'Unsupported runtime pack entry type.' }
            if (($fileType -eq 0x4000 -or ($entry.ExternalAttributes -band 0x10) -ne 0) -and -not $directory) {
                throw 'Noncanonical runtime pack directory.'
            }
            if ($directory -and ($entry.Length -ne 0 -or $fileType -eq 0x8000)) { throw 'Noncanonical runtime pack directory.' }
            if ($pathKey.Equals('.signature.p7s', [StringComparison]::OrdinalIgnoreCase) -and $name -cne '.signature.p7s') {
                throw 'Noncanonical runtime pack signature member.'
            }
            if ($entry.Length -lt 0 -or $entry.Length -gt 536870912) { throw 'Runtime pack exceeds the entry byte bound.' }
            $total += $entry.Length
            if ($total -gt 2147483648) { throw 'Runtime pack exceeds the total byte bound.' }
            if ($name.StartsWith('runtimes/', [StringComparison]::OrdinalIgnoreCase)) {
                $nativeRoot = "runtimes/$Rid/native/"
                if (-not $name.StartsWith($nativeRoot, [StringComparison]::Ordinal) -and
                    -not ($directory -and $nativeRoot.StartsWith($name, [StringComparison]::Ordinal))) {
                    throw 'Runtime pack contains assets outside the selected native RID.'
                }
            }
        }
        foreach ($key in $paths.Keys) {
            $parent = $key
            while ($parent.Contains('/')) {
                $parent = $parent.Substring(0, $parent.LastIndexOf('/'))
                if ($paths.ContainsKey($parent) -and -not $paths[$parent]) { throw 'Runtime pack file/directory collision.' }
            }
        }
        [string[]] $orderedNames = @($members.Keys)
        [Array]::Sort($orderedNames, [StringComparer]::Ordinal)
        $buffer = [byte[]]::new(65536)
        foreach ($name in $orderedNames) {
            $entry = $members[$name]
            $stream = $entry.Open()
            try {
                $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
                try {
                    [long] $read = 0
                    [uint32] $crc = 0
                    while (($count = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        $read += $count
                        if ($read -gt $entry.Length) { throw 'Runtime pack entry exceeds its declared size.' }
                        $hash.AppendData($buffer, 0, $count)
                        $crc = [GuestRuntimePack.Crc32]::Append($crc, $buffer, $count)
                    }
                    if ($read -ne $entry.Length) { throw 'Runtime pack entry does not match its declared size.' }
                    if ($crc -ne $entry.Crc32) { throw 'Runtime pack entry CRC mismatch.' }
                    $digest = [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
                }
                finally { $hash.Dispose() }
            } finally { $stream.Dispose() }
            $entries.Add([pscustomobject]@{ path = $entry.FullName; sizeBytes = $entry.Length; sha256 = $digest })
            if ($entry.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase)) {
                if ($null -ne $nuspec -or $entry.Length -gt 1048576) { throw 'Invalid nuspec count or size.' }
                $settings = [Xml.XmlReaderSettings]::new()
                $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
                $settings.XmlResolver = $null
                $stream = $entry.Open()
                try {
                    $reader = [Xml.XmlReader]::Create($stream, $settings)
                    try {
                        $nuspec = [Xml.XmlDocument]::new()
                        $nuspec.XmlResolver = $null
                        $nuspec.Load($reader)
                    } finally { $reader.Dispose() }
                } finally { $stream.Dispose() }
            }
        }
        if ($null -eq $nuspec) { throw 'Runtime pack nuspec is missing.' }
        $ids = $nuspec.SelectNodes("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='id']")
        $versions = $nuspec.SelectNodes("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='version']")
        $expectedId = "Microsoft.Android.Runtime.Mono.36.$Rid"
        if ($ids.Count -ne 1 -or $versions.Count -ne 1 -or $ids[0].InnerText -cne $expectedId -or $versions[0].InnerText -cne $Version) {
            throw 'Runtime pack ID/version does not match the explicit producer plan.'
        }
        # This is the normal Mono native asset closure in Microsoft.Android.Runtime.proj,
        # including the archive stub produced by the ordinary native build.
        foreach ($asset in @('libmono-android.debug.so', 'libmono-android.release.so', 'libxamarin-debug-app-helper.so',
            'libxamarin-native-tracing.so', 'libunwind_xamarin-debug.a', 'libunwind_xamarin-release.a',
            'libc.so', 'libdl.so', 'liblog.so', 'libm.so', 'libz.so', 'libarchive-dso-stub.so')) {
            if (-not $members.ContainsKey("runtimes/$Rid/native/$asset")) { throw "Missing normal native asset: $asset" }
        }
        if (-not $members.ContainsKey('data/RuntimeList.xml')) { throw 'Normal runtime-list manifest is missing.' }
        $archive.Position = 0
        $archiveHash = [Security.Cryptography.SHA256]::Create()
        try { $archiveDigest = [Convert]::ToHexString($archiveHash.ComputeHash($archive)).ToLowerInvariant() }
        finally { $archiveHash.Dispose() }
        [pscustomobject]@{
            schemaVersion = 1; kind = 'guest-runtime-package-inventory'
            id = $expectedId; version = $Version; rid = $Rid
            fileName = $leaf
            sha256 = $archiveDigest
            sizeBytes = $archiveSize
            signatureEntryPresent = $members.ContainsKey('.signature.p7s')
            entries = @($entries.ToArray())
        }
    } finally { $zip.Dispose() }
    } finally { $archive.Dispose() }
}

# Keep signature evidence distinct from the ZIP inventory: presence is not verification or trust.
function New-GuestRuntimePackSignature {
    param (
        [Parameter(Mandatory)][psobject] $Inventory,
        [Parameter(Mandatory)][int] $VerificationExitCode,
        [Parameter(Mandatory)][string] $ToolVersion,
        [Parameter(Mandatory)][string] $OutputPath
    )
    $classification = if (-not $Inventory.signatureEntryPresent) { 'unsigned' }
        elseif ($VerificationExitCode -eq 0) { 'signature-valid-policy-unqualified' }
        else { 'verification-failed' }
    [pscustomobject]@{
        schemaVersion = 1; kind = 'guest-runtime-package-signature'
        packageSha256 = $Inventory.sha256
        signatureEntryPresent = [bool] $Inventory.signatureEntryPresent
        verificationExitCode = $VerificationExitCode
        classification = $classification
        toolVersion = $ToolVersion
        arguments = @('nuget', 'verify', '--all', $Inventory.fileName)
        outputFileName = [IO.Path]::GetFileName($OutputPath)
        outputSha256 = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

# A single writer is used for both intermediate failure receipts and the final unadmitted receipt.
function Write-GuestProducerReceipt {
    param ([Parameter(Mandatory)][psobject] $Receipt, [Parameter(Mandatory)][string] $Path)
    [IO.File]::WriteAllText("$Path.tmp", ($Receipt | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    [IO.File]::Move("$Path.tmp", $Path, $true)
}

function Invoke-GuestProducerCommand {
    param (
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Tool,
        [string[]] $Arguments = @(),
        [Parameter(Mandatory)][string] $WorkingDirectory,
        [Parameter(Mandatory)][psobject] $Receipt,
        [Parameter(Mandatory)][string] $ReceiptPath,
        [switch] $AllowNonzeroExit
    )
    $ErrorActionPreference = 'Stop'
    if ($Name -cnotmatch '\A[a-z0-9][a-z0-9-]{0,63}\z') { throw 'Invalid producer command name.' }
    if (@($Receipt.commands | Where-Object { $_.name -ceq $Name }).Count -ne 0) { throw 'Duplicate producer command name.' }
    $log = Join-Path (Split-Path $ReceiptPath) "$Name.log"
    $command = [pscustomobject]@{
        name = $Name; requestedTool = $Tool; resolvedTool = $null
        arguments = @($Arguments); workingDirectory = [IO.Path]::GetFullPath($WorkingDirectory)
        startedAtUtc = [DateTime]::UtcNow.ToString('O'); endedAtUtc = $null
        result = 'invocation-failed'; exitCode = $null; error = $null
        outputFileName = [IO.Path]::GetFileName($log); outputSha256 = $null
        outputStreams = 'combined stdout/stderr'
    }
    $writer = [IO.StreamWriter]::new($log, $false, [Text.UTF8Encoding]::new($false))
    $locationPushed = $false
    try {
        $command.resolvedTool = (Get-Command $Tool -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
        Push-Location -LiteralPath $command.workingDirectory -ErrorAction Stop
        $locationPushed = $true
        $PSNativeCommandUseErrorActionPreference = $false
        & $command.resolvedTool @Arguments 2>&1 | ForEach-Object {
            $writer.WriteLine([string] $_)
            Write-Host $_
        }
        if ($null -eq $LASTEXITCODE) { throw 'Native command did not report an exit code.' }
        $command.exitCode = $LASTEXITCODE
        $command.result = if ($LASTEXITCODE -eq 0) { 'exited-zero' } else { 'exited-nonzero' }
    } catch {
        $command.error = $_.Exception.Message
        $writer.WriteLine($command.error)
    } finally {
        if ($locationPushed) { Pop-Location }
        $writer.Dispose()
    }
    $command.endedAtUtc = [DateTime]::UtcNow.ToString('O')
    $command.outputSha256 = (Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash.ToLowerInvariant()
    $Receipt.commands.Add($command)
    $failed = $command.result -eq 'invocation-failed' -or ($command.exitCode -ne 0 -and -not $AllowNonzeroExit)
    if ($failed) {
        $Receipt.status = 'failed'
        $Receipt.failure = [pscustomobject]@{ stage = $Name; message = "Producer command failed: $Name ($($command.result))." }
    }
    Write-GuestProducerReceipt $Receipt $ReceiptPath
    if ($failed) { throw $Receipt.failure.message }
    return $command
}

function Get-GuestRuntimePackMemberDelta {
    param ([Parameter(Mandatory)][psobject] $InputInventory, [Parameter(Mandatory)][psobject] $OutputInventory)
    if ($InputInventory.id -cne $OutputInventory.id -or $InputInventory.version -cne $OutputInventory.version -or
        $InputInventory.rid -cne $OutputInventory.rid) { throw 'Package identity changed during signing.' }
    $before = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $after = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($entry in $InputInventory.entries) { $before.Add($entry.path, $entry) }
    foreach ($entry in $OutputInventory.entries) { $after.Add($entry.path, $entry) }
    $names = [Collections.Generic.HashSet[string]]::new($before.Keys, [StringComparer]::Ordinal)
    $names.UnionWith($after.Keys)
    [string[]] $ordered = @($names)
    [Array]::Sort($ordered, [StringComparer]::Ordinal)
    foreach ($name in $ordered) {
        $a = if ($before.ContainsKey($name)) { $before[$name] } else { $null }
        $b = if ($after.ContainsKey($name)) { $after[$name] } else { $null }
        if ($null -ne $a -and $null -ne $b -and $a.sha256 -ceq $b.sha256 -and $a.sizeBytes -eq $b.sizeBytes) { continue }
        [pscustomobject]@{
            path = $name
            change = $(if ($null -eq $a) { 'added' } elseif ($null -eq $b) { 'removed' } else { 'changed' })
            input = $a; output = $b
        }
    }
}

function Read-GuestRuntimePackNativeCarriers {
    param ([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][psobject] $Inventory,
        [Parameter(Mandatory)][string] $BuildMarker)
    if ($BuildMarker -cnotmatch '\Aandroid-d549-[1-9][0-9]{0,11}-[1-9][0-9]{0,3}\z') { throw 'Invalid native build marker.' }
    $machine = switch ($Inventory.rid) { 'android-arm64' { 183 } 'android-x64' { 62 } default { throw 'Unsupported native RID.' } }
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() -cne $Inventory.sha256) {
            throw 'Native archive differs from inventory.'
        }
        $stream.Position = 0
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read, $true)
        try {
            foreach ($configuration in @('debug', 'release')) {
                # These are the existing mono/monodroid CMake OUTPUT_NAMEs; do not scan arbitrary package binaries.
                $name = "runtimes/$($Inventory.rid)/native/libmono-android.$configuration.so"
                $entry = $zip.GetEntry($name)
                $record = @($Inventory.entries | Where-Object { $_.path -ceq $name })
                if ($null -eq $entry -or $record.Count -ne 1 -or $entry.Length -lt 64 -or $entry.Length -gt 536870912) {
                    throw 'Missing bounded native carrier.'
                }
                $contents = $entry.Open()
                $hasher = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
                try {
                    $bytes = [byte[]]::new(64)
                    $contents.ReadExactly($bytes)
                    $hasher.AppendData($bytes)
                    $marker = "$BuildMarker`0"
                    $carry = [Text.Encoding]::Latin1.GetString($bytes)
                    $found = $carry.Contains($marker, [StringComparison]::Ordinal)
                    $total = 64L
                    $buffer = [byte[]]::new(65536)
                    while (($count = $contents.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        $total += $count
                        if ($total -gt $entry.Length) { throw 'Native carrier exceeds declared length.' }
                        $hasher.AppendData($buffer, 0, $count)
                        $window = $carry + [Text.Encoding]::Latin1.GetString($buffer, 0, $count)
                        $found = $found -or $window.Contains($marker, [StringComparison]::Ordinal)
                        $carry = $window.Substring([Math]::Max(0, $window.Length - $marker.Length + 1))
                    }
                    $hash = [Convert]::ToHexString($hasher.GetHashAndReset()).ToLowerInvariant()
                } finally { $hasher.Dispose(); $contents.Dispose() }
                if ($hash -cne $record[0].sha256 -or $total -ne $record[0].sizeBytes) { throw 'Native carrier differs from inventory.' }
                # No PowerShell ELF reader is available before Prepare; decode only the fixed ELF64-LE header.
                if ($bytes[0] -ne 127 -or $bytes[1] -ne 69 -or $bytes[2] -ne 76 -or $bytes[3] -ne 70 -or
                    $bytes[4] -ne 2 -or $bytes[5] -ne 1 -or $bytes[6] -ne 1 -or
                    ($bytes[16] + 256 * $bytes[17]) -ne 3 -or ($bytes[18] + 256 * $bytes[19]) -ne $machine) {
                    throw 'Native carrier is not the expected ELF64 little-endian shared object.'
                }
                if (-not $found) {
                    throw 'Native carrier lacks the compiled build marker.'
                }
                [pscustomobject]@{
                    path = $name; sizeBytes = $total; sha256 = $hash; elfClass = 2; endianness = 1; machine = $machine
                    gnuBuildId = $null; gnuBuildIdStatus = 'not-collected'; markerMatch = 'nul-terminated'
                }
            }
        } finally { $zip.Dispose() }
    } finally { $stream.Dispose() }
}

function New-GuestRuntimePackagePostSign {
    param (
        [Parameter(Mandatory)][psobject] $InputInventory, [Parameter(Mandatory)][psobject] $OutputInventory,
        [Parameter(Mandatory)][psobject] $Signature, [Parameter(Mandatory)][psobject] $Source,
        [Parameter(Mandatory)][psobject] $BuildReceipt, [Parameter(Mandatory)][psobject] $InputReference,
        [Parameter(Mandatory)][psobject] $OutputReference, [Parameter(Mandatory)][psobject] $SignatureReference,
        [Parameter(Mandatory)][psobject] $Signer, [Parameter(Mandatory)][object[]] $OperationEvidence,
        [Parameter(Mandatory)][psobject] $MemberDelta, [Parameter(Mandatory)][psobject] $NativeProvenance
    )
    if ($InputInventory.id -cne $OutputInventory.id -or $InputInventory.rid -cne $OutputInventory.rid -or
        $InputInventory.version -cne $OutputInventory.version -or $Signature.packageSha256 -cne $OutputInventory.sha256) {
        throw 'Post-sign package identities do not agree.'
    }
    [pscustomobject]@{
        schemaVersion = 1; kind = 'guest-runtime-package-postsign'
        id = $OutputInventory.id; rid = $OutputInventory.rid; version = $OutputInventory.version
        status = $(if ($Signature.signatureEntryPresent -and $Signature.verificationExitCode -eq 0) { 'verified-policy-unqualified' } else { 'produced-verification-failed' })
        source = $Source; buildReceipt = $BuildReceipt
        input = [pscustomobject]@{ fileName = $InputInventory.fileName; sizeBytes = $InputInventory.sizeBytes; sha256 = $InputInventory.sha256; inventory = $InputReference }
        output = [pscustomobject]@{ fileName = $OutputInventory.fileName; sizeBytes = $OutputInventory.sizeBytes; sha256 = $OutputInventory.sha256; inventory = $OutputReference; signature = $SignatureReference }
        signer = $Signer; operationEvidence = $OperationEvidence; memberDelta = $MemberDelta; nativeProvenance = $NativeProvenance
    }
}

Export-ModuleMember -Function Get-GuestRuntimePackIdentity, New-GuestRuntimePackPlan, Read-GuestRuntimePackInventory, New-GuestRuntimePackSignature, Write-GuestProducerReceipt, Invoke-GuestProducerCommand, Get-GuestRuntimePackMemberDelta, Read-GuestRuntimePackNativeCarriers, New-GuestRuntimePackagePostSign
