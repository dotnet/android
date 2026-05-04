# AI Code Generation Pitfalls

Patterns that AI-generated code consistently gets wrong. Always loaded during
reviews.

---

## Common AI Mistakes

| Pattern | What to watch for |
|---------|------------------|
| **Reinventing the wheel** | AI creates new infrastructure instead of using existing utilities. ALWAYS check if a similar utility exists before accepting new wrapper code. This is the most expensive AI pattern — hundreds of lines of plausible code that duplicates what's already there. |
| **Over-engineering** | HttpClient injection "for testability", speculative helper classes, unused overloads. If no caller needs it today, remove it. |
| **Swallowed errors** | AI catch blocks love to eat exceptions silently. Check EVERY catch block. Also check that exit codes are checked consistently. |
| **Null-forgiving operator (`!`)** | AI sprinkles `!` everywhere to silence nullable warnings. This is banned — use null checks, `IsNullOrEmpty()`, or make types non-nullable. See `csharp-rules.md` for the full NRT rules. |
| **Wrong formatting** | AI generates standard C# formatting (no space before parens). This repo requires Mono style: `Foo ()`, `array [0]`. |
| **`string.Empty` and `Array.Empty<T>()`** | AI defaults to these. Use `""` and `[]` instead. |
| **Sloppy structure** | Multiple types in one file, block-scoped namespaces, `#region` directives, classes where records would do. New helpers marked `public` when `internal` suffices. |
| **Docs describe intent not reality** | AI doc comments often describe what the code *should* do, not what it *actually* does. Review doc comments against the implementation. (Postmortem `#59`) |
| **Unused parameters** | AI adds `CancellationToken` parameters but never observes them, or accepts `additionalArgs` as a string and interpolates it into a command. Unused CancellationToken is a broken contract; string args are injection risks. |
| **Confidently wrong domain facts** | AI makes authoritative claims about platform-specific behavior that are wrong (e.g., claiming a deprecated env var is recommended). Always verify domain-specific claims against official docs. |
| **Over-mocking** | Not everything needs to be mocked. Integration tests with `Assert.Ignore` on failure are fine and catch real API changes that mocks never will. |
| **`Debug.WriteLine` for logging** | AI catch blocks often log with `System.Diagnostics.Debug.WriteLine()` or `Console.WriteLine()` — neither integrates with MSBuild or codebase logger patterns. Use the task's logging facilities instead. |
| **Modifying localization files** | AI modifies non-English `.resx` or `.lcl` files. Only the main English resource files should be edited. |
| **`git commit --amend`** | AI uses `--amend` on commits. Always create new commits — the maintainer will squash as needed. |
| **Commit messages omit non-obvious choices** | Behavioral decisions ("styleable arrays are cached, not copied per-access") and known limitations ("this leaks N bytes on Android 9") belong in the commit message. (Postmortem `#13`, `#69`) |
| **Typos in user-visible strings** | Users copy-paste error messages into bug reports. Get them right. (Postmortem `#61`) |
| **Filler words in docs** | "So" at the start of a sentence adds nothing. Be direct. (Postmortem `#71`) |
