# Native Android MSTest source-generation proof

This isolated application tests the native MSTest/Microsoft.Testing.Platform
integration. It does not migrate `Mono.Android.NET-Tests`, Java.Interop's NUnit
suite, or JCW tests. Its scoped package pins are MSTest and SourceGeneration
`4.6.0-preview.26524.1` and MTP `2.6.0-preview.26524.1`; product pins are unchanged.

The app registers its own assembly and both referenced test-library assemblies
together through `AddMSTest`. Each project references the generator directly.
Runtime packages explicitly specify `Publish="true"` because the SDK otherwise
excludes `PrivateAssets="all"` packages from Android's publish/deployment closure.
The generator remains analyzer-only and private.

## Cases and evidence

| Selection | Rows | Expected outcome |
| --- | ---: | --- |
| `proof` (default) | 17 | 15 passed, 2 skipped |
| `diagnostic` | 3 | 3 deliberate failures, native exit 2 |
| `proof,diagnostic` | 20 | 15 passed, 2 skipped, 3 deliberate failures |
| `jni` | 3 | 3 passed |
| `library` | 3 | 2 passed, 1 inconclusive/skip |

Coverage includes inherited `TestContext` assignment and initialization/cleanup
in all three assemblies, ignored versus inconclusive lifecycle, Task and
ValueTask, duplicate DataRow arguments, DynamicData, matching display names,
Android bindings, raw JNI string-reference disposal, real TRX attachments and
multiline failure diagnostics with generated invocation stacks.

Native UIDs, not display names, identify results. `D.Nodes.xml` preserves every
native update, its state and native method metadata. On this Android runtime the
adapter's native assembly-full-name field is empty; the separate defining
assembly comes from the explicitly registered fixture `Type`, with namespace
and type-name matching and rejection of unknown/mismatched metadata.

Execution includes native discovery through MSTest's source runner and records
every resulting native UID. Standalone `--list-tests json` was a separate
experiment: CoreCLR emitted the expected identities, but NativeAOT did not yield
capturable JSON. That surface is not claimed as proven, and `-e list` is rejected.
Neither console redirection nor reflection observers remain in this app.

The instrumentation publishes the shared Android streaming protocol and native
TRX path. It recreates only this app's external `TestResults` directory per run.
Result files, including MSTest's initially owner-only attachments, are explicitly
made readable for ADB export. These files contain synthetic proof evidence.
Attachment creation uses Android's explicit external directory: bundled
assemblies have an empty `Assembly.Location`, so MSTest's default
source-derived `TestContext.TestResultsDirectory` is not a portable writable
Android directory. This proof does not replace that framework behavior or
establish a general NUnit synchronization-context compatibility layer.

## Running

Prepare/build the local SDK normally, then use an explicitly owned device and
ADB server. Do not borrow another session's writable toolchain, caches or AVD.
For example, with `ANDROID_SERIAL` and `ADB_SERVER_SOCKET` already configured:

```sh
./dotnet-local.sh build tests/MSTest.Android.Tests/MSTest.Android.Tests.csproj \
  -t:Install -c Release "-p:AdbTarget=-s $ANDROID_SERIAL" \
  -p:PublishTrimmed=true -p:TrimMode=full \
  -p:EmitCompilerGeneratedFiles=true \
  -p:CompilerGeneratedFilesOutputPath=obj/d-generated/Release

adb -s "$ANDROID_SERIAL" logcat -c
adb -s "$ANDROID_SERIAL" shell am instrument -w -r \
  -e include proof,diagnostic \
  net.dot.android.mstestproof/net.dot.android.mstestproof.Instrumentation
adb -s "$ANDROID_SERIAL" logcat -d -v threadtime
```

Category selection also accepts
the `IncludeCategories` runtime configuration setting; this bounded runner
supports simple alphanumeric/hyphen category names joined by comma or semicolon.
It rejects unsupported expressions rather than silently interpreting them.

Capture `instrumentation.txt`, `logcat.txt`, the returned counts/exit/path bundle
as `bundle.json`, the returned `D.Nodes.xml` and native `.trx`, and the complete
external `TestResults` directory as `device-results`. For execution directories
captured in that layout:

```sh
python3 tests/MSTest.Android.Tests/verify-results.py \
  CORECLR_ROOTED CORECLR_ROOTLESS AOT_ROOTED AOT_ROOTLESS
```

Use `--selection proof` for default-category runs. The verifier checks the exact
method/row shape, unique and stable native UIDs, outcomes, TRX and wire identity
agreement, lifecycle multiplicity, duplicate argument values, attachment
content/linkage, generated diagnostic stacks and absence of the temporary
runtime observer.

To exercise the actual project-mode Android MTP host (default `proof` selection),
select MTP explicitly and use an absolute project path:

```sh
DOTNET_TEST_RUNNER=Microsoft.Testing.Platform ./dotnet-local.sh test \
  --project "$PWD/tests/MSTest.Android.Tests/MSTest.Android.Tests.csproj" \
  --no-build -c Release --report-trx \
  "-p:AdbTarget=-s $ANDROID_SERIAL"
```

Pass the same runtime/build properties to both build and test. In particular,
add `-p:PublishAot=true -p:Optimize=true -p:PublishTrimmed=true -p:TrimMode=full`
to both commands for NativeAOT. Both project-mode CoreCLR and NativeAOT produced
15 passed and 2 skipped rows. The console's `net11.0|arm64` label describes the
host tool; the installed app targets `net12.0-android`.

## Trimming and runtime boundary

The app has no manual whole-test-assembly discovery roots. Compare it against an
experimental import containing `TrimmerRootAssembly` with `RootMode="All"` for
`MSTest.Android.Tests`, `MSTest.Android.JavaInterop.Tests` and
`MSTest.Android.TestLibrary.Tests`. Run both with full trimming; then repeat
with `-p:PublishAot=true -p:Optimize=true`. Verify the produced APK contains the
NativeAOT application library, not CoreCLR, rather than relying on flags alone.

At the selected integrated head, observer-free full-trim CoreCLR and actual
NativeAOT on API 35 arm64 produced the same 20 UID/metadata/outcome rows with
and without those three experimental roots. Both native ABIs (arm64 and x86_64)
compiled; device execution covered arm64 only. The .NET 12 SDK has CoreCLR and
NativeAOT runtime imports, not a Mono runtime import.

Separate observed runs verified active `SourceGeneratedReflectionOperations`,
generated constructors, test/lifecycle invokers and inherited context setters
from all three assemblies. Generated descriptor completeness remained **false**.
The temporary observer is not part of this application. Generated dynamic
dependencies and bounded fallback reflection remain; compiler output and these
results do not prove universally reflection-free discovery or eliminate
framework trimming/AOT warnings.

This result supersedes only those experimental fixture-discovery roots.
Existing NUnit whole-assembly roots, RD.xml/complete metadata, converter and
reflection roots, StartupHook, JNI and resource roots elsewhere remain
independent and untouched until their actual consumers are removed and tested.
