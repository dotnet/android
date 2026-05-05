# Testing Review Rules

Guidance for test code in dotnet/java-interop. Loaded when test files change.

---

## Testing Checks

| Check | What to look for |
|-------|-----------------|
| **NUnit conventions** | Use `[TestFixture]`, `[Test]`, `[NonParallelizable]` (for tests that hang without it). |
| **Bug fixes need regression tests** | Every PR that fixes a bug should include a test that fails without the fix and passes with it. If the PR description says "fixes #N" but adds no test, ask for one. |
| **Test assertions must be specific** | `Assert.IsNotNull(result)` or `Assert.IsTrue(success)` don't tell you what went wrong. Prefer `Assert.AreEqual(expected, actual)` or NUnit constraints (`Assert.That` with `Does.Contain`, `Is.EqualTo`, etc.) for richer failure messages. |
| **Deterministic test data** | Tests should not depend on system locale, timezone, or current date. Use explicit `CultureInfo.InvariantCulture` and hardcoded dates when testing formatting. |
| **Test edge cases** | Empty collections, null inputs, boundary values, concurrent calls, and very large inputs should all be considered. If the PR only tests the happy path, suggest edge cases. |
| **Generator tests must include Invoker types** | Tests for generated binding code should verify both the interface/class output and the `*Invoker` type behavior. Invoker codegen has historically had subtle bugs with default interface methods and virtual dispatch. |
| **JVM-dependent tests** | Tests that require a running JVM should be in projects that configure the JVM environment (e.g., `Java.Interop-Tests`). Verify that test classes requiring a JVM are not placed in unit-test-only projects. |
| **Expected codegen output tests** | Generator tests that compare expected output should be updated when the expected output format changes. Stale expected output files cause spurious test failures. |
