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

The new package isn't cached in the feed yet. One-time setup, then ingest:

1. `iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }"` (or the `.sh` equivalent)
2. ```powershell
   $env:RunningOnCI='true'
   $env:NUGET_CREDENTIALPROVIDER_VSTS_TOKENTYPE='SelfDescribing'
   ./build-tools/gradle/gradlew.bat --project-dir src/<project> build
   ```
   Sign in via the popup; the feed proxies + caches the package. `SelfDescribing` is required — the default `Compact` token is rejected by the feed when ingesting plugin markers (e.g. AGP plugin from `pluginManagement`).
3. Re-run CI on the Dependabot PR. No PR edit needed.

If the popup never appears or auth keeps cancelling, clear the cached session token and try again:
```powershell
Remove-Item "$env:LOCALAPPDATA\MicrosoftCredentialProvider\SessionTokenCache.dat" -ErrorAction SilentlyContinue
```

### Fallback: mirror via `az` bearer token

Some packages (observed with `com.android.tools.lint:lint-gradle` and its transitive deps) get 401-rejected even with a successfully attached credprovider `VssSessionToken`. When that happens, mirror the failing URLs directly using an Azure DevOps OAuth token from `az`:

```powershell
$token = az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv
$h = @{ Authorization = "Bearer $token" }

# Pull each failing URL from the gradle error output and re-request it with the bearer token:
$urls = Select-String -Path <gradle.log> -Pattern "Could not GET 'https://pkgs\.dev\.azure\.com/dnceng/[^']+'" -AllMatches |
    % { $_.Matches } | % { $_.Value -replace "^Could not GET '","" -replace "'$","" } | Sort-Object -Unique
foreach ($u in $urls) { Invoke-WebRequest -Uri $u -Headers $h -SkipHttpErrorCheck | % StatusCode }

# Repeat the gradle build to discover the next layer of transitive deps; the feed will
# return 401 for each new uncached package. Loop build → mirror → build until clean.
```

The resource id `499b84ac-1321-427f-aa17-267ca6975798` is Azure DevOps. After successful ingestion, the package is anonymous-readable, so future CI runs pass without any auth.

The credprovider plugin is a no-op when no AzDO repos are configured (i.e. local builds without `RunningOnCI`).

## Don'ts

- Don't hard-code Maven repo URLs in `build.gradle` / `settings.gradle`; use the shared file.
- Don't wrap `plugins {}` in `if (...)` — Gradle rejects it.
- Don't use modern `plugins { id 'com.android.application' version '...' }` DSL without confirming the plugin is in `dotnet-public-maven`; prefer `buildscript { ... } / apply plugin: '...'` when in doubt.