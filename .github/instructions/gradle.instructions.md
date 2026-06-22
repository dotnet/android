---
applyTo: "**/*.gradle"
---

# Gradle Build Conventions

All Gradle projects in this repo (under `src/`) follow a shared
configuration. Read this before editing any `build.gradle` or
`settings.gradle` file.

## Shared repository configuration

Project Maven dependencies and Gradle plugins are resolved through a single
shared file: **`eng/gradle/repositories.gradle`**. Do not hard-code Maven
repository URLs in individual `build.gradle` or `settings.gradle` files.

Each `settings.gradle` applies the shared file to both `pluginManagement` and
`dependencyResolutionManagement`:

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

Individual `build.gradle` files should **not** contain `repositories { ... }`
blocks — the settings-level `dependencyResolutionManagement` provides them.

## CI vs local resolution

`eng/gradle/repositories.gradle` switches on `System.getenv('RunningOnCI')`:

- **`RunningOnCI=true`** (Azure DevOps CI, set in
  `build-tools/automation/yaml-templates/variables.yaml`): resolves through
  the dnceng Azure Artifacts feed (`dotnet-public-maven`) for CFSClean
  network isolation compliance — see https://aka.ms/1es/netiso/CFS.
- **`RunningOnCI` unset** (local builds, Dependabot, GitHub Actions): uses
  standard public repos (`google()`, `mavenCentral()`, `gradlePluginPortal()`)
  so contributors don't need any feed credentials.

CI reads the dnceng feed **anonymously**. Once a package has been pulled
through the feed by an authenticated request (any maintainer), it is cached
and anonymous reads work forever after.

## Testing the CI path locally

```powershell
$env:RunningOnCI = 'true'
./build-tools/gradle/gradlew.bat --project-dir src/<project> build
```

```bash
RunningOnCI=true ./build-tools/gradle/gradlew --project-dir src/<project> build
```

## Ingesting a new package (when CI fails 401)

When Dependabot bumps a Gradle dependency, CI may fail with `401 Unauthorized`
on the new version because it isn't cached in the dnceng feed yet. To fix:

1. Install the Azure Artifacts credential provider (one-time):
   ```powershell
   iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }"
   ```
2. Set `RunningOnCI=true` and re-run the affected gradle build locally.
   The credprovider plugin (declared in `settings.gradle`) will prompt for
   a device-flow login at https://aka.ms/devicelogin. Sign in with your
   Microsoft account; the feed then proxies and caches the package.
3. Re-run CI on the Dependabot PR — anonymous reads will now succeed.

No PR edit is required. The credprovider plugin is a no-op on local
non-CI builds (no AzDO repos are configured for it to authenticate).

## Don't

- **Don't** add `maven { url 'https://repo.maven.apache.org/...' }`,
  `mavenCentral()`, `google()`, or `jcenter()` directly in a `build.gradle`
  or settings file — use the shared `eng/gradle/repositories.gradle` instead.
- **Don't** copy hard-coded `pkgs.dev.azure.com/dnceng/...` URLs into new
  Gradle files. The feed URL lives in exactly one place.
- **Don't** wrap the `plugins {}` block in `if (...)` — Gradle rejects it.
  The credprovider plugin must be declared unconditionally.
- **Don't** use the modern `plugins { id 'com.android.application' version
  '...' }` DSL without verifying the plugin is resolvable from
  `dotnet-public-maven` in CI. The legacy
  `buildscript { ... } / apply plugin: '...'` style is sometimes safer.
