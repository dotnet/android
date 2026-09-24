param ([string] $JavaHome = $env:JAVA_HOME)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$out = Join-Path $root ('bin\guest-readiness-gradle-tests\run ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$out\child" | Out-Null
Copy-Item "$PSScriptRoot\gradle-repository-settings.gradle" "$out\settings.gradle"
Copy-Item "$PSScriptRoot\gradle-repository-build.gradle" "$out\build.gradle"
$init = Join-Path $out 'diagnostic init.gradle'
Copy-Item "$root\build-tools\scripts\guest-readiness-repositories.gradle" $init
$previousJava = $env:JAVA_HOME
$previousGradleHome = $env:GRADLE_USER_HOME
$previousGradleArgs = $env:GRADLEARGS
$previousNuGet = $env:NUGET_PACKAGES
try {
    $env:JAVA_HOME = $JavaHome
    # Gradle's immutable-workspace temporary names otherwise exceed Windows path limits here.
    $env:GRADLE_USER_HOME = Join-Path $root 'bin\gradle'
    $wrapper = Join-Path $root ('build-tools\gradle\' + $(if ($IsWindows) { 'gradlew.bat' } else { 'gradlew' }))
    foreach ($enabled in @($false, $true)) {
        $arguments = @('--offline', '--no-daemon', '--console=plain', '-p', $out,
            "-PexpectMirror=$($enabled.ToString().ToLowerInvariant())", 'assertRepositories', ':child:assertRepositories')
        if ($enabled) { $arguments += @('--init-script', $init) }
        & $wrapper @arguments *> "$out\gradle-$enabled.log"
        if ($LASTEXITCODE -ne 0) {
            Get-Content "$out\gradle-$enabled.log"
            throw "Real Gradle lifecycle repository assertions failed (diagnostic=$enabled)."
        }
    }
    $env:NUGET_PACKAGES = Join-Path $root '.packages'
    foreach ($enabled in @($false, $true)) {
        $env:GRADLEARGS = if ($enabled) { "--stacktrace --no-daemon --init-script `"$init`"" } else { $null }
        $expected = if ($enabled) { $env:GRADLEARGS } else { '--stacktrace --no-daemon' }
        foreach ($project in @(
            'src\manifestmerger\manifestmerger.csproj',
            'src\proguard-android\proguard-android.csproj',
            'external\Java.Interop\tools\java-source-utils\java-source-utils.csproj'
        )) {
            $observed = & dotnet msbuild (Join-Path $root $project) -nologo -getProperty:GradleArgs
            if ($LASTEXITCODE -ne 0 -or ($observed -join "`n").Trim() -cne $expected) {
                throw "Actual project did not inherit expected GradleArgs: $project / $enabled."
            }
        }
    }
    Write-Host "PASS: actual held Gradle, settings plugin/buildscript/dependency and project/buildscript repositories, child project, OFF/ON, path with spaces. Offline fixture only. Evidence: $out"
    Write-Host 'PASS: uppercase GradleArgs environment in all three actual root/submodule project evaluations, ON/OFF and spaced init-script path.'
} finally {
    $env:JAVA_HOME = $previousJava
    $env:GRADLE_USER_HOME = $previousGradleHome
    $env:GRADLEARGS = $previousGradleArgs
    $env:NUGET_PACKAGES = $previousNuGet
}
exit 0
