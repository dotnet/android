#requires -Version 7.3
param (
    [Parameter(Mandatory)][ValidateSet('Input', 'Output')][string] $Phase,
    [Parameter(Mandatory)][string] $SourceDirectory,
    [Parameter(Mandatory)][string] $DestinationDirectory,
    [Parameter(Mandatory)][string] $EvidenceDirectory,
    [string] $WorkingDirectory = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot/guest-readiness-runtime-pack.psm1" -Force

$sourceCommit = 'c10a5463cc66cc2c3f5513110353b48bf4f75203'
$signingTemplateCommit = '19fa9b5addc2c2d99a6e09492a60b3b2f01e799b'
$oneEsCommit = '76ca628b0f2f914dd9a2fa37b54bffe2778f7f81'
$version = '36.1.69-guest.15457266.1'
$marker = 'android-d549-15457266-1'
$expected = [ordered]@{
    'android-arm64' = 'b6f89ce67d01e8f99c14718e3d16138e1dae91e1edc5f066984b26bfc798d816'
    'android-x64' = '28a2edcb90afb766bc1599dc36165c3822662315eebb92095a85569d34fc50d2'
}
$signListHash = 'e9ccca0fb2e2d6ff567c9f0578d14f9a7652cb5c8811f7b2a41ae01804c59fdc'

if ($env:DIAGNOSTIC_BUILD_REASON -cne 'Manual' -or $env:DIAGNOSTIC_REPOSITORY -cne 'dotnet/android' -or
    $env:DIAGNOSTIC_SOURCE_BRANCH -cnotmatch '\Arefs/heads/(?!main\z|release/)[A-Za-z0-9._/-]+\z' -or
    $env:DIAGNOSTIC_SOURCE_VERSION -cnotmatch '\A[0-9a-f]{40}\z' -or
    $env:DIAGNOSTIC_SIGNING_TEMPLATE_VERSION -cne $signingTemplateCommit -or
    $env:DIAGNOSTIC_1ES_VERSION -cne $oneEsCommit) {
    throw 'Real diagnostic signing requires manual non-release source and the reviewed signing/1ES template revisions.'
}
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$head = & git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head -cne $env:DIAGNOSTIC_SOURCE_VERSION) { throw 'Checkout differs from pipeline source.' }
if ($Phase -eq 'Input' -and -not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
    throw 'Source artifact directory is missing.'
}
if ([IO.Path]::GetFullPath($SourceDirectory) -ieq [IO.Path]::GetFullPath($DestinationDirectory) -or
    [IO.Path]::GetFullPath($SourceDirectory) -ieq [IO.Path]::GetFullPath($EvidenceDirectory) -or
    [IO.Path]::GetFullPath($DestinationDirectory) -ieq [IO.Path]::GetFullPath($EvidenceDirectory)) {
    throw 'Source, staging and evidence paths must remain isolated.'
}

function Get-Reference([string] $Path) {
    [pscustomobject]@{
        fileName = [IO.Path]::GetFileName($Path)
        sizeBytes = (Get-Item -LiteralPath $Path).Length
        sha256 = (Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant()
    }
}
function Assert-Reference([string] $Path, [psobject] $Reference) {
    if ($null -eq $Reference -or $Reference.fileName -cne [IO.Path]::GetFileName($Path) -or
        $Reference.sizeBytes -ne (Get-Item -LiteralPath $Path).Length -or
        $Reference.sha256 -cne (Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant()) {
        throw "Original artifact reference does not match $([IO.Path]::GetFileName($Path))."
    }
}
function Get-Package([string] $Directory, [string] $Rid) {
    $leaf = "Microsoft.Android.Runtime.Mono.36.$Rid.$version.nupkg"
    $path = Join-Path $Directory $leaf
    $file = Get-Item -LiteralPath $path
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Runtime package is not a regular file.'
    }
    $inventory = Read-GuestRuntimePackInventory $path $Rid $version
    Read-GuestRuntimePackNativeCarriers $path $inventory $marker | Out-Null
    return $inventory
}

if ($Phase -eq 'Input') {
    if ([string]::IsNullOrWhiteSpace($WorkingDirectory) -or
        [IO.Path]::GetFullPath($WorkingDirectory) -ieq [IO.Path]::GetFullPath($SourceDirectory) -or
        [IO.Path]::GetFullPath($WorkingDirectory) -ieq [IO.Path]::GetFullPath($DestinationDirectory) -or
        [IO.Path]::GetFullPath($WorkingDirectory) -ieq [IO.Path]::GetFullPath($EvidenceDirectory) -or
        (Test-Path -LiteralPath $WorkingDirectory)) {
        throw 'A fresh, isolated normal signing working directory is required.'
    }
    $receiptPath = Join-Path $SourceDirectory 'pack-receipt.json'
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.schema -ne 2 -or $receipt.kind -cne 'android-official-guest-readiness-phase' -or
        $receipt.component -cne 'android' -or $receipt.phase -cne 'Pack' -or
        $receipt.status -cne 'completed-unadmitted' -or $receipt.sourceCommit -cne $sourceCommit -or
        $receipt.definitionId -cne '11410' -or $receipt.buildNumber -cne '15457266' -or
        $receipt.version -cne $version -or $receipt.buildId -cne $marker -or
        $receipt.requestedSignType -cne 'Test' -or $receipt.packageAdmission -ne $false -or
        @($receipt.packages).Count -ne 2) { throw 'Original Pack receipt does not identify the frozen unsigned producer.' }
    $signList = Join-Path $SourceDirectory 'SignList.xml'
    if ((Get-FileHash -LiteralPath $signList).Hash.ToLowerInvariant() -cne $signListHash) {
        throw 'Original SignList.xml differs from the reviewed source.'
    }
    Assert-Reference $signList (@($receipt.files | Where-Object { $_.fileName -ceq 'SignList.xml' }) | Select-Object -First 1)
    $inputs = @(
        foreach ($rid in $expected.Keys) {
            $inventory = Get-Package $SourceDirectory $rid
            if ($inventory.sha256 -cne $expected[$rid] -or $inventory.signatureEntryPresent) {
                throw "Frozen $rid package hash or unsigned state differs."
            }
            $record = @($receipt.files | Where-Object { $_.fileName -ceq $inventory.fileName })
            $package = @($receipt.packages | Where-Object { $_.id -ceq $inventory.id })
            if ($record.Count -ne 1 -or $package.Count -ne 1) { throw "Original receipt does not uniquely bind $rid." }
            $path = Join-Path $SourceDirectory $inventory.fileName
            Assert-Reference $path $record[0]
            $inventoryPath = Join-Path $SourceDirectory $package[0].inventory.fileName
            Assert-Reference $inventoryPath $package[0].inventory
            $priorInventory = Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json
            if ($priorInventory.sha256 -cne $inventory.sha256 -or $priorInventory.sizeBytes -ne $inventory.sizeBytes -or
                ($priorInventory.entries | ConvertTo-Json -Depth 8 -Compress) -cne ($inventory.entries | ConvertTo-Json -Depth 8 -Compress)) {
                throw "Original inventory differs from the actual $rid archive."
            }
            [pscustomobject]@{ rid = $rid; path = $path; inventory = $inventory }
        }
    )
    if (Test-Path -LiteralPath $DestinationDirectory) { throw 'Signing input destination already exists.' }
    if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'Signing evidence destination already exists.' }
    # The approved v4 Extract step uses this path as its process working directory before creating its own output.
    New-Item -ItemType Directory -Path $WorkingDirectory -ErrorAction Stop | Out-Null
    New-Item -ItemType Directory $DestinationDirectory, $EvidenceDirectory | Out-Null
    foreach ($input in $inputs) {
        $destination = Join-Path $DestinationDirectory $input.inventory.fileName
        Copy-Item -LiteralPath $input.path -Destination $destination
        if ((Get-FileHash -LiteralPath $destination).Hash.ToLowerInvariant() -cne $expected[$input.rid]) {
            throw 'Staged package differs from its frozen input.'
        }
    }
    Copy-Item -LiteralPath $signList -Destination (Join-Path $DestinationDirectory 'SignList.xml')
    Copy-Item -LiteralPath $receiptPath -Destination (Join-Path $EvidenceDirectory 'pack-receipt.json')
    Write-GuestProducerReceipt ([pscustomobject]@{
        schemaVersion = 1; kind = 'android-diagnostic-real-sign-input'; packageAdmission = $false
        producer = [pscustomobject]@{ organization = 'devdiv'; project = 'DevDiv'; definitionId = 11410
            buildId = 15457266; sourceCommit = $sourceCommit; artifact = 'guest-readiness-Darwin-Pack'
            receipt = (Get-Reference (Join-Path $EvidenceDirectory 'pack-receipt.json')) }
        signerSourceCommit = $head; signType = 'Real'
        templates = @([pscustomobject]@{ repository = 'DevDiv/Xamarin.yaml-templates'; commit = $signingTemplateCommit },
            [pscustomobject]@{ repository = '1ESPipelineTemplates/MicroBuildTemplate'; commit = $oneEsCommit })
        signList = (Get-Reference (Join-Path $DestinationDirectory 'SignList.xml'))
        packages = @($inputs | ForEach-Object { Get-Reference (Join-Path $DestinationDirectory $_.inventory.fileName) })
        status = 'staged-unadmitted'
    }) (Join-Path $EvidenceDirectory 'input-receipt.json')
} else {
    $publicationRoot = Split-Path -Path $SourceDirectory -Parent
    New-Item -ItemType Directory -Force $publicationRoot, $EvidenceDirectory | Out-Null
    try {
    if ($env:DIAGNOSTIC_SIGNING_JOB_STATUS -cne 'Succeeded') {
        throw "Normal signing job did not succeed: $env:DIAGNOSTIC_SIGNING_JOB_STATUS"
    }
    if (-not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
        throw 'Normal signing produced no signed output directory.'
    }
    $inputReceipt = Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'input-receipt.json') -Raw | ConvertFrom-Json
    if ($inputReceipt.kind -cne 'android-diagnostic-real-sign-input' -or
        $inputReceipt.signerSourceCommit -cne $head -or $inputReceipt.signType -cne 'Real' -or
        $inputReceipt.templates[0].commit -cne $signingTemplateCommit -or
        $inputReceipt.templates[1].commit -cne $oneEsCommit -or
        $inputReceipt.producer.sourceCommit -cne $sourceCommit -or
        $inputReceipt.producer.buildId -ne 15457266 -or $inputReceipt.packageAdmission -ne $false) {
        throw 'Signing input receipt changed.'
    }
    $files = @(Get-ChildItem -LiteralPath $SourceDirectory -File -Recurse)
    if ($files.Count -ne 2) { throw 'Signed output must contain exactly two archive files.' }
    $outputs = @(
        foreach ($rid in $expected.Keys) {
            $input = Get-Package $DestinationDirectory $rid
            if ($input.sha256 -cne $expected[$rid] -or $input.signatureEntryPresent) { throw 'Staged input changed during signing.' }
            $output = Get-Package $SourceDirectory $rid
            if (-not $output.signatureEntryPresent -or $output.sha256 -ceq $input.sha256) {
                throw 'No distinct signed runtime package was produced.'
            }
            $inputRecord = @($inputReceipt.packages | Where-Object { $_.fileName -ceq $input.fileName })
            if ($inputRecord.Count -ne 1) { throw 'Input receipt does not uniquely bind the package.' }
            Assert-Reference (Join-Path $DestinationDirectory $input.fileName) $inputRecord[0]
            [pscustomobject]@{ input = $inputRecord[0]; output = (Get-Reference (Join-Path $SourceDirectory $output.fileName))
                changes = @(Get-GuestRuntimePackMemberDelta $input $output) }
        }
    )
    Write-GuestProducerReceipt ([pscustomobject]@{
        schemaVersion = 1; kind = 'android-diagnostic-real-sign-output'; packageAdmission = $false
        inputReceipt = (Get-Reference (Join-Path $EvidenceDirectory 'input-receipt.json'))
        signerSourceCommit = $head; signType = 'Real'; packages = $outputs
        status = 'produced-policy-unqualified'
        notice = 'Normal template verification and 1ES job/scanner results must be checked separately; these bytes are not approved for installation or promotion.'
    }) (Join-Path $EvidenceDirectory 'output-receipt.json')
    } catch {
        $failure = [pscustomobject]@{
            schemaVersion = 1; kind = 'android-diagnostic-real-sign-failure'; packageAdmission = $false
            signerSourceCommit = $head; status = 'failed'; phase = 'Output'
            message = $_.Exception.Message
            notice = 'No signed package or signing success is asserted. Inspect the normal signing task and scanner timeline.'
        }
        Write-GuestProducerReceipt $failure (Join-Path $EvidenceDirectory 'output-failure.json')
        Write-GuestProducerReceipt $failure (Join-Path $publicationRoot 'status.failed.json')
        throw
    }
}
