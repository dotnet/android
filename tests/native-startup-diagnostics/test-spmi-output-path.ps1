param (
    [Parameter(Mandatory = $true)][string] $TaskDirectory,
    [Parameter(Mandatory = $true)][string] $GuardianProofDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'This characterizes Windows path semantics only.' }
$task = Get-Content (Join-Path $TaskDirectory 'task.json') -Raw | ConvertFrom-Json
if ($task.id -ine 'c954c89b-0c57-4f6c-bca5-5ff47d04ddf5' -or
    "$($task.version.Major).$($task.version.Minor).$($task.version.Patch)" -cne '1.86.0') {
    throw 'Expected exact retained 1ESSecretScanning 1.86.0 task.'
}
$output = @($task.inputs | Where-Object name -CEQ 'Output')
if ($output.Count -ne 1 -or $output[0].defaultValue -cne '$(Build.SourcesDirectory)\') {
    throw 'Unexpected task Output default.'
}
$variablesPath = Join-Path $TaskDirectory 'node_modules\gdn-task-lib\gdn-variables.js'
if ((Get-FileHash $variablesPath).Hash -ine '5c40b9dd0e6af4a46e296e486ad252d6f680139585a2b1e6c1a69a74c9ddd5f5') {
    throw 'Expected exact retained task variable setup bytes.'
}
$variables = Get-Content $variablesPath -Raw
foreach ($statement in @(
    'const varName = `GDNP_${taskName}_${taskInput.name.toUpperCase()}`;',
    'const varValue = process.env[varName];',
    'if (varValue == null) {',
    'process.env[varName] = inputValue;'
)) {
    if (-not $variables.Contains($statement)) { throw 'Retained task environment precedence changed.' }
}
$proof = Get-Content (Join-Path $GuardianProofDirectory 'receipt.json') -Raw | ConvertFrom-Json
foreach ($file in $proof.files) {
    $path = Join-Path $GuardianProofDirectory $file.path
    if ((Get-FileHash $path).Hash -ine $file.sha256 -or (Get-Item $path).Length -ne $file.sizeBytes) {
        throw 'Guardian static proof hash mismatch.'
    }
}
$factory = Get-Content (Join-Path $GuardianProofDirectory 'trace\CliAnalyzerOutputPathFactory.decompiled.cs') -Raw
foreach ($statement in @(
    'text = Path.ChangeExtension(text, Path.GetExtension(resultPath));',
    'CopyFileToUserLocation(resultPath, text);',
    'analyzeConfig.Arguments[outputKey].Values = resultsPaths;'
)) {
    if (-not $factory.Contains($statement)) { throw 'Retained output-path receiver changed.' }
}
$raw = 'C:\a\_work\1\.gdn\.r\sarifpatternmatcher\001\sarifpatternmatcher.sarif'
$explicit = 'C:\a\_work\_temp\guest-readiness-spmi.sarif'
$extension = [IO.Path]::GetExtension($raw)
if ([IO.Path]::ChangeExtension('C:\a\_work\1\s\', $extension) -cne 'C:\a\_work\1\s\.sarif' -or
    [IO.Path]::ChangeExtension($explicit, $extension) -cne $explicit -or
    [IO.Path]::GetExtension($explicit) -cne $extension -or $explicit -ceq $raw) {
    throw 'Windows FILE-output path characterization failed.'
}
# Model only: acquired task/Guardian code is never loaded or executed.
foreach ($case in @(
    @{Present=$false; Value=$null; Expected='task-default'},
    @{Present=$true; Value='explicit-file'; Expected='explicit-file'},
    @{Present=$true; Value=''; Expected=''}
)) {
    $resolved = if ($case.Present) { $case.Value } else { 'task-default' }
    if ($resolved -cne $case.Expected) { throw 'Environment precedence model failed.' }
}
# Guardian numbers duplicate requested names before changing extensions: a shared
# filename can still converge. The graph check therefore requires one scanner.
if ([IO.Path]::ChangeExtension("$explicit (2)", $extension) -cne $explicit) {
    throw 'Duplicate requested output characterization changed.'
}
Write-Output 'PASS: static retained task/Guardian source checks, real Windows Path values, and labeled absent/explicit/empty environment model. No scanner execution or hosted claim.'
exit 0
