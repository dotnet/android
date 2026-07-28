---
applyTo: "**/*.gradle,**/*.gradle.kts"
---

# Gradle conventions

All `src/*` Gradle projects share two repo config files: **`eng/gradle/plugin-repositories.gradle`** (for `pluginManagement.repositories`) and **`eng/gradle/dependency-repositories.gradle`** (for `dependencyResolutionManagement.repositories`). Never hard-code Maven URLs (`mavenCentral()`, `google()`, `pkgs.dev.azure.com/...`, etc.) in `build.gradle`/`settings.gradle`.

## settings.gradle template

```groovy
// See: eng/gradle/plugin-repositories.gradle, eng/gradle/dependency-repositories.gradle
pluginManagement {
    apply from: "${rootDir}/../../eng/gradle/plugin-repositories.gradle", to: pluginManagement
}
dependencyResolutionManagement {
    apply from: "${rootDir}/../../eng/gradle/dependency-repositories.gradle", to: dependencyResolutionManagement
}
rootProject.name = '<project>'
```

Adjust the `../..` depth to reach the repo root from that project; it is not
always two levels (e.g. `external/Java.Interop/tools/java-source-utils` uses
four).

Kotlin DSL (`settings.gradle.kts`) applies the same two Groovy files, but passes
the receiver as `to = this`:

```kotlin
// See: eng/gradle/plugin-repositories.gradle, eng/gradle/dependency-repositories.gradle
pluginManagement {
    apply(from = "$rootDir/../../eng/gradle/plugin-repositories.gradle", to = this)
}
dependencyResolutionManagement {
    apply(from = "$rootDir/../../eng/gradle/dependency-repositories.gradle", to = this)
}
rootProject.name = "<project>"
```

`build.gradle` files must not declare their own `repositories { ... }`.

## CI vs local

Both files switch on `System.getenv('RUNNINGONCI')`. Azure DevOps exports the
`RunningOnCI` pipeline variable under this normalized environment-variable name.

- **`RUNNINGONCI=true`** (Azure DevOps, sourced from `RunningOnCI` in `build-tools/automation/yaml-templates/variables.yaml`) → dnceng `dotnet-public-maven` feed (CFSClean isolation, https://aka.ms/1es/netiso/CFS). Anonymous read of cached packages.
- **unset** (local, Dependabot, GitHub Actions) → `google()` + `mavenCentral()` + `gradlePluginPortal()` for plugins, `google()` + `mavenCentral()` for deps. No credentials needed.

CI reads cached packages from the mirror anonymously. `mirror-dependencies.ps1`
runs the same anonymous Gradle resolution, then seeds each missing URL with an
authenticated HTTP request until the build succeeds.

Test the CI path locally: `$env:RUNNINGONCI='true'` (PowerShell) or `RUNNINGONCI=true ...` (bash).

## When CI fails 401 on a Dependabot bump

The new package isn't cached in the dnceng `dotnet-public-maven` feed yet. CI agents only do anonymous reads, so someone has to authenticate once locally to make the feed pull the package (and its transitive deps) from upstream.

Use the helper script — it runs the build, parses any 401 URLs out of the log, re-fetches each one with an Azure DevOps OAuth token using Basic authentication (so the feed mirrors it), and loops until the build succeeds:

```powershell
az login   # one-time, corp account with MFA satisfied

pwsh ./eng/gradle/mirror-dependencies.ps1 `
    -ProjectDir <path-to-failing-gradle-project> `
    -Task <gradle-task-CI-runs> `
    -GradleWrapper <path-to-wrapper-CI-runs> `
    -AndroidHome <path-to-Android-SDK>   # required for any com.android.* project
```

The mirror must run in the project that actually needs the new package — a sibling project's build won't trigger a mirror for someone else's deps. If that project uses a different Gradle wrapper in CI, pass the same wrapper with `-GradleWrapper`; Kotlin and other plugins publish Gradle-version-specific variants. Typical convergence is 2-5 iterations as the resolver walks the dep graph breadth-first.

After it succeeds, just re-run the failed CI job. No PR edits needed — the packages are now anonymous-readable forever.

Tests that resolve Maven files without Gradle can seed coordinates directly:

```powershell
pwsh ./eng/gradle/mirror-dependencies.ps1 `
    -MavenArtifact 'androidx.core:core:1.12.0'
```

This attempts the coordinate's POM, JAR, AAR, and Gradle module metadata. Append
the exact filename as a fourth segment for a nonstandard payload.

## Tests

Tests must not reach the public internet on CI; everything routes through the
mirror. Two mechanisms in `Xamarin.ProjectTools` handle this, and both apply
unconditionally — local runs hit the same URLs as CI, so a package the mirror
lacks fails everywhere instead of only on CI:

- **Generated Gradle projects** — `AndroidGradleProject` writes a
  `settings.gradle.kts` that applies the same two shared config files by
  absolute path, and copies the repository wrapper from `build-tools/gradle`
  instead of running `gradle init`. Don't reintroduce `google()` /
  `mavenCentral()` into generated projects, and don't let a generated project
  download its own Gradle distribution on CI.
- **Non-Gradle Maven downloads** — use `TestEnvironment.DotNetPublicMaven` as
  the base URL, both for `WebContent` on a `BuildItem` and for `Repository`
  metadata on an `<AndroidMavenLibrary>`. Don't write a `repo1.maven.org` or
  `maven.google.com` URL into a test, and don't use the `"Central"` / `"Google"`
  shorthands there — those are covered without network by
  `MavenDownloadTests.KnownRepositoryShorthand`.

When a test needs a coordinate the feed hasn't cached, seed it with
`-MavenArtifact` above rather than pointing the test at a public repository.

## Don'ts

- Don't hard-code Maven repo URLs in `build.gradle` / `settings.gradle`; use the shared file.
- Don't use modern `plugins { id 'com.android.application' version '...' }` DSL without confirming the plugin is in `dotnet-public-maven`; prefer `buildscript { ... } / apply plugin: '...'` when in doubt.
- Don't add a Gradle credential provider or any authenticated repository to a
  build. CI resolves anonymously; authentication belongs only in
  `mirror-dependencies.ps1`, which seeds the feed over plain HTTP.