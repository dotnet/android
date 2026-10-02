# Testing Review Rules

Guidance for test code. The repo-specific conventions (e.g., `BaseTest`,
`dotnet-local`) are included here alongside general best practices.

---

## Testing Checks

| Check | What to look for |
|-------|-----------------|
| **Inherit from `BaseTest`** | Test fixtures should inherit from `BaseTest` (provides `Root`, `TestName`, SDK paths, platform helpers). |
| **NUnit conventions** | Use `[TestFixture]`, `[Test]`, `[NonParallelizable]` (for tests that hang without it). |
| **Use the matching test host** | SDK/device integration tests use `dotnet-local.cmd`/`dotnet-local.sh`; standalone suites can use the installed SDK as documented by the `tests` skill. Run the smallest relevant suite rather than requiring a full SDK build for every refactor. |
| **Validation proportional to the change** | For an API replacement, first adapt and run existing tests without weakening their behavioral assertions. New fixtures or test-only infrastructure need a specific coverage gap; they are not automatic requirements of swapping libraries. |
| **Bug fixes need regression evidence** | Normally add a focused regression test that fails before the fix. If the user explicitly excludes new tests, honor that scope and use existing tests or a bounded temporary reproduction; report coverage limits rather than silently treating the fix as verified. |
| **Test assertions must be specific** | `Assert.IsNotNull(result)` or `Assert.IsTrue(success)` don't tell you what went wrong. Prefer `Assert.AreEqual(expected, actual)` or NUnit constraints (`Assert.That` with `Does.Contain`, `Is.EqualTo`, etc.) for richer failure messages. |
| **Deterministic test data** | Tests should not depend on system locale, timezone, or current date. Use explicit `CultureInfo.InvariantCulture` and hardcoded dates when testing formatting. |
| **Test relevant edge cases** | Tie requests to changed behavior or a demonstrated risk, such as duplicate archive names or traversal after normalization. Do not demand an exhaustive empty/null/concurrent/large-input matrix merely because those cases are imaginable. |
| **Android tools SDK/JDK fixtures** | Tests under `tests/Xamarin.Android.Tools.AndroidSdk-Tests/` commonly build isolated fake SDK/JDK layouts and platform-specific tool scripts (`.bat` on Windows, shell scripts on Unix). Keep these fixtures self-contained and cleaned up in setup/teardown rather than depending on the developer machine's installed SDK/JDK. |
| **Generator tests must include Invoker types** | Tests for generated binding code (under `external/Java.Interop/tools/generator/` and related test projects) should verify both the interface/class output and the `*Invoker` type behavior. Invoker codegen has historically had subtle bugs with default interface methods and virtual dispatch. |
| **JVM-dependent tests** | Tests that require a running JVM must be in projects that configure the JVM environment (e.g., `Java.Interop-Tests`). Verify that test classes requiring a JVM are not placed in unit-test-only projects, where they will silently skip or fail with obscure errors. |
| **Expected codegen output tests** | Generator tests that compare against expected output files should be updated when the expected format changes. Stale expected-output files cause spurious test failures that mask real regressions. |
| **Test nested Java/JNI names at boundaries** | Name-conversion tests must include a `$` nested type and assert the final manifest, map, rule, or generated-source output—not only the helper. |
