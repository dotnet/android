---
applyTo: "**/*.gradle"
---

# Gradle conventions

All `src/*` Gradle projects share one repo config: **`eng/gradle/repositories.gradle`**. Never hard-code Maven URLs (`mavenCentral()`, `google()`, `pkgs.dev.azure.com/...`, etc.) in `build.gradle`/`settings.gradle`.

## settings.gradle template

```groovy
pluginManagement {
    apply from: "${rootDir}/../../eng/gradle/repositories.gradle", to: pluginManagement
}
plugins {
    id 'com.microsoft.azure.artifacts.credprovider' version '1.1.1'
}
dependencyResolutionManagement {
    apply from: "${rootDir}/../../eng/gradle/repositories.gradle", to: dependencyResolutionManagement
}
rootProject.name = '<project>'
```

`build.gradle` files must not declare their own `repositories { ... }`.

## CI vs local

`repositories.gradle` switches on `System.getenv('RunningOnCI')`:

- **`RunningOnCI=true`** (Azure DevOps, set in `build-tools/automation/yaml-templates/variables.yaml`) → dnceng `dotnet-public-maven` feed (CFSClean isolation, https://aka.ms/1es/netiso/CFS). Anonymous read of cached packages.
- **unset** (local, Dependabot, GitHub Actions) → `google()` + `mavenCentral()` + `gradlePluginPortal()`. No credentials needed.

Test the CI path locally: `$env:RunningOnCI='true'` (PowerShell) or `RunningOnCI=true ...` (bash).

## When CI fails 401 on a Dependabot bump

The new package isn't cached in the feed yet. One-time setup, then ingest:

1. `iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }"` (or the `.sh` equivalent)
2. `$env:RunningOnCI='true'; ./build-tools/gradle/gradlew.bat --project-dir src/<project> build` — sign in via the device-flow prompt; the feed proxies + caches the package.
3. Re-run CI on the Dependabot PR. No PR edit needed.

The credprovider plugin is a no-op when no AzDO repos are configured (i.e. local builds without `RunningOnCI`).

## Don'ts

- Don't hard-code Maven repo URLs in `build.gradle` / `settings.gradle`; use the shared file.
- Don't wrap `plugins {}` in `if (...)` — Gradle rejects it.
- Don't use modern `plugins { id 'com.android.application' version '...' }` DSL without confirming the plugin is in `dotnet-public-maven`; prefer `buildscript { ... } / apply plugin: '...'` when in doubt.