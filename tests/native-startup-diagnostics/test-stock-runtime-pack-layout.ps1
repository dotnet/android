param ([Parameter(Mandatory)][string] $StockPackage)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$source = Join-Path $root 'build-tools\scripts\guest-readiness-runtime-pack.psm1'
Import-Module $source -Force
$originalHash = (Get-FileHash -LiteralPath $StockPackage).Hash
$expectedRejection = 'Expected version is not a canonical unique Android guest producer version.'
try {
    Read-GuestRuntimePackInventory $StockPackage 'android-x64' '36.1.69' | Out-Null
    throw 'Production inventory incorrectly accepts held versions.'
} catch {
    if ($_.Exception.Message -cne $expectedRejection) { throw }
}

# Only model the version admission predicate; never rewrite the real stock archive.
# All ZIP layout, hashing, CRC, size and native-closure code remains the production code.
$text = Get-Content $source -Raw
$predicate = "if (`$Version -cnotmatch '\A36\.1\.69-guest\.[1-9][0-9]{0,11}\.[1-9][0-9]{0,3}\z')"
if (($text.Split($predicate, [StringSplitOptions]::None)).Count -ne 2) {
    throw 'Expected exactly one production version predicate.'
}
$out = Join-Path $root ('bin\guest-stock-layout-tests\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $out | Out-Null
$module = Join-Path $out 'stock-layout-test.psm1'
$stockPredicate = "if (`$Version -cnotmatch '\A36\.1\.69\z')"
[IO.File]::WriteAllText($module, $text.Replace($predicate, $stockPredicate))
$modeledText = Get-Content $module -Raw
if (($modeledText.Split($stockPredicate, [StringSplitOptions]::None)).Count -ne 2 -or
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($modeledText.Replace($stockPredicate, $predicate))) -cne
    [Convert]::ToBase64String([IO.File]::ReadAllBytes($source))) {
    throw 'Stock test changed production code beyond the single version predicate.'
}
Import-Module $module -Prefix Stock -Force
try {
    $inventory = Read-StockGuestRuntimePackInventory $StockPackage 'android-x64' '36.1.69'
    $zip = [IO.Compression.ZipFile]::OpenRead($StockPackage)
    try {
        if ($zip.Entries.Count -ne 24 -or $inventory.entries.Count -ne $zip.Entries.Count) {
            throw 'Unexpected held stock archive layout.'
        }
        foreach ($entry in $zip.Entries) {
            $stream = $entry.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
            finally { $stream.Dispose() }
            $record = @($inventory.entries | Where-Object path -CEQ $entry.FullName)
            if ($record.Count -ne 1 -or $record[0].sizeBytes -ne $entry.Length -or $record[0].sha256 -cne $hash) {
                throw "Stock member not completely inventoried: $($entry.FullName)"
            }
        }
    } finally { $zip.Dispose() }
    if ((Get-FileHash -LiteralPath $StockPackage).Hash -cne $originalHash -or
        $inventory.sha256 -cne $originalHash.ToLowerInvariant()) { throw 'Stock archive byte identity changed.' }
    $inventory | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $out 'stock-inventory.json')
    Write-Host "PASS: byte-identical held stock archive, all 24 members inventoried. Only version predicate modeled; no diagnostic or signature admission. Evidence: $out"
} finally { Remove-Module (Get-Module | Where-Object Path -EQ $module) }
try {
    Read-GuestRuntimePackInventory $StockPackage 'android-x64' '36.1.69' | Out-Null
    throw 'Production inventory incorrectly accepts held versions after the test.'
} catch {
    if ($_.Exception.Message -cne $expectedRejection) { throw }
}
exit 0
