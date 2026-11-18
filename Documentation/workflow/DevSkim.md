# DevSkim banned-API analysis

The `DevSkim` stage of the CI and PR pipelines runs
[DevSkim](https://github.com/microsoft/DevSkim) over the source code and build
tooling of the product, and fails when it finds a banned API, such as a banned C
function. It supports the Microsoft.Security.SystemsADM.10082 requirement.
A clean scan is only part of meeting that requirement. See [Limits](#limits).

CodeQL runs separately, in the nightly pipeline, and isn't affected.

## Pipelines

The stage is defined in `build-tools/automation/yaml-templates/stage-devskim.yaml`.
It is the last stage of these pipelines:

| Pipeline                                               | Runs for                                       |
|--------------------------------------------------------|------------------------------------------------|
| `build-tools/automation/azure-pipelines-public.yaml`   | Pull requests, and the nightly build of `main` |
| `build-tools/automation/azure-pipelines.yaml`          | CI builds on DevDiv                            |
| `build-tools/automation/azure-pipelines-internal.yaml` | CI builds and pull requests on dnceng/internal |

The scan needs no build, so the stage has no dependencies. It runs on a Linux
agent next to the build stages, and doesn't hold up packing or publishing.

## What is scanned

DevSkim scans these directories, one scan per directory:

  * `src`: the runtime, build tasks, bindings and tools.
  * `build-tools`: build and CI tooling.
  * `eng`: engineering scripts from dotnet/arcade.
  * `external`: files imported from other Microsoft repositories (only
    localization files today).
  * `tools`: developer tools.

These are out of scope:

  * `tests`, `samples`, `Documentation`, `docs` and `Localize`: tests, samples,
    documentation and translations.
  * `src-ThirdParty`: third-party code, such as the Android platform tools.
  * Git submodules. The stage doesn't check them out, and the only one,
    `external/android-api-docs`, is documentation.

`build-tools/automation/scripts/devskim-options.json` lists the files that are
skipped inside the scanned directories: the unit tests in `src/*/Tests`, the
`external/android-api-docs` documentation for checkouts that have the
submodule, and `.git`. Patterns match the full path of a file, so keep them
specific: a pattern like `**/Tests/**` would skip a whole checkout that sits
below a directory called `Tests`.

## What fails the stage

DevSkim exits with 0 when it reports findings, so the stage checks the SARIF
reports with `build-tools/automation/scripts/check_devskim.py`. It fails on:

  * every result tagged `API.DangerousAPI.BannedFunction`, at any severity or
    confidence. This includes the moderate `DS154189`, which covers most banned
    functions, and the important `DS185832` for `strcpy`.
  * a report that is missing or malformed.
  * no results at all in the report of `src`, which has the native code, since
    DevSkim also exits with 0 when it analyzes no files.

Inline suppressions (`DevSkim: ignore ...`) are disabled with
`--disable-supression`, which is how the CLI spells it, so a comment can't hide a
finding. There is no baseline, and no exclusion for an individual API: an
approved exception needs a reviewed change, such as a pattern for its file in
`devskim-options.json`. Other findings stay in the reports for triage, but don't
fail the stage. For instance, DevSkim 1.0.100 reports `strlen`, `memcpy` and
`malloc` as problematic, but not banned.

Before the scan, the stage runs DevSkim with the same settings over a generated C
file that calls `strcpy`, and requires DevSkim to report it. Without this check, a
clean scan could mean that the scanner or its rules don't work, rather than that
the code is clean.

## Limits

A clean scan doesn't prove that the product has no banned APIs:

  * DevSkim has banned-API rules only for C, C++ and Objective-C, so the stage
    doesn't check C#, Java or Kotlin for banned APIs.
  * DevSkim 1.0.100 reports `strlen` as a problematic function (`DS140021`), and
    that hides the banned-function rule for `strlen` (`DS154192`). `strlen` calls
    don't fail the stage.

## Fixing a finding

Fix the code rather than excluding it. DevSkim matches function names, so a
finding isn't always a bug, and some fixes only change what the code calls. The
native code uses these replacements:

| Finding                            | Replacement                                    |
|------------------------------------|------------------------------------------------|
| `strerror`                         | `ErrorMessage`                                 |
| `snprintf` of a number             | `std::to_chars`                                |
| `snprintf` of strings              | `Util::format_concatenated`                    |
| `fprintf` into a stream            | `fputs` and `fputc`                            |
| `fopen`                            | `open` and `fdopen`                            |
| `printf` in `format (printf, ...)` | `format (__printf__, ...)`: the same attribute |

`ErrorMessage` and `Util::format_concatenated` are in
`src/native/clr/include/runtime-base/util.hh`.

## Reports

The `DevSkim` pipeline artifact has the SARIF report and the console log of each
scan, and the log of the tool installation. It is published even when the stage
fails.

## Run it locally

DevSkim runs on .NET 8, 9 or 10, and has all the files of a scan open at once, so
raise the open file limit first (macOS defaults to 256). If it runs out of open
files, it aborts with a logger stack trace that says `Could not load file or
assembly 'System.Diagnostics.DiagnosticSource'`. The pipelines use the .NET 10
SDK.

Install the pinned version from outside the repository, so that `global.json`
doesn't ask for its SDK:

```shell
(cd /tmp && dotnet tool install Microsoft.CST.DevSkim.CLI --version 1.0.100 \
  --tool-path /tmp/devskim-tool --configfile "<repository>/NuGet.config")
```

Then scan each directory from the root of the repository, with the reports written
outside of it, and check them:

```shell
ulimit -n 8192
mkdir -p /tmp/devskim-reports
reports=()
for source in build-tools eng external src tools; do
  reports+=("/tmp/devskim-reports/$source.sarif")
  /tmp/devskim-tool/devskim analyze --source-code "$PWD/$source" --base-path "$PWD" \
    --output-file "/tmp/devskim-reports/$source.sarif" --file-format sarif \
    --severity Critical,Important,Moderate,BestPractice,ManualReview \
    --confidence High,Medium,Low --disable-supression --disable-parallel \
    --options-json build-tools/automation/scripts/devskim-options.json
done
python3 build-tools/automation/scripts/check_devskim.py \
  --require-results /tmp/devskim-reports/src.sarif "${reports[@]}"
python3 -B -m unittest discover -s build-tools/automation/scripts -p test_devskim.py
```

## Updating DevSkim

The version is set by `DevSkimVersion` in
`build-tools/automation/yaml-templates/variables.yaml`, and repeated in the
install command above. When you change it, run a real scan and the tests above,
and check that the flags and the tags that `check_devskim.py` reads
(`properties.tags` on each result) still exist. The stage fails if DevSkim stops
reporting the banned function in its test file.
