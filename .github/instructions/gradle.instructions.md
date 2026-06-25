---
applyTo: "**/*.gradle"
---

# Gradle conventions

All `src/*` Gradle projects share two repo config files: **`eng/gradle/plugin-repositories.gradle`** (for `pluginManagement.repositories`) and **`eng/gradle/dependency-repositories.gradle`** (for `dependencyResolutionManagement.repositories`). Never hard-code Maven URLs (`mavenCentral()`, `google()`, `pkgs.dev.azure.com/...`, etc.) in `build.gradle`/`settings.gradle`.

## settings.gradle template

```groovy
pluginManagement {
    apply from: "${rootDir}/../../eng/gradle/plugin-repositories.gradle", to: pluginManagement
}
plugins {
    id 'com.microsoft.azure.artifacts.credprovider' version '1.1.1'
}
dependencyResolutionManagement {
    apply from: "${rootDir}/../../eng/gradle/dependency-repositories.gradle", to: dependencyResolutionManagement
}
rootProject.name = '<project>'
```

`build.gradle` files must not declare their own `repositories { ... }`.

## CI vs local

Both files switch on `System.getenv('RunningOnCI')` (or `RUNNINGONCI` — AzDO uppercases env vars on Linux/macOS agents):

- **`RunningOnCI=true`** (Azure DevOps, set in `build-tools/automation/yaml-templates/variables.yaml`) → dnceng `dotnet-public-maven` feed (CFSClean isolation, https://aka.ms/1es/netiso/CFS). Anonymous read of cached packages.
- **unset** (local, Dependabot, GitHub Actions) → `google()` + `mavenCentral()` + `gradlePluginPortal()` for plugins, `google()` + `mavenCentral()` for deps. No credentials needed.

Test the CI path locally: `$env:RunningOnCI='true'` (PowerShell) or `RunningOnCI=true ...` (bash).

## When CI fails 401 on a Dependabot bump

The new package isn't cached in the dnceng `dotnet-public-maven` feed yet. The CFSClean-isolated CI agents only do anonymous reads, so someone has to authenticate once locally to make the feed pull the package (and all its transitive deps) from upstream.

### Recommended: mirror via `az` bearer token

This is the most reliable path. It uses your `az login` Azure DevOps OAuth token directly via an HTTP Bearer header, so it bypasses every credprovider/session-token edge case.

```powershell
# Make sure you're logged in (corp account, MFA-satisfied)
az login

cd <repo-root>
$env:ANDROID_HOME = '<path-to-Android-SDK>'   # e.g. D:\android-toolchain\sdk
$env:RunningOnCI  = 'true'

# Project that needs the new package — must be the SAME project, not a sibling.
# (A failing AGP transitive dep in /tests/.../JavaLib cannot be mirrored by
# running gradle in /src/manifestmerger; the feed wants the request to come
# from the configuration that actually needs it.)
cd <path/to/the/gradle/project>
$projGradle = "..\..\..\..\..\build-tools\gradle\gradlew.bat"   # adjust depth

function Mirror-FailedUrls($logPath) {
    $urls = Select-String -Path $logPath -Pattern "Could not GET 'https://pkgs\.dev\.azure\.com/dnceng/[^']+'" -AllMatches |
        % { $_.Matches } | % { $_.Value -replace "^Could not GET '","" -replace "'$","" } | Sort-Object -Unique
    if ($urls.Count -eq 0) { return 0 }
    $token = az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv
    $h = @{ Authorization = "Bearer $token" }
    foreach ($u in $urls) { Invoke-WebRequest -Uri $u -Headers $h -SkipHttpErrorCheck | Out-Null }
    Write-Host "Mirrored $($urls.Count) packages"
    return $urls.Count
}

# Loop: build → mirror everything that 401'd → build again. Maven resolves the
# graph breadth-first, so each iteration uncovers the next layer of transitive
# deps. Typically converges in 3-5 iterations.
for ($i = 1; $i -le 15; $i++) {
    $log = "build-iter-$i.log"
    & $projGradle <task-that-resolves-deps> --no-daemon --refresh-dependencies *>&1 | Tee-Object $log | Out-Null
    if ((Get-Content $log -Tail 5) -match "BUILD SUCCESSFUL") { Write-Host "Done!"; break }
    if ((Mirror-FailedUrls $log) -eq 0) { Get-Content $log -Tail 30; throw "Failed but no 401s — different problem" }
}
```

The resource id `499b84ac-1321-427f-aa17-267ca6975798` is Azure DevOps. After successful ingestion each package is anonymous-readable, so future CI runs pass without any auth.

After mirroring, no PR edit is needed — just re-run the failed CI job.

### Older alternative: the artifacts-credprovider plugin

This sometimes works for simple cases (e.g. a single plugin-marker pull) but has unresolved auth gaps for some packages — observed with `com.android.tools.lint:lint-gradle` and its transitive deps, which 401 even with a properly attached `VssSessionToken`. Prefer the `az` flow above; fall back to credprovider only if you can't get an `az login` working.

```powershell
iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }"   # one-time
Remove-Item "$env:LOCALAPPDATA\MicrosoftCredentialProvider\SessionTokenCache.dat" -ErrorAction SilentlyContinue
$env:RunningOnCI = 'true'
$env:NUGET_CREDENTIALPROVIDER_VSTS_TOKENTYPE = 'SelfDescribing'   # required for plugin markers
./build-tools/gradle/gradlew.bat --project-dir <project-dir> build --no-daemon
```

The credprovider plugin is a no-op when no AzDO repos are configured (i.e. local builds without `RunningOnCI`).

## Don'ts

- Don't hard-code Maven repo URLs in `build.gradle` / `settings.gradle`; use the shared file.
- Don't wrap `plugins {}` in `if (...)` — Gradle rejects it.
- Don't use modern `plugins { id 'com.android.application' version '...' }` DSL without confirming the plugin is in `dotnet-public-maven`; prefer `buildscript { ... } / apply plugin: '...'` when in doubt.