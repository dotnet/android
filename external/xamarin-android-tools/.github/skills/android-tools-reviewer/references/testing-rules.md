# Testing Review Rules

Guidance for test code reviews. Loaded when test files change.

---

| Check | What to look for |
|-------|-----------------|
| **Bug fixes need regression tests** | Every PR that fixes a bug should include a test that fails without the fix and passes with it. If the PR description says "fixes #N" but adds no test, ask for one. |
| **Test assertions must be specific** | `Assert.IsNotNull(result)` or `Assert.IsTrue(success)` don't tell you what went wrong. Prefer `Assert.AreEqual(expected, actual)` or NUnit constraints (`Assert.That` with `Does.Contain`, `Is.EqualTo`, etc.) for richer failure messages. |
| **Deterministic test data** | Tests should not depend on system locale, timezone, or current date. Use explicit `CultureInfo.InvariantCulture` and hardcoded dates when testing formatting. |
| **Test edge cases** | Empty collections, null inputs, boundary values, concurrent calls, and very large inputs should all be considered. If the PR only tests the happy path, suggest edge cases. |
