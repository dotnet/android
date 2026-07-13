# Testing Review Rules

Guidance for test code. The repo-specific conventions (e.g., `BaseTest`,
`dotnet-local`) are included here alongside general best practices.

---

## Testing Checks

| Check | What to look for |
|-------|-----------------|
| **Inherit from `BaseTest`** | Test fixtures should inherit from `BaseTest` (provides `Root`, `TestName`, SDK paths, platform helpers). |
| **NUnit conventions** | Use `[TestFixture]`, `[Test]`, `[NonParallelizable]` (for tests that hang without it). |
| **Test with `dotnet-local`** | Tests must run via `dotnet-local.cmd`/`dotnet-local.sh` to use the locally built SDK. |
| **Bug fixes need regression tests** | Every PR that fixes a bug should include a test that fails without the fix and passes with it. If the PR description says "fixes #N" but adds no test, ask for one. |
| **Test assertions must be specific** | `Assert.IsNotNull(result)` or `Assert.IsTrue(success)` don't tell you what went wrong. Prefer `Assert.AreEqual(expected, actual)` or NUnit constraints (`Assert.That` with `Does.Contain`, `Is.EqualTo`, etc.) for richer failure messages. |
| **Deterministic test data** | Tests should not depend on system locale, timezone, or current date. Use explicit `CultureInfo.InvariantCulture` and hardcoded dates when testing formatting. |
| **Test edge cases** | Empty collections, null inputs, boundary values, concurrent calls, and very large inputs should all be considered. If the PR only tests the happy path, suggest edge cases. |
| **Android tools SDK/JDK fixtures** | Tests under `external/xamarin-android-tools/tests/` commonly build isolated fake SDK/JDK layouts and platform-specific tool scripts (`.bat` on Windows, shell scripts on Unix). Keep these fixtures self-contained and cleaned up in setup/teardown rather than depending on the developer machine's installed SDK/JDK. |
| **Generator tests must include Invoker types** | Tests for generated binding code (under `external/Java.Interop/tools/generator/` and related test projects) should verify both the interface/class output and the `*Invoker` type behavior. Invoker codegen has historically had subtle bugs with default interface methods and virtual dispatch. |
| **JVM-dependent tests** | Tests that require a running JVM must be in projects that configure the JVM environment (e.g., `Java.Interop-Tests`). Verify that test classes requiring a JVM are not placed in unit-test-only projects, where they will silently skip or fail with obscure errors. |
| **Expected codegen output tests** | Generator tests that compare against expected output files should be updated when the expected format changes. Stale expected-output files cause spurious test failures that mask real regressions. |
