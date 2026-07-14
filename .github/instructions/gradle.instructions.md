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
if (System.getenv('ANDROID_MIRROR_MAVEN_DEPENDENCIES') == 'true') {
    apply from: "${rootDir}/../../eng/gradle/credential-provider.gradle"
}
dependencyResolutionManagement {
    apply from: "${rootDir}/../../eng/gradle/dependency-repositories.gradle", to: dependencyResolutionManagement
}
rootProject.name = '<project>'
```

`build.gradle` files must not declare their own `repositories { ... }`.

## CI vs local

Both files switch on `System.getenv('RUNNINGONCI')`. Azure DevOps exports the
`RunningOnCI` pipeline variable under this normalized environment-variable name.

- **`RUNNINGONCI=true`** (Azure DevOps, sourced from `RunningOnCI` in `build-tools/automation/yaml-templates/variables.yaml`) → dnceng `dotnet-public-maven` feed (CFSClean isolation, https://aka.ms/1es/netiso/CFS). Anonymous read of cached packages.
- **unset** (local, Dependabot, GitHub Actions) → `google()` + `mavenCentral()` + `gradlePluginPortal()` for plugins, `google()` + `mavenCentral()` for deps. No credentials needed.

CI reads cached packages from the mirror anonymously. The Azure Artifacts
credential provider is loaded only when `ANDROID_MIRROR_MAVEN_DEPENDENCIES=true`;
`mirror-dependencies.ps1` sets this while seeding uncached packages.

Test the CI path locally: `$env:RUNNINGONCI='true'` (PowerShell) or `RUNNINGONCI=true ...` (bash).

## When CI fails 401 on a Dependabot bump

The new package isn't cached in the dnceng `dotnet-public-maven` feed yet. CI agents only do anonymous reads, so someone has to authenticate once locally to make the feed pull the package (and its transitive deps) from upstream.

Use the helper script — it runs the build, parses any 401 URLs out of the log, re-fetches each one with an Azure DevOps bearer token (so the feed mirrors it), and loops until the build succeeds:

```powershell
az login   # one-time, corp account with MFA satisfied

pwsh ./eng/gradle/mirror-dependencies.ps1 `
    -ProjectDir <path-to-failing-gradle-project> `
    -Task <gradle-task-CI-runs> `
    -AndroidHome <path-to-Android-SDK>   # required for any com.android.* project
```

The mirror must run in the project that actually needs the new package — a sibling project's build won't trigger a mirror for someone else's deps. Typical convergence is 2-5 iterations as the resolver walks the dep graph breadth-first.

After it succeeds, just re-run the failed CI job. No PR edits needed — the packages are now anonymous-readable forever.

## Don'ts

- Don't hard-code Maven repo URLs in `build.gradle` / `settings.gradle`; use the shared file.
- Don't use modern `plugins { id 'com.android.application' version '...' }` DSL without confirming the plugin is in `dotnet-public-maven`; prefer `buildscript { ... } / apply plugin: '...'` when in doubt.