# Testing Review Rules

Guidance for test code. The repo-specific conventions (e.g., `BaseTest`,
`dotnet-local`) are included here alongside general best practices.

---

## Testing Checks

| Check | What to look for |
|-------|-----------------|
| **`BaseTest`** | Build integration fixtures that need its project, SDK, or platform helpers can follow existing `BaseTest` patterns; standalone tooling fixtures need not inherit it. |
| **Test framework** | Follow the fixture's NUnit or MSTest conventions; disable parallel execution only when needed. |
| **Test command** | See the `tests` skill: standalone tooling uses `dotnet test`; full-build tests use the local SDK. |
| **Tests must earn their place** | Prefer existing coverage and tests of meaningful behavior or regressions; refactors can need characterization tests. Avoid duplicates, brittle GC-timing tests, and tests that only assert trivial implementation details or removed code. |
| **Test assertions** | Assert the relevant behavior and add diagnostic context when a failure would otherwise be ambiguous. |
| **Deterministic test data** | Tests should not depend on system locale, timezone, or current date. Use explicit `CultureInfo.InvariantCulture` and hardcoded dates when testing formatting. |
| **Android tools SDK/JDK fixtures** | Tests under `tests/Xamarin.Android.Tools.AndroidSdk-Tests/` commonly build isolated fake SDK/JDK layouts and platform-specific tool scripts (`.bat` on Windows, shell scripts on Unix). Keep these fixtures self-contained and cleaned up in setup/teardown rather than depending on the developer machine's installed SDK/JDK. |
| **Generator Invoker tests** | When a change affects invoker generation, cover the relevant interface and `*Invoker` behavior, including default methods or virtual dispatch where applicable. |
| **JVM-dependent tests** | Tests that require a running JVM must be in projects that configure the JVM environment (e.g., `Java.Interop-Tests`). Verify that test classes requiring a JVM are not placed in unit-test-only projects, where they will silently skip or fail with obscure errors. |
| **Expected codegen output tests** | Generator tests that compare against expected output files should be updated when the expected format changes. Stale expected-output files cause spurious test failures that mask real regressions. |
| **Test nested Java/JNI names at boundaries** | Name-conversion tests must include a `$` nested type and assert the final manifest, map, rule, or generated-source output—not only the helper. |
| **Retain temporary peers in JNI tests** | Don't chain `.Class.Handle` on temporaries (e.g., `peer.Class.Handle`): on CoreCLR/NativeAOT, `JavaMarshalGCBridge` deletes grefs of unreachable peers (even `java.lang.Class`), causing CheckJNI aborts. Reuse `using var klass = peer.Class;` (or `GC.KeepAlive`). |
| **No GC-timing tests for `GC.KeepAlive` fixes** | Don't require or add `GC.Collect ()`, `WeakReference.IsAlive`, or GC stress tests — JIT liveness and GC timing aren't deterministic and make tests flaky. Use deterministic coverage only where it catches a real regression: codegen expected-output assertions, `JniEnvironment.LocalReferenceCount` leak checks, or targeted functional tests. |
