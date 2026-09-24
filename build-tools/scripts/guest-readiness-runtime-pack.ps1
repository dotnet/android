param (
    [Parameter(Mandatory)][string] $BuildNumber,
    [Parameter(Mandatory)][string] $Attempt,
    [string] $ExpectedSourceCommit,
    [string] $ExpectedPatchSha256,
    [switch] $Execute
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot\guest-readiness-runtime-pack.psm1" -Force
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$plan = New-GuestRuntimePackPlan -RepositoryRoot $root -BuildNumber $BuildNumber -Attempt $Attempt
if (-not $Execute) {
    $plan | ConvertTo-Json -Depth 12
    exit 0
}
if (-not $IsWindows) { throw 'This manual producer uses the repository normal Windows build route.' }
if ($ExpectedSourceCommit -cnotmatch '\A[0-9a-f]{40}\z' -or $ExpectedPatchSha256 -cnotmatch '\A[0-9a-f]{64}\z') {
    throw 'Explicit reviewed source and binary patch identities are required.'
}
Set-Location $root
$head = & git rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head -cne $ExpectedSourceCommit) { throw 'Checked-out source is not the reviewed producer commit.' }
& git merge-base --is-ancestor $plan.baseline HEAD
if ($LASTEXITCODE -ne 0) { throw 'The exact held Android baseline is not an ancestor.' }
$status = & git status --porcelain
if ($LASTEXITCODE -ne 0 -or $status) { throw 'Producer requires a clean committed source checkout.' }
$submodules = @(& git submodule status --recursive)
if ($LASTEXITCODE -ne 0 -or @($submodules | Where-Object { $_ -notmatch '^ [0-9a-f]{40} ' }).Count -ne 0) {
    throw 'All normal submodule inputs must be initialized at their committed revisions.'
}
$output = $plan.artifactDirectory
if (Test-Path $output) { throw 'Producer output already exists; use a fresh checkout rather than mixing receipts.' }
New-Item -ItemType Directory -Path $output | Out-Null
& git diff --binary "--output=$output\source.patch" $plan.baseline HEAD
if ($LASTEXITCODE -ne 0 -or (Get-FileHash "$output\source.patch").Hash.ToLowerInvariant() -cne $ExpectedPatchSha256) {
    throw 'Source patch bytes differ from the reviewed patch.'
}
$plan | ConvertTo-Json -Depth 12 | Set-Content "$output\plan.json" -Encoding utf8
$submodules | Set-Content "$output\submodules.txt" -Encoding utf8
$env:NUGET_PACKAGES = "$root\bin\guest-readiness-packages"
$env:DOTNET_CLI_HOME = "$root\bin\guest-readiness-dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

$receiptPath = "$output\producer-receipt.json"
$receipt = [pscustomobject]@{
    schema = 2; component = 'android'; baseline = $plan.baseline
    sourceCommit = $head; patchSha256 = $ExpectedPatchSha256
    version = $plan.version; buildId = $plan.buildId
    normalPackProject = 'build-tools/create-packs/Microsoft.Android.Runtime.proj'
    planRef = [pscustomobject]@{ fileName = 'plan.json'; sha256 = (Get-FileHash "$output\plan.json").Hash.ToLowerInvariant() }
    submodulesRef = [pscustomobject]@{ fileName = 'submodules.txt'; sha256 = (Get-FileHash "$output\submodules.txt").Hash.ToLowerInvariant() }
    compilerInputsRef = $null
    commands = [Collections.Generic.List[object]]::new()
    packages = [Collections.Generic.List[object]]::new()
    status = 'running'; failure = $null
    verificationOutputStreams = 'combined stdout/stderr'
    packageAdmission = $false
    executionNotice = 'Actual command results and normal package outputs only; no signing-policy approval, installation or loaded-byte claim.'
}
Write-GuestProducerReceipt $receipt $receiptPath
$stage = 'producer-setup'
function Invoke-Checked([string] $Tool, [string[]] $Arguments, [string] $Log) {
    $script:stage = [IO.Path]::GetFileNameWithoutExtension($Log)
    Invoke-GuestProducerCommand -Name $script:stage -Tool $Tool -Arguments $Arguments -WorkingDirectory $root `
        -Receipt $receipt -ReceiptPath $receiptPath | Out-Null
}

try {
# These tests do not require the Android SDK or alter ordinary package contents.
$cmake = (Get-Command cmake -ErrorAction Stop).Source
$ctest = Join-Path (Split-Path $cmake) 'ctest.exe'
Invoke-Checked $cmake @('-S', "$root\tests\native-startup-diagnostics", '-B', "$root\bin\guest-readiness-producer-tests") "$output\test-configure.log"
Invoke-Checked $cmake @('--build', "$root\bin\guest-readiness-producer-tests", '--config', 'Release') "$output\test-build.log"
Invoke-Checked $ctest @('--test-dir', "$root\bin\guest-readiness-producer-tests", '-C', 'Release', '--output-on-failure') "$output\test-run.log"
Invoke-Checked 'pwsh' @('-NoProfile', '-File', "$root\tests\native-startup-diagnostics\test-runtime-pack-producer.ps1") "$output\producer-tests.log"

foreach ($command in $plan.commands) {
    $stage = $command.name
    if ($command.tool.EndsWith('dotnet-local.cmd') -and -not (Test-Path "$root\bin\Release\dotnet\dotnet.exe")) {
        throw 'Normal preparation did not provide the required local .NET SDK.'
    }
    Invoke-Checked $command.tool $command.arguments "$output\$($command.name).log"
}

$stage = 'compiler-inputs'
# Inventory exact generated native inputs, not guessed latest files elsewhere on the machine.
$generated = @(
    "$root\src\native\CMakePresets.json",
    "$root\bin\BuildRelease\xa_build_configuration.cmake",
    "$root\bin\guest-readiness-toolchain\ndk\source.properties"
)
foreach ($path in $generated) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required normal generated input missing: $path" }
}
$ndkProperties = Get-Content $generated[2] -Raw
if ($ndkProperties -notmatch '(?m)^Pkg.Revision\s*=\s*28\.2\.13676358\s*$') { throw 'NDK does not match the held source pin.' }
$compiler = "$root\bin\guest-readiness-toolchain\ndk\toolchains\llvm\prebuilt\windows-x86_64\bin\clang++.exe"
Invoke-Checked $compiler @('--version') "$output\compiler-version.log"
$stage = 'compiler-inputs'
$generated += $compiler
$compileInputs = @(Get-ChildItem "$root\src\native\obj" -Recurse -File |
    Where-Object { $_.Name -in @('CMakeCache.txt', 'compile_commands.json', 'archive-dso-stub-config.hh', 'host-config.h', 'startup-diagnostics-build-id.txt') })
if ($compileInputs.Count -eq 0) { throw 'Native build compiler inputs are missing.' }
$generated += $compileInputs.FullName
foreach ($abi in @('arm64-v8a', 'x86_64')) {
    foreach ($configuration in @('Debug', 'Release')) {
        $matches = @($compileInputs | Where-Object {
            $_.Name -eq 'CMakeCache.txt' -and
            (Get-Content $_.FullName -Raw) -match "(?m)^ANDROID_ABI:[^=]+=$([regex]::Escape($abi))\r?$" -and
            (Get-Content $_.FullName -Raw) -match "(?m)^CMAKE_BUILD_TYPE:[^=]+=$configuration\r?$" -and
            (Get-Content $_.FullName -Raw) -match "(?m)^XA_STARTUP_DIAGNOSTICS_BUILD_ID:[^=]+=$([regex]::Escape($plan.buildId))\r?$"
        })
        if ($matches.Count -ne 1) { throw "Expected one marker-bound MonoVM configuration: $abi $configuration" }
    }
}
$inputInventory = @($generated | Sort-Object -Unique | ForEach-Object {
    [pscustomobject]@{
        path = [IO.Path]::GetRelativePath($root, $_)
        sha256 = (Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant()
        length = (Get-Item -LiteralPath $_).Length
    }
})
$inputInventory | ConvertTo-Json -Depth 6 | Set-Content "$output\compiler-inputs.json" -Encoding utf8
$receipt.compilerInputsRef = [pscustomobject]@{
    fileName = 'compiler-inputs.json'
    sha256 = (Get-FileHash "$output\compiler-inputs.json").Hash.ToLowerInvariant()
}
Write-GuestProducerReceipt $receipt $receiptPath

$stage = 'package-inventory'
$packages = @(Get-ChildItem "$output\packages" -Filter '*.nupkg' -File)
if ($packages.Count -ne 2) { throw 'Artifact-only output must contain exactly two normal runtime nupkgs.' }
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
Invoke-Checked $dotnet @('--version') "$output\verification-tool-version.log"
$dotnetVersion = @(Get-Content "$output\verification-tool-version.log")
if ($dotnetVersion.Count -ne 1 -or $dotnetVersion[0].Length -gt 128 -or $dotnetVersion[0] -cnotmatch '\A[0-9A-Za-z._-]+\z') {
    throw 'Cannot identify the actual package verification tool.'
}
foreach ($rid in $plan.rids) {
    $stage = "inventory-$rid"
    $package = "$output\packages\Microsoft.Android.Runtime.Mono.36.$rid.$($plan.version).nupkg"
    $inventory = Read-GuestRuntimePackInventory -Path $package -Rid $rid -Version $plan.version
    $inventoryPath = "$output\inventory.$($inventory.id).json"
    $inventory | ConvertTo-Json -Depth 8 | Set-Content $inventoryPath -Encoding utf8
    $verifyLog = "$output\verify-$rid.log"
    $stage = "verify-$rid"
    $verification = Invoke-GuestProducerCommand -Name $stage -Tool $dotnet -Arguments @('nuget', 'verify', '--all', $inventory.fileName) `
        -WorkingDirectory "$output\packages" -Receipt $receipt -ReceiptPath $receiptPath -AllowNonzeroExit
    $verifyExitCode = $verification.exitCode
    if ((Get-FileHash -LiteralPath $package).Hash.ToLowerInvariant() -cne $inventory.sha256) {
        throw 'Package bytes changed during signature verification.'
    }
    $signature = New-GuestRuntimePackSignature $inventory $verifyExitCode $dotnetVersion[0] $verifyLog
    $signaturePath = "$output\signature.$($inventory.id).json"
    $signature | ConvertTo-Json -Depth 6 | Set-Content $signaturePath -Encoding utf8
    $receipt.packages.Add([pscustomobject]@{
        inventory = $inventory
        verificationExitCode = $verifyExitCode
        signatureClass = $signature.classification
        inventoryRef = [pscustomobject]@{
            fileName = [IO.Path]::GetFileName($inventoryPath)
            sha256 = (Get-FileHash -LiteralPath $inventoryPath).Hash.ToLowerInvariant()
        }
        signatureRef = [pscustomobject]@{
            fileName = [IO.Path]::GetFileName($signaturePath)
            sha256 = (Get-FileHash -LiteralPath $signaturePath).Hash.ToLowerInvariant()
        }
        admission = 'blocked-pending-approved-signing-and-consumer-receipts'
    })
    Write-GuestProducerReceipt $receipt $receiptPath
    if ($signature.classification -eq 'verification-failed') { throw 'Package signature verification failed.' }
}
Invoke-Checked 'git' @('diff', '--exit-code', '--quiet', 'HEAD') "$output\source-unchanged.log"
$receipt.status = 'completed-unadmitted'
Write-GuestProducerReceipt $receipt $receiptPath
} catch {
    $receipt.status = 'failed'
    if ($null -eq $receipt.failure) {
        $receipt.failure = [pscustomobject]@{ stage = $stage; message = $_.Exception.Message }
    }
    Write-GuestProducerReceipt $receipt $receiptPath
    throw
}
exit 0
