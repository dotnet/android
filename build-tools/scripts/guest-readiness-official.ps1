#requires -Version 7.3
param ([Parameter(Mandatory)][ValidateSet('Validate', 'Build', 'Pack', 'Input', 'Output')][string] $Phase)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/guest-readiness-runtime-pack.psm1" -Force
function Copy-GuestReceiptFile {
    param ([string] $Source, [string] $Destination)
    $hash = (Get-FileHash -LiteralPath $Source).Hash.ToLowerInvariant()
    if (Test-Path -LiteralPath $Destination) {
        if ((Get-FileHash -LiteralPath $Destination).Hash.ToLowerInvariant() -cne $hash) {
            throw 'Retained receipt file collides with different bytes.'
        }
    } else {
        Copy-Item -LiteralPath $Source -Destination $Destination
    }
    if (-not (Test-Path -LiteralPath $Destination) -or
        (Get-FileHash -LiteralPath $Destination).Hash.ToLowerInvariant() -cne $hash) {
        throw 'Retained receipt file differs from its source.'
    }
    [pscustomobject]@{ fileName = [IO.Path]::GetFileName($Destination); sizeBytes = (Get-Item -LiteralPath $Destination).Length; sha256 = $hash }
}
function Get-VerifiedGuestBuildReceipt {
    param ([string] $Path, [psobject] $Expected, [string[]] $BuildArguments)
    $build = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($build.schema -ne 2 -or $build.kind -cne 'android-official-guest-readiness-phase' -or
        $build.phase -cne 'Build' -or $build.status -cne 'completed-unadmitted' -or
        $null -ne $build.failure -or $build.commands.Count -lt 3) { throw 'Earlier Build receipt is not completed.' }
    foreach ($name in @('component', 'baseline', 'sourceCommit', 'version', 'buildId', 'patchSha256',
        'definitionId', 'pipelinePath', 'sourceBranch', 'buildNumber', 'requestedSignType')) {
        if ($build.$name -cne $Expected.$name) { throw "Earlier Build receipt identity differs: $name" }
    }
    if (($build.templates | ConvertTo-Json -Compress) -cne ($Expected.templates | ConvertTo-Json -Compress)) {
        throw 'Earlier Build resolved templates differ.'
    }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($command in $build.commands) {
        if (-not $names.Add($command.name) -or $command.result -cne 'exited-zero' -or $command.exitCode -ne 0 -or
            $null -ne $command.error -or $command.resolvedTool -isnot [string] -or
            [string]::IsNullOrWhiteSpace($command.resolvedTool) -or
            $command.outputFileName -cnotmatch '\Abuild-[A-Za-z0-9._-]+\.log\z' -or
            $command.outputSha256 -cnotmatch '\A[0-9a-f]{64}\z') { throw 'Earlier Build command did not complete successfully.' }
        $start = [DateTimeOffset]::Parse($command.startedAtUtc, [Globalization.CultureInfo]::InvariantCulture)
        $end = [DateTimeOffset]::Parse($command.endedAtUtc, [Globalization.CultureInfo]::InvariantCulture)
        if ($end -lt $start) { throw 'Earlier Build command timestamps are reversed.' }
        $log = Join-Path (Split-Path $Path) $command.outputFileName
        if ((Get-FileHash -LiteralPath $log).Hash.ToLowerInvariant() -cne $command.outputSha256) {
            throw 'Earlier Build command log differs from its receipt.'
        }
    }
    foreach ($name in @('build-source-patch', 'build-submodules', 'build-make-jenkins')) {
        if (-not $names.Contains($name)) { throw 'Earlier Build command ledger is incomplete.' }
    }
    $make = @($build.commands | Where-Object { $_.name -ceq 'build-make-jenkins' })[0]
    if ($make.requestedTool -cne 'make' -or
        ($make.arguments | ConvertTo-Json -Compress) -cne ($BuildArguments | ConvertTo-Json -Compress)) {
        throw 'Earlier Build command did not use the expected normal build properties.'
    }
    return $build
}
function Save-GuestRuntimePackCandidates {
    param ([string] $Directory, [string] $Destination, [string] $BuildNumber, [string] $Attempt)
    $expected = Get-GuestRuntimePackIdentity $BuildNumber $Attempt
    $references = @(
        foreach ($rid in @('android-arm64', 'android-x64')) {
            $leaf = "Microsoft.Android.Runtime.Mono.36.$rid.$($expected.version).nupkg"
            $source = Join-Path $Directory $leaf
            $file = Get-Item -LiteralPath $source
            if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                $file.Length -lt 1 -or $file.Length -gt 2147483648) {
                throw 'Runtime pack candidate must be a bounded regular archive file.'
            }
            Copy-GuestReceiptFile $source (Join-Path $Destination $leaf)
        }
    )
    $sidecar = Join-Path $Destination 'unadmitted-runtime-candidates.json'
    Write-GuestProducerReceipt ([pscustomobject]@{
        schemaVersion = 1; kind = 'android-unadmitted-runtime-candidates'
        status = 'unadmitted'; version = $expected.version; files = $references
        notice = 'Raw normal pack outputs retained before layout, native-marker or signature validation. Not qualified packages.'
    }) $sidecar
    $references
    Get-FileReference $sidecar
}
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$baseline = 'd549e1dc4e2a083b08b4f24cb5495e81b99d79b5'
$identity = Get-GuestRuntimePackIdentity $env:BUILD_BUILDID $env:GUEST_ATTEMPT
# This mode must take the existing Test branch, never change the signing predicate.
if ($env:BUILD_REASON -cne 'Manual' -or $env:BUILD_DEFINITIONNAME -cne 'Xamarin.Android' -or
    $env:SYSTEM_DEFINITIONID -cne '11410' -or $env:BUILD_REPOSITORY_NAME -cne 'dotnet/android' -or
    ([string] $env:BUILD_SOURCEBRANCH).StartsWith('refs/heads/release/', [StringComparison]::OrdinalIgnoreCase) -or
    -not ([string] $env:BUILD_SOURCEBRANCH).StartsWith('refs/heads/', [StringComparison]::Ordinal) -or
    $env:GUEST_SKIP_COMPLIANCE -ine 'false') {
    throw 'Guest readiness requires manual definition11410, upstream branch, Test signing and unchanged compliance.'
}
foreach ($value in @($env:BUILD_SOURCEVERSION, $env:GUEST_YAML_VERSION, $env:GUEST_1ES_VERSION)) {
    if ($value -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Exact source and resolved template commits are required.' }
}
if ($env:GUEST_CONFIGURATION -cnotmatch '\A(Debug|Release)\z') { throw 'Invalid build configuration.' }
foreach ($value in @($env:SYSTEM_DEFINITIONID, $env:BUILD_BUILDID, $env:SYSTEM_JOBATTEMPT)) {
    $numeric = 0
    if ($value -cnotmatch '\A[1-9][0-9]*\z' -or -not [int]::TryParse($value, [ref] $numeric)) { throw 'Invalid provider numeric identity.' }
}
$head = & git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head -cne $env:BUILD_SOURCEVERSION) { throw 'Checkout differs from pipeline source.' }
& git -C $root merge-base --is-ancestor $baseline HEAD
if ($LASTEXITCODE -ne 0) { throw 'Held baseline is not an ancestor.' }
if ($Phase -eq 'Validate') { exit 0 }

$signing = $Phase -in @('Input', 'Output')
$out = if ($signing) { $env:GUEST_SIGN_RECEIPT_DIRECTORY } else { Join-Path $root 'bin/guest-readiness-official' }
if ([string]::IsNullOrWhiteSpace($out)) { throw 'Receipt directory is required.' }
New-Item -ItemType Directory -Force $out | Out-Null
$receiptPath = Join-Path $out "$($Phase.ToLowerInvariant())-receipt.json"
if (Test-Path $receiptPath) { throw 'Phase receipt already exists; do not overwrite prior execution evidence.' }
$receipt = [pscustomobject]@{
    schema = 2; kind = 'android-official-guest-readiness-phase'; component = 'android'; phase = $Phase; baseline = $baseline; sourceCommit = $head
    version = $identity.version; buildId = $identity.buildId; patchSha256 = $null
    definitionId = '11410'; pipelinePath = 'build-tools/automation/azure-pipelines.yaml'
    sourceBranch = $env:BUILD_SOURCEBRANCH; buildNumber = $env:BUILD_BUILDID
    jobName = $env:SYSTEM_JOBNAME; jobAttempt = $env:SYSTEM_JOBATTEMPT
    requestedSignType = 'Test'
    templates = @(
        [pscustomobject]@{ repository = 'DevDiv/Xamarin.yaml-templates'; commit = $env:GUEST_YAML_VERSION },
        [pscustomobject]@{ repository = '1ESPipelineTemplates/MicroBuildTemplate'; commit = $env:GUEST_1ES_VERSION }
    )
    commands = [Collections.Generic.List[object]]::new()
    files = [Collections.Generic.List[object]]::new()
    packages = [Collections.Generic.List[object]]::new()
    status = 'running'; failure = $null; packageAdmission = $false
}
Write-GuestProducerReceipt $receipt $receiptPath
function Invoke-Recorded([string] $Name, [string] $Tool, [string[]] $Arguments, [string] $Directory = $root) {
    Invoke-GuestProducerCommand -Name "$($Phase.ToLowerInvariant())-$Name" -Tool $Tool -Arguments $Arguments -WorkingDirectory $Directory `
        -Receipt $receipt -ReceiptPath $receiptPath
}
function Get-FileReference([string] $Path) {
    [pscustomobject]@{ fileName = [IO.Path]::GetFileName($Path); sizeBytes = (Get-Item -LiteralPath $Path).Length; sha256 = (Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant() }
}
function Save-Inventory([string] $Directory, [string] $Rid, [string] $Prefix) {
    $id = "Microsoft.Android.Runtime.Mono.36.$Rid"
    $path = Join-Path $Directory "$id.$($identity.version).nupkg"
    $inventory = Read-GuestRuntimePackInventory $path $Rid $identity.version
    $inventoryPath = Join-Path $out "$Prefix.inventory.$id.json"
    # Pack runs on macOS/Linux and Input on Windows; their inventory bytes must agree.
    $json = ($inventory | ConvertTo-Json -Depth 12).Replace("`r`n", "`n").Replace("`r", "`n").TrimEnd([char]10) + "`n"
    [IO.File]::WriteAllText($inventoryPath, $json, [Text.UTF8Encoding]::new($false))
    [pscustomobject]@{ value = $inventory; reference = (Get-FileReference $inventoryPath); path = $path }
}
try {
    $verificationFailed = $false
    $priorSigningFailed = $false
    $priorJobStatus = ''
    if ($Phase -eq 'Output') {
        $priorJobStatus = $env:GUEST_SIGNING_JOB_STATUS
        if ($priorJobStatus -cnotin @('Succeeded', 'SucceededWithIssues', 'Failed', 'Canceled', 'Skipped') -or
            [string]::IsNullOrWhiteSpace($env:GUEST_SIGNING_OUTPUT_DIRECTORY) -or
            [string]::IsNullOrWhiteSpace($env:GUEST_RETAINED_OUTPUT_DIRECTORY)) {
            throw 'Normal signing output, retention directory and prior job status are required.'
        }
        if ([IO.Path]::GetFullPath($env:GUEST_SIGNING_OUTPUT_DIRECTORY) -ieq [IO.Path]::GetFullPath($env:GUEST_RETAINED_OUTPUT_DIRECTORY)) {
            throw 'Signing source and retained output directories must be distinct.'
        }
        New-Item -ItemType Directory -Force $env:GUEST_RETAINED_OUTPUT_DIRECTORY | Out-Null
        $priorSigningFailed = $priorJobStatus -cne 'Succeeded'
        $contextPath = Join-Path $out 'output-signing-context.json'
        [pscustomobject]@{
            schemaVersion = 1; kind = 'android-normal-signing-output-context'
            priorJobStatus = $priorJobStatus; observationPhase = 'Output-hook-entry'; observedAtUtc = [DateTime]::UtcNow.ToString('O')
            sourceStage = 'normal-packed-signing-output'; sourceDirectory = $env:GUEST_SIGNING_OUTPUT_DIRECTORY
            notice = 'Pre-hook Agent.JobStatus environment observation, not the final provider job result. Individual earlier task results were not collected.'
        } | ConvertTo-Json -Depth 4 | Set-Content $contextPath -Encoding utf8
        $receipt.files.Add((Get-FileReference $contextPath))
        Write-GuestProducerReceipt $receipt $receiptPath
    }
    $patch = Join-Path $out "$($Phase.ToLowerInvariant()).source.patch"
    Invoke-Recorded 'source-patch' git @('diff', '--binary', "--output=$patch", $baseline, 'HEAD') | Out-Null
    $receipt.patchSha256 = (Get-FileHash $patch).Hash.ToLowerInvariant()
    Invoke-Recorded 'submodules' git @('submodule', 'status', '--recursive') | Out-Null
    if ($Phase -in @('Build', 'Pack')) {
        $restoreConfig = Join-Path $root 'NuGet.config'
        if (-not $IsMacOS) {
            $receipt.files.Add((Copy-GuestReceiptFile $restoreConfig (Join-Path $out 'NuGet.config')))
        }
        $arguments = @(
            "-p:AndroidPackVersionLong=$($identity.version)", "-p:PackageVersion=$($identity.version)",
            "-p:AndroidStartupDiagnosticsBuildId=$($identity.buildId)", '-p:AndroidGuestReadinessBuild=true',
            '-p:RunningOnCI=true'
            # macOS already retrieved audit data through ordinary config discovery.
            if (-not $IsMacOS) { "-p:RestoreConfigFile=`"$restoreConfig`"" }
        )
        # MSBUILD_ARGS reaches Prepare, recursive make and normal package Execs; values are validated above.
        $target = if ($Phase -eq 'Build') { 'jenkins' } else { 'create-installers' }
        $makeArguments = @($target, "CONFIGURATION=$env:GUEST_CONFIGURATION",
            'PREPARE_CI=1', 'PREPARE_AUTOPROVISION=1', "MSBUILD_ARGS=$($arguments -join ' ')")
        if ($Phase -eq 'Pack') {
            $expectedBuildArguments = @('jenkins') + $makeArguments[1..($makeArguments.Count - 1)]
            $buildPath = Join-Path $out 'build-receipt.json'
            Get-VerifiedGuestBuildReceipt $buildPath $receipt $expectedBuildArguments | Out-Null
            # Keep the Build bytes distinct from the signing directory's alias of the Pack receipt.
            $copy = Join-Path $out 'build-phase-receipt.json'
            $receipt.files.Add((Copy-GuestReceiptFile $buildPath $copy))
            Write-GuestProducerReceipt $receipt $receiptPath
        }
        $previousGradleArgs = $env:GRADLEARGS
        try {
            if (-not $IsMacOS) {
                $gradleInit = Join-Path $PSScriptRoot 'guest-readiness-repositories.gradle'
                $receipt.files.Add((Copy-GuestReceiptFile $gradleInit (Join-Path $out 'guest-readiness-repositories.gradle')))
                $gradleArguments = if ([string]::IsNullOrWhiteSpace($previousGradleArgs)) { '--stacktrace --no-daemon' } else { $previousGradleArgs }
                # Environment reaches both root and pinned Java.Interop MSBuild projects.
                $env:GRADLEARGS = "$gradleArguments --init-script `"$gradleInit`""
            }
            Invoke-Recorded "make-$target" make $makeArguments | Out-Null
        } finally { $env:GRADLEARGS = $previousGradleArgs }
        if ($Phase -eq 'Pack') {
            $directory = Join-Path $root "bin/Build$env:GUEST_CONFIGURATION/nuget-unsigned"
            foreach ($reference in Save-GuestRuntimePackCandidates $directory $out $env:BUILD_BUILDID $env:GUEST_ATTEMPT) {
                $receipt.files.Add($reference)
            }
            Write-GuestProducerReceipt $receipt $receiptPath
            $receipt.files.Add((Copy-GuestReceiptFile (Join-Path $directory 'SignList.xml') (Join-Path $out 'SignList.xml')))
            $tools = Join-Path $root "bin/Build$env:GUEST_CONFIGURATION/buildtoolsinventory.csv"
            Copy-Item $tools (Join-Path $out 'buildtoolsinventory.csv')
            $receipt.files.Add((Get-FileReference (Join-Path $out 'buildtoolsinventory.csv')))
            foreach ($file in @('Configuration.props', "bin/Build$env:GUEST_CONFIGURATION/Configuration.Generated.props")) {
                $leaf = [IO.Path]::GetFileName($file)
                Copy-Item (Join-Path $root $file) (Join-Path $out $leaf)
                $receipt.files.Add((Get-FileReference (Join-Path $out $leaf)))
            }
            foreach ($rid in @('android-arm64', 'android-x64')) {
                $inventory = Save-Inventory $directory $rid 'build'
                Read-GuestRuntimePackNativeCarriers $inventory.path $inventory.value $identity.buildId | Out-Null
                $receipt.packages.Add([pscustomobject]@{ id = $inventory.value.id; inventory = $inventory.reference })
            }
            foreach ($package in Get-ChildItem $directory -File -Filter '*.nupkg') {
                if ($receipt.files.fileName -cnotcontains $package.Name) {
                    $receipt.files.Add((Get-FileReference $package.FullName))
                }
            }
            $cacheFiles = @(Get-ChildItem (Join-Path $root 'src/native/obj') -Recurse -Filter CMakeCache.txt -File)
            foreach ($abi in @('arm64-v8a', 'x86_64')) {
                foreach ($configuration in @('Debug', 'Release')) {
                    $matches = @($cacheFiles | Where-Object {
                        $text = Get-Content $_.FullName -Raw
                        $text -match "(?m)^ANDROID_ABI:[^=]+=$abi\r?$" -and
                        $text -match "(?m)^CMAKE_BUILD_TYPE:[^=]+=$configuration\r?$" -and
                        $text -match "(?m)^XA_STARTUP_DIAGNOSTICS_BUILD_ID:[^=]+=$([regex]::Escape($identity.buildId))\r?$"
                    })
                    if ($matches.Count -ne 1) { throw "Missing unique marker-bound native configuration: $abi $configuration" }
                    $copy = Join-Path $out "CMakeCache-$abi-$configuration.txt"
                    Copy-Item $matches[0].FullName $copy
                    $receipt.files.Add((Get-FileReference $copy))
                    foreach ($language in @('C', 'CXX')) {
                        $compiler = @(Get-ChildItem $matches[0].Directory.FullName -Recurse -File -Filter "CMake${language}Compiler.cmake")
                        if ($compiler.Count -ne 1) { throw "Missing unique generated compiler identity: $abi $configuration $language" }
                        $copy = Join-Path $out "CMake${language}Compiler-$abi-$configuration.cmake"
                        Copy-Item $compiler[0].FullName $copy
                        $receipt.files.Add((Get-FileReference $copy))
                    }
                }
            }
        }
    } else {
        $buildPath = Join-Path $env:GUEST_BUILD_RECEIPT_DIRECTORY 'pack-receipt.json'
        $build = Get-Content $buildPath -Raw | ConvertFrom-Json
        if ($build.schema -ne 2 -or $build.kind -cne 'android-official-guest-readiness-phase' -or
            $build.phase -cne 'Pack' -or $build.baseline -cne $baseline -or $build.status -cne 'completed-unadmitted' -or
            $build.sourceCommit -cne $head -or $build.version -cne $identity.version -or
            $build.buildId -cne $identity.buildId -or $build.patchSha256 -cne $receipt.patchSha256) {
            throw 'Build receipt does not bind this signing input.'
        }
        if (($build.templates | ConvertTo-Json -Compress) -cne ($receipt.templates | ConvertTo-Json -Compress)) {
            throw 'Build and signer resolved templates differ.'
        }
        Copy-Item $buildPath (Join-Path $out 'build-receipt.json') -Force
        $receipt.files.Add((Get-FileReference (Join-Path $out 'build-receipt.json')))
        if ($Phase -eq 'Output') {
            Invoke-Recorded 'sign-template-commit' git @('rev-parse', 'HEAD') $env:GUEST_TEMPLATE_CHECKOUT | Out-Null
            if ((Get-Content (Join-Path $out 'output-sign-template-commit.log') -Raw).Trim() -cne $env:GUEST_YAML_VERSION) {
                throw 'Signer template checkout differs from resolved resource.'
            }
            Invoke-Recorded 'verification-tool-version' dotnet @('--version') $env:GUEST_SIGNING_OUTPUT_DIRECTORY | Out-Null
            $toolVersion = (Get-Content (Join-Path $out 'output-verification-tool-version.log') -Raw).Trim()
            if ($toolVersion -cnotmatch '\A[0-9A-Za-z._-]{1,128}\z') { throw 'Invalid verifier version output.' }
            Invoke-Recorded '1es-template-commit' git @('rev-parse', 'HEAD') $env:GUEST_1ES_CHECKOUT | Out-Null
            if ((Get-Content (Join-Path $out 'output-1es-template-commit.log') -Raw).Trim() -cne $env:GUEST_1ES_VERSION) {
                throw '1ES checkout differs from resolved resource.'
            }
            $templateFiles = [Collections.Generic.List[object]]::new()
            foreach ($spec in @(
                @{ root = $env:GUEST_TEMPLATE_CHECKOUT; repository = 'DevDiv/Xamarin.yaml-templates'; commit = $env:GUEST_YAML_VERSION
                   paths = @('sign-artifacts/jobs/v4.yml', 'sign-artifacts/steps/v4.yml', 'sign-artifacts/steps/v4-SignFiles.proj',
                       'sign-artifacts/steps/common/Extract.ps1', 'sign-artifacts/steps/common/EscapeSignFiles.ps1') },
                @{ root = $env:GUEST_1ES_CHECKOUT; repository = '1ESPipelineTemplates/MicroBuildTemplate'; commit = $env:GUEST_1ES_VERSION
                   paths = @('azure-pipelines/MicroBuild.1ES.Official.yml', 'azure-pipelines/Stages/Stage.yml', 'azure-pipelines/Jobs/Job.yml') }
            )) {
                foreach ($path in $spec.paths) {
                    $templateFiles.Add([pscustomobject]@{ repository = $spec.repository; commit = $spec.commit; path = $path
                        sha256 = (Get-FileHash (Join-Path $spec.root $path)).Hash.ToLowerInvariant() })
                }
            }
            foreach ($name in @('SignPackageContents.binlog', 'SignNuGetPackages.binlog')) {
                Copy-Item (Join-Path $env:GUEST_SIGN_BINLOG_DIRECTORY $name) (Join-Path $out $name)
                $receipt.files.Add((Get-FileReference (Join-Path $out $name)))
            }
        }
        foreach ($rid in @('android-arm64', 'android-x64')) {
            $inputPrefix = if ($Phase -eq 'Input') { 'input' } else { 'observed-input' }
            $inputPackage = Save-Inventory $env:GUEST_UNSIGNED_DIRECTORY $rid $inputPrefix
            $inputCarriers = @(Read-GuestRuntimePackNativeCarriers $inputPackage.path $inputPackage.value $identity.buildId)
            $builtPackage = @($build.files | Where-Object { $_.fileName -ceq $inputPackage.value.fileName })
            if ($builtPackage.Count -ne 1 -or $builtPackage[0].sha256 -cne $inputPackage.value.sha256 -or
                $builtPackage[0].sizeBytes -ne $inputPackage.value.sizeBytes) { throw 'Unsigned package differs from build output.' }
            $builtInventory = @($build.packages | Where-Object { $_.id -ceq $inputPackage.value.id })
            if ($builtInventory.Count -ne 1 -or
                $builtInventory[0].inventory.sha256 -cne $inputPackage.reference.sha256 -or
                $builtInventory[0].inventory.sha256 -cne
                    (Get-FileHash (Join-Path $env:GUEST_BUILD_RECEIPT_DIRECTORY $builtInventory[0].inventory.fileName)).Hash.ToLowerInvariant()) {
                throw 'Build inventory does not bind the unsigned package.'
            }
            if ($Phase -eq 'Input') {
                $receipt.packages.Add([pscustomobject]@{ id = $inputPackage.value.id; inventory = $inputPackage.reference })
                continue
            }
            $originalInputPath = Join-Path $out "input.inventory.$($inputPackage.value.id).json"
            if ((Get-FileHash $originalInputPath).Hash.ToLowerInvariant() -cne $inputPackage.reference.sha256) {
                throw 'Signing transformed its retained unsigned input inventory.'
            }
            $inputPackage.reference = Get-FileReference $originalInputPath
            $output = Save-Inventory $env:GUEST_SIGNING_OUTPUT_DIRECTORY $rid 'output'
            $outputCarriers = @(Read-GuestRuntimePackNativeCarriers $output.path $output.value $identity.buildId)
            $configurationEvidence = [Collections.Generic.List[object]]::new()
            $abi = if ($rid -eq 'android-arm64') { 'arm64-v8a' } else { 'x86_64' }
            foreach ($configuration in @('Debug', 'Release')) {
                foreach ($name in @("CMakeCache-$abi-$configuration.txt", "CMakeCCompiler-$abi-$configuration.cmake", "CMakeCXXCompiler-$abi-$configuration.cmake")) {
                    $file = Join-Path $env:GUEST_BUILD_RECEIPT_DIRECTORY $name
                    $expected = @($build.files | Where-Object { $_.fileName -ceq $name })
                    if ($expected.Count -ne 1 -or (Get-FileHash $file).Hash.ToLowerInvariant() -cne $expected[0].sha256) {
                        throw 'Generated compiler evidence differs from build receipt.'
                    }
                    Copy-Item $file (Join-Path $out $name)
                    $configurationEvidence.Add((Get-FileReference (Join-Path $out $name) | Select-Object fileName, sha256))
                }
            }
            foreach ($carrier in $outputCarriers) {
                $before = @($inputCarriers | Where-Object { $_.path -ceq $carrier.path })
                if ($before.Count -ne 1) { throw 'Native carrier path changed during signing.' }
                $carrier | Add-Member -NotePropertyName input -NotePropertyValue ($before[0] | Select-Object * -ExcludeProperty path)
            }
            [pscustomobject]@{
                schemaVersion = 1; kind = 'guest-runtime-native-provenance'; id = $output.value.id; rid = $rid; version = $identity.version
                packageSha256 = $output.value.sha256; inventory = ($output.reference | Select-Object fileName, sha256)
                sourceCommit = $head; patchSha256 = $receipt.patchSha256; buildMarker = $identity.buildId
                configuration = [pscustomobject]@{
                    msbuildProperty = 'AndroidStartupDiagnosticsBuildId'; cmakeVariable = 'XA_STARTUP_DIAGNOSTICS_BUILD_ID'
                    value = $identity.buildId; evidence = @($configurationEvidence.ToArray())
                }
                carriers = $outputCarriers
            } | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $out "native-provenance.$($output.value.id).json") -Encoding utf8
            $verify = Invoke-GuestProducerCommand -Name "output-verify-$rid" -Tool dotnet -Arguments @('nuget', 'verify', '--all', $output.value.fileName) `
                -WorkingDirectory $env:GUEST_SIGNING_OUTPUT_DIRECTORY -Receipt $receipt -ReceiptPath $receiptPath -AllowNonzeroExit
            $signature = New-GuestRuntimePackSignature $output.value $verify.exitCode $toolVersion (Join-Path $out $verify.outputFileName)
            $signaturePath = Join-Path $out "output.signature.$($output.value.id).json"
            $signature | ConvertTo-Json -Depth 8 | Set-Content $signaturePath -Encoding utf8
            if ((Get-FileHash $output.path).Hash.ToLowerInvariant() -cne $output.value.sha256) { throw 'Signed bytes changed during verification.' }
            $receipt.packages.Add([pscustomobject]@{
                id = $output.value.id; input = $inputPackage.reference; output = $output.reference
                signature = (Get-FileReference $signaturePath)
            })
            $deltaPath = Join-Path $out "member-delta.$($output.value.id).json"
            [pscustomobject]@{
                schemaVersion = 1; kind = 'android-runtime-package-member-delta'
                inputSha256 = $inputPackage.value.sha256; outputSha256 = $output.value.sha256
                changes = @(Get-GuestRuntimePackMemberDelta $inputPackage.value $output.value)
                notice = 'Observed transformations only; no policy admission. NuGet pack task result not captured; correlate the provider timeline independently.'
            } | ConvertTo-Json -Depth 12 | Set-Content $deltaPath -Encoding utf8
            if (-not $output.value.signatureEntryPresent) {
                throw 'Normal packed output has no signature entry; unsigned output is not retained as signed output.'
            }
            Copy-GuestReceiptFile $output.path (Join-Path $env:GUEST_RETAINED_OUTPUT_DIRECTORY $output.value.fileName) | Out-Null
            if ($verify.exitCode -ne 0) {
                $verificationFailed = $true
            }
        }
    }
    if ($priorSigningFailed) {
        $receipt.failure = [pscustomobject]@{ stage = 'prior-signing-job'; message = "Job status before output capture: $priorJobStatus; individual earlier task results were not collected." }
    }
    $receipt.status = if ($verificationFailed) { 'produced-verification-failed' } elseif ($priorSigningFailed) { 'failed' } else { 'completed-unadmitted' }
    Write-GuestProducerReceipt $receipt $receiptPath
    if ($Phase -eq 'Output') {
        foreach ($package in $receipt.packages) {
            $inputInventory = Get-Content (Join-Path $out $package.input.fileName) -Raw | ConvertFrom-Json
            $output = Get-Content (Join-Path $out $package.output.fileName) -Raw | ConvertFrom-Json
            $signature = Get-Content (Join-Path $out $package.signature.fileName) -Raw | ConvertFrom-Json
            $postsignParameters = @{
                InputInventory = $inputInventory; OutputInventory = $output; Signature = $signature
                source = [pscustomobject]@{ repository = 'dotnet/android'; baselineCommit = $baseline; sourceCommit = $head; patchSha256 = $receipt.patchSha256 }
                buildReceipt = (Get-FileReference (Join-Path $out 'build-receipt.json') | Select-Object fileName, sha256)
                InputReference = ($package.input | Select-Object fileName, sha256)
                OutputReference = ($package.output | Select-Object fileName, sha256)
                SignatureReference = ($package.signature | Select-Object fileName, sha256)
                signer = [pscustomobject]@{
                    organization = 'devdiv'; project = 'DevDiv'; definitionId = 11410
                    buildId = [int] $env:BUILD_BUILDID; jobName = $env:SYSTEM_JOBNAME; jobAttempt = [int] $env:SYSTEM_JOBATTEMPT
                    pipelinePath = $receipt.pipelinePath; pipelineCommit = $head; requestedSignType = 'Test'; templates = @($templateFiles.ToArray())
                }
                operationEvidence = @(
                    [pscustomobject]@{ role = 'sign'; evidenceType = 'build-binlog'; reference = (Get-FileReference (Join-Path $out 'SignPackageContents.binlog') | Select-Object fileName, sha256) },
                    [pscustomobject]@{ role = 'sign'; evidenceType = 'build-binlog'; reference = (Get-FileReference (Join-Path $out 'SignNuGetPackages.binlog') | Select-Object fileName, sha256) },
                    [pscustomobject]@{ role = 'verify'; evidenceType = 'command-receipt'; reference = (Get-FileReference $receiptPath | Select-Object fileName, sha256) }
                )
                memberDelta = (Get-FileReference (Join-Path $out "member-delta.$($output.id).json") | Select-Object fileName, sha256)
                NativeProvenance = (Get-FileReference (Join-Path $out "native-provenance.$($output.id).json") | Select-Object fileName, sha256)
            }
            New-GuestRuntimePackagePostSign @postsignParameters |
                ConvertTo-Json -Depth 15 | Set-Content (Join-Path $out "postsign.$($output.id).json") -Encoding utf8
        }
    }
} catch {
    $receipt.status = 'failed'
    if ($null -eq $receipt.failure) { $receipt.failure = [pscustomobject]@{ stage = $Phase; message = $_.Exception.Message } }
    if ($Phase -eq 'Output' -and -not [string]::IsNullOrWhiteSpace($priorJobStatus)) {
        $receipt.failure.message = "Prior signing job status: $priorJobStatus; individual earlier task results not collected. $($receipt.failure.message)"
    }
    Write-GuestProducerReceipt $receipt $receiptPath
    throw
}
if ($verificationFailed) { Write-Error 'Normal output retained, but standard verification failed; no policy admission.' }
if ($priorSigningFailed) { Write-Error "Normal output retained, but prior signing job status was $priorJobStatus; no policy admission." }
