# AI Code Generation Pitfalls

Patterns that AI-generated code consistently gets wrong. Always loaded during reviews.

---

| Pattern | What to watch for |
|---------|------------------|
| **Reinventing the wheel** | AI creates new infrastructure instead of using existing utilities. ALWAYS check if a similar utility exists before accepting new wrapper code. This is the most expensive AI pattern — hundreds of lines of plausible code that duplicates what's already there. |
| **Over-engineering** | HttpClient injection "for testability", elevation auto-detection, speculative helper classes, unused overloads. If no caller needs it today, remove it. |
| **Swallowed errors** | AI catch blocks love to eat exceptions silently. Check EVERY catch block. Also check that exit codes are checked consistently. |
| **Ignoring target framework** | AI generates code for the newest .NET. Check every API call against the oldest supported target framework. |
| **Sloppy structure** | Multiple types in one file, block-scoped namespaces, #region directives, classes where records would do. New helpers marked `public` when `internal` suffices. |
| **Confidently wrong domain facts** | AI once claimed `ANDROID_SDK_ROOT` was the recommended variable (it's deprecated). Always verify domain-specific claims against official docs. |
| **Over-mocking** | Not everything needs to be mocked. Integration tests with `Assert.Ignore` on failure are fine and catch real API changes that mocks never will. |
| **Docs describe intent not reality** | AI doc comments often describe what the code *should* do, not what it *actually* does. Review doc comments against the implementation. |
| **Unused parameters** | AI adds `CancellationToken` parameters but never observes them, or accepts `additionalArgs` as a string and interpolates it into a command. Unused CancellationToken is a broken contract; string args are injection risks. |
| **Null-forgiving operator (`!`)** | The postfix `!` null-forgiving operator (e.g., `foo!.Bar`) is banned in most codebases. If the value can be null, add a proper null check. If it can't be null, make the parameter/variable non-nullable. AI frequently sprinkles `!` to silence the compiler — this turns compile-time warnings into runtime `NullReferenceException`s. Use `IsNullOrEmpty()` extension methods or null-coalescing instead. Note: this rule is about the postfix `!` operator, not the logical negation `!` (e.g., `if (!someBool)` or `if (!string.IsNullOrEmpty (s))`). |
| **`Debug.WriteLine` for logging** | AI catch blocks often log with `System.Diagnostics.Debug.WriteLine()` or `Console.WriteLine()` — neither integrates with most codebase logger patterns. |
| **`git commit --amend`** | AI uses `--amend` on commits that are already pushed or belong to another author. Always create new commits — the maintainer will squash as needed. |
| **Commit messages omit non-obvious choices** | Behavioral decisions and known limitations belong in the commit message, not just the code. |
| **Typos in user-visible strings** | Users copy-paste error messages into bug reports. Get them right. |
| **Filler words in docs** | "So" at the start of a sentence adds nothing. Be direct. |
