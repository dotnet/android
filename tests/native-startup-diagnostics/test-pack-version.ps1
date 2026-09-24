$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$out = Join-Path $root ('bin\guest-pack-version-tests\run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $out | Out-Null
$previousCache = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = Join-Path $root '.packages'
try {
    New-Item -ItemType Directory -Force $env:NUGET_PACKAGES | Out-Null
    $cacheIgnore = Join-Path $env:NUGET_PACKAGES '.gitignore'
    if (-not (Test-Path $cacheIgnore)) { [IO.File]::WriteAllText($cacheIgnore, "*`n") }
    # Compile the production Git tasks, not substitute version task implementations.
    $tasks = "$PSScriptRoot\pack-version-tasks.csproj"
    if (-not (Test-Path "$root\bin\guest-pack-version-tests\tasks-obj\project.assets.json")) {
        & dotnet restore $tasks --configfile "$root\NuGet.config" --nologo -v:q
        if ($LASTEXITCODE -ne 0) { throw 'Production Git task fixture restore failed.' }
    }
    & dotnet build $tasks --no-restore --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Production Git task fixture compilation failed.' }
    $common = @(
        "$root\build-tools\create-packs\Microsoft.Android.Runtime.proj", '-t:PackVersionRegression', '-nologo', '-v:m',
        "-p:PrepTasksAssembly=$root\bin\guest-pack-version-tests\tasks\net10.0\pack-version-tasks.dll",
        "-p:CustomAfterMicrosoftCommonTargets=$PSScriptRoot\pack-version-regression.targets",
        '-p:PackVersionCommitCount=73', '-p:AndroidRuntime=Mono'
    )
    if (-not $IsMacOS) { $common += "-p:RestoreConfigFile=$root\NuGet.config" }
    $diagnostic = @('-p:AndroidGuestReadinessBuild=true', '-p:AndroidPackVersionLong=36.1.69-guest.12345.1',
        '-p:PackageVersion=36.1.69-guest.12345.1', '-p:AndroidStartupDiagnosticsBuildId=android-d549-12345-1')
    foreach ($rid in @('android-arm64', 'android-x64')) {
        foreach ($mode in @('diagnostic', 'off', 'false')) {
            $version = if ($mode -eq 'diagnostic') { '36.1.69-guest.12345.1' } else { '36.1.74' }
            $directory = Join-Path $out "$rid-$mode"
            $properties = if ($mode -eq 'diagnostic') { $diagnostic } else {
                # Even supplied versions must retain ordinary target-time overwrite semantics when off.
                @('-p:AndroidPackVersionLong=1.2.3', '-p:PackageVersion=1.2.3')
            }
            if ($mode -eq 'false') { $properties += '-p:AndroidGuestReadinessBuild=false' }
            & dotnet msbuild @common @properties "-p:AndroidRID=$rid" "-p:ExpectedPackageVersion=$version" `
                "-p:VersionFixtureOutput=$directory" "-bl:$directory.binlog"
            if ($LASTEXITCODE -ne 0) { throw "Actual runtime project version/PackTask failed: $rid/$mode." }
            $id = "Microsoft.Android.Runtime.Mono.36.$rid"
            $package = Join-Path $directory "$id.$version.nupkg"
            if (-not (Test-Path $package)) { throw "Actual PackTask archive name mismatch: $package." }
            $zip = [IO.Compression.ZipFile]::OpenRead($package)
            try {
                $entry = $zip.GetEntry("$id.nuspec")
                if ($null -eq $entry) { throw 'Actual PackTask did not produce its expected nuspec.' }
                $reader = [IO.StreamReader]::new($entry.Open())
                try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
                if ($nuspec.package.metadata.version -cne $version -or $nuspec.package.metadata.id -cne $id) {
                    throw 'Actual PackTask nuspec identity mismatch.'
                }
            } finally { $zip.Dispose() }
        }
    }
    foreach ($case in @(
        @{ property = '-p:AndroidPackVersionLong=36.1.74'; error = 'Guest readiness requires a canonical unique package version.' },
        @{ property = '-p:PackageVersion=36.1.74'; error = 'Guest readiness package versions must agree.' },
        @{ property = '-p:AndroidStartupDiagnosticsBuildId=android-d549-12345-2'; error = 'Guest readiness version and marker must bind the same build and attempt.' }
    )) {
        $lines = & dotnet msbuild @common @diagnostic $case.property '-p:AndroidRID=android-arm64' 2>&1
        if ($LASTEXITCODE -eq 0 -or -not ($lines -join "`n").Contains($case.error)) {
            throw "Actual version target did not reject invalid diagnostic input: $($case.property)."
        }
    }
    Write-Host "PASS: real runtime project, production Git/version tasks, six actual NuGet packages and invalid identities. Synthetic payloads only. Evidence: $out"
} finally { $env:NUGET_PACKAGES = $previousCache }
exit 0
