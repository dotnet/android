# Code Review Postmortem

Review feedback from **@jonathanpeppers** (and Copilot reviewer) on Copilot-assisted PRs by @rmarinho, adding new infrastructure to `Xamarin.Android.Tools.AndroidSdk`.

| PR | Title | Status |
|----|-------|--------|
| [#274](https://github.com/dotnet/android-tools/pull/274) | Add JDK installation support (Microsoft OpenJDK) | Merged 2026-02-26 |
| [#275](https://github.com/dotnet/android-tools/pull/275) | Add SDK bootstrap and sdkmanager wrapper | Merged 2026-03-02 |
| [#281](https://github.com/dotnet/android-tools/pull/281) | Add tool runner base infrastructure | Open |
| [#282](https://github.com/dotnet/android-tools/pull/282) | Add AvdManagerRunner for avdmanager CLI operations | Open |
| [#283](https://github.com/dotnet/android-tools/pull/283) | Add AdbRunner for adb CLI operations | Open |
| [#284](https://github.com/dotnet/android-tools/pull/284) | Add EmulatorRunner for emulator CLI operations | Open |

---

## 1. Use the right source: Microsoft OpenJDK, not Adoptium

**PR #274** — The initial implementation downloaded JDKs from the Eclipse Adoptium (Temurin) API. Jonathan redirected it to Microsoft's own OpenJDK build at <https://www.microsoft.com/openjdk>.

> "Should use instead: https://www.microsoft.com/openjdk"

**Why it matters:** The library ships inside Microsoft tooling (Visual Studio, .NET Android SDK). Downloading from a third-party distribution introduces supply-chain risk and licensing ambiguity. Using Microsoft's own build keeps the provenance chain tight and ensures the JDK is tested against Microsoft's own CI.

---

## 2. Don't ship support for versions that aren't ready

**PR #274** — The original code supported both JDK 17 and JDK 21. Jonathan asked to remove 17 and ship only 21, while keeping the `SupportedVersions` array so more can be added later.

> "I wouldn't support 17 yet, but it would be ok to leave this as an `int[]` so more could be added later."

**Why it matters:** Supporting a version means testing, documenting, and maintaining it. Shipping JDK 17 support without the infrastructure to validate it invites bugs that only appear in production. Leaving the extensibility point (the array) costs nothing and keeps the door open.

---

## 3. Must target netstandard2.0 — this runs inside Visual Studio

**PR #274** — The code originally targeted only modern .NET. Jonathan flagged that it must support `netstandard2.0` because the library loads inside Visual Studio (which runs on .NET Framework).

> "This code needs to be able run on .NET framework — so it needs to support netstandard. It could be running inside Visual Studio."

**Why it matters:** A library that compiles fine on `net10.0` but uses APIs absent from `netstandard2.0` will crash at runtime inside VS. This is the kind of bug that passes CI (tests run on modern .NET) but fails in the field. Every new API call must be checked against the `netstandard2.0` surface area.

---

## 4. New C# language features may not be available

**PR #274** (commit `b2a8a7be`) — Jonathan fixed code that used newer C# language features not available when targeting `netstandard2.0` / older compilers.

**Why it matters:** When Copilot generates code, it tends to use the newest syntax it knows. If the project's `LangVersion` or target framework doesn't support those features, the build breaks. Reviewers must watch for this, especially in multi-target projects.

---

## 5. Thread `CancellationToken` through every async call

**PR #274** (commit `212ac436`) and **PR #275** (review comment) — Multiple places accepted a `CancellationToken` parameter but never passed it to the underlying HTTP call (`GetStringAsync`, `ReadAsStreamAsync`, etc.). Jonathan fixed this throughout.

> (PR #275): Copilot reviewer flagged `GetStringAsync` not passing `cancellationToken`. Jonathan's commit wired it through properly.

**Why it matters:** An un-cancellable async method is a lie — callers think they can cancel but the operation keeps running, holding sockets, memory, and threads. In an IDE (VS), a stuck HTTP call can freeze the UI. Always propagate the token to the lowest-level async operation.

---

## 6. `HttpClient` must be static — socket exhaustion

**PR #275** (commit `d30a5d8e`) — The `SdkManager` class created a per-instance `HttpClient`. Jonathan changed it to `static readonly` and removed disposal.

> Commit message: "Per Microsoft guidelines, HttpClient instances should be static and shared across the application lifetime."

**Why it matters:** Creating and disposing `HttpClient` instances leads to socket exhaustion under load because `TIME_WAIT` sockets accumulate. The [official guidance](https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines) is to reuse a single static instance. This is a well-known .NET pitfall that AI-generated code consistently gets wrong.

---

## 7. Remove `HttpClient` injection until there's a real need

**PR #275** (threads 11, 26) — The AI-generated code accepted an optional `HttpClient` parameter for "testability" and "enterprise proxy scenarios." Jonathan pushed back twice, noting the justification was speculative.

> "Why do we need to pass in an `HttpClient`? Could `SdkManager` just use its own and it would simplify some things?"
>
> "I would still remove the option to pass in a `HttpClient` until you have a reason you actually need it. The AI gave some made-up/theoretical answer on why not to remove it."

**Why it matters:** YAGNI (You Aren't Gonna Need It). Every public API parameter is a maintenance commitment. AI assistants tend to over-engineer for hypothetical scenarios. If no caller needs to inject an `HttpClient` today, don't add the parameter — it complicates the constructor, the dispose logic, and the ownership semantics.

---

## 8. Use `XmlReader` instead of `XElement` / LINQ to XML

**PR #275** (thread 12) — Manifest parsing used `XDocument.Parse()` + LINQ queries. Jonathan requested `XmlReader` for lower overhead.

> "If copilot wrote this, can we make it use `XmlReader` instead? `XElement` is based on System.Linq, so it's slower."

**Why it matters:** `XElement` builds a full DOM tree in memory. For a manifest that only needs forward-only reading, `XmlReader` is streaming and allocation-free. In a library that may run in constrained IDE processes, avoiding unnecessary allocations matters.

---

## 9. One type per file, and update Copilot instructions

**PR #275** (threads 9, 25) — Multiple types were crammed into `SdkManager.cs`. Jonathan asked for each to live in its own file and for the convention to be added to `copilot-instructions.md` so AI assistants follow it going forward.

> "Can we put each type in its own file, `SdkManifestComponent.cs`, and update `copilot-instructions.md` so AIs will always do this?"

**Why it matters:** One-type-per-file is a core C# convention in this repo. It makes types discoverable via filename, simplifies git blame, and reduces merge conflicts. Encoding the convention in Copilot instructions prevents the same issue from recurring in every AI-assisted PR.

---

## 10. Name types to avoid ambiguity — `SdkManifestComponent`, not `ManifestComponent`

**PR #275** (thread 9) — The type was originally named `ManifestComponent`, which collides conceptually with `AndroidManifest.xml`.

> "You may also want to pick a better name other than `Manifest`? There is `AndroidManifest.xml`, so maybe this needs to be `SdkManifestComponent`?"

**Why it matters:** In a codebase that deals with both Android app manifests and SDK repository manifests, an ambiguous name causes confusion. Prefixing with `Sdk` makes the domain clear at a glance.

---

## 11. No empty catch blocks — always log the exception

**PR #275** (threads 17, 18) — Multiple `catch` blocks swallowed exceptions silently.

> "Every `catch` should get the `Exception` and log it, so no empty catch blocks."

**Why it matters:** Empty catch blocks hide bugs. When something goes wrong in production, there's no diagnostic trail. Even if the exception is expected (e.g., declining a license), logging it provides a breadcrumb for debugging. This was a recurring pattern the AI generated.

---

## 12. Remove `#region` directives

**PR #275** (thread 19) — Test files had `#region` blocks.

> "Can we remove all `#region` and update `copilot-instructions.md` so it won't make them in the future?"

**Why it matters:** `#region` hides code and makes reviews harder — collapsed regions are easy to skip. Modern IDEs make them unnecessary. Banning them in Copilot instructions prevents AI from reintroducing them.

---

## 13. Use `ArrayPool<byte>` for download buffers

**PR #274** (commit `6e167e00`) and **PR #275** (thread 21) — Download code allocated `new byte[81920]` on each call. Jonathan replaced this with `ArrayPool<byte>.Shared.Rent()` with `try/finally` return.

> "Can this use `ArrayPool<byte>.Rent()` and return it in a `try-finally` block?"

**Why it matters:** An 80 KB allocation goes straight to the Large Object Heap (LOH), which is expensive to collect. `ArrayPool` rents from a shared pool, avoiding GC pressure. In a library that may download multiple files, this adds up.

---

## 14. Extract magic numbers into named constants

**PR #274** (commit `6e167e00`) — The download buffer size (`81920`), bytes-per-MB divisor (`1048576`), and whitespace char array were inline literals. Jonathan extracted them to `const` fields (`BufferSize`, `BytesPerMB`, `WhitespaceChars`).

**Why it matters:** Magic numbers obscure intent. A reviewer seeing `81920` has to mentally compute "oh, that's 80 KB." A constant named `BufferSize` communicates instantly. It also creates a single point of change.

---

## 15. Centralize process creation in `ProcessUtils`

**PR #274** (commits `d0f3bea2`, `9ed5ddc8`) and **PR #275** (thread 29) — The code manually constructed `ProcessStartInfo` in multiple places. Jonathan created `ProcessUtils.CreateProcessStartInfo()` that uses `ArgumentList` on modern .NET and falls back to a safe `Arguments` string on `netstandard2.0`.

> "Can this use: ProcessUtils.cs#L22"

**Why it matters:** Duplicated process-launch code means duplicated bugs — inconsistent argument escaping, missing `UseShellExecute = false`, forgotten `RedirectStandardOutput`. A centralized helper gets it right once. The `ArgumentList` vs `Arguments` split is particularly tricky to get right across target frameworks.

---

## 16. Use `NullProgress` pattern instead of `progress?.Report()`

**PR #274** (commit `48ede26d`) and **PR #275** (threads 98–99) — Code was littered with `progress?.Report(...)` null checks. Jonathan replaced these with a `NullProgress` sentinel assigned via `progress ??= NullProgress.Instance`.

> "Can we set `progress ??=` with a 'null/no-op progress' so you don't have to put `progress?.` checks everywhere?"

**Why it matters:** Null-propagation operators scattered throughout a method add visual noise and make it easy to miss a spot. The null-object pattern eliminates the entire class of "forgot to null-check" bugs and makes the code cleaner.

---

## 17. `IsElevated()` is unnecessary — let the caller handle elevation

**PR #274** (commit `e8e9db78`) and **PR #275** (thread 88) — The code had an `IsElevated()` helper with P/Invoke to check admin rights, with logic to re-launch elevated. Jonathan removed it.

> "Should we even have this? It seems like we should check for elevation (only where required!) and error if we don't have it. Seems like VS Code or developers should run this tool already elevated?"

**Why it matters:** Auto-elevation is a security anti-pattern — it silently escalates privileges. The calling tool (VS, VS Code, CLI) should prompt the user for elevation. The library should simply fail with a clear error if it lacks permissions.

---

## 18. `ANDROID_SDK_ROOT` is deprecated — use `ANDROID_HOME`

**PR #275** (thread 31) — The code set both `ANDROID_HOME` and `ANDROID_SDK_ROOT` environment variables.

> "`ANDROID_SDK_ROOT` is deprecated: https://developer.android.com/tools/variables#envar — I don't think we should add new code using it."

**Why it matters:** Setting a deprecated variable trains downstream tools and developers to depend on it, prolonging the migration. Following the official Android documentation (`ANDROID_HOME` only) keeps the codebase aligned with the platform's direction.

---

## 19. Create `EnvironmentVariableNames` constants — no raw strings

**PR #275** (threads 30, 42) — Environment variable names like `"ANDROID_HOME"` were scattered as string literals.

> "Should we create a `EnvironmentVariableNames.cs` that has important env var names like these?"

**Why it matters:** String typos in environment variable names produce silent, hard-to-debug failures (the wrong variable is read/set, but no error is thrown). A constants class catches typos at compile time and makes it easy to find all usages via "Find References."

---

## 20. Validate enum/parameter values — don't silently accept garbage

**PR #275** (thread 22) — The `checksumType` was a string parameter that only handled `"sha1"` but accepted anything without error.

> "Why is there a `checksumType` parameter if it only does `SHA1`? Should it be checking the value and throwing an exception for unexpected hash types?"

This also led to making `checksumType` an enum instead of a string (thread 85):

> "Can `checksumType` be an enum, so no typos can occur? Also would avoid string comparisons."

**Why it matters:** A function that silently ignores an unsupported value is a correctness bug waiting to happen. If a new checksum type is added to the manifest but the code doesn't support it, downloads would pass "verification" without actually being checked. Throw early, throw loud.

---

## 21. Use p/invoke for `chmod` instead of spawning a process

**PR #275** (thread 23) — The code ran `chmod +x` by spawning a child process. Jonathan suggested using p/invoke directly.

> "Is there a p/invoke way to do this instead? Then it wouldn't run a new process."

**Why it matters:** Spawning a process for a single syscall is expensive — it forks, execs, waits, and produces overhead for logging and error handling. A direct `[DllImport("libc")] chmod()` call is instantaneous and more reliable. It also makes error handling straightforward (check the return value) instead of parsing process exit codes.

---

## 22. If `chmod` fails, throw — don't silently continue

**PR #275** (thread 32) — After adding p/invoke for `chmod`, the failure path caught the exception and continued.

> "If this fails, should we just let the exception happen? The problem is you won't be able to use `sdkmanager` if it fails, so seems like it should error?"

**Why it matters:** If `chmod` fails, the `sdkmanager` binary won't be executable, and every subsequent operation will fail with a confusing "file not found" or "permission denied" error. Failing fast at the point of the real problem (chmod) gives the user an actionable error message.

---

## 23. Use version-based directory names, not `latest`

**PR #275** (thread 15) — The bootstrap extracted cmdline-tools to a `latest/` directory.

> "On macOS, `latest` is a symlink to the versioned one, like `latest -> 19.0` or whatever. Could we just use the version number of the package for the directory name and not do anything with `latest` at all?"

**Why it matters:** `latest` is ambiguous — it doesn't tell you which version is installed, and upgrading is a destructive overwrite with no rollback. A versioned directory (`cmdline-tools/19.0/`) is self-documenting, allows side-by-side versions, and matches how `sdkmanager` itself organizes packages.

---

## 24. License presentation API — don't just auto-accept

**PR #275** (thread 20) — The original `AcceptLicensesAsync` blindly sent `"y"` to stdin for every license prompt. Jonathan asked for an API to present licenses to the user first.

> "If we have code here to _accept_ licenses, is there supposed to be an API that can be used to present them to the user? It seems like IDEs will show some UI for these, and CLI tools should prompt at the terminal."

**Why it matters:** Auto-accepting licenses without user consent may violate legal requirements. IDEs need to display the license text and get explicit user approval. The resulting `GetPendingLicensesAsync()` + `AcceptLicensesAsync(IEnumerable<string>)` API separates presentation from acceptance.

---

## 25. Prefer `record` types for immutable data models

**PR #275** (threads 78–80) — `SdkBootstrapProgress`, `SdkLicense`, and `SdkManifestComponent` were plain classes. Jonathan asked them to be `record` types.

> "Can this be a `record`?"

**Why it matters:** Records provide value equality, `ToString()`, and deconstruction for free. For small data-carrier types, they eliminate boilerplate and make intent clear: this is an immutable data bag, not a stateful object.

---

## 26. Use file-scoped namespaces in all new files

**PR #275** (threads 95–96) — New files used traditional block-scoped namespaces.

> "Should we use file-scoped namespaces in all new files?"

**Why it matters:** File-scoped namespaces (`namespace Foo;`) reduce one level of indentation across the entire file, improving readability. Jonathan also asked this to be added to `copilot-instructions.md` to prevent regression.

---

## 27. Don't duplicate code — unify platform mappings

**PR #275** (threads 81, 92) — Both `JdkInstaller` and `SdkManager` had independent OS/architecture-to-string mapping logic.

> "This mapping seems specific to the `sdkmanager`, we already have this here: JdkInstaller.cs#L319-L334. We should either: Unify with `JdkInstaller` or put this code in SDKManager, if it is specific to Android SDK."

**Why it matters:** Duplicated platform-detection logic means two places to update when a new architecture (e.g., RISC-V) is added, and two places that can diverge silently. Either share the code or make each copy clearly scoped to its specific use case.

---

## 28. Remove unused / speculative code

**PR #275** (threads 82–84) — An `AndroidEnvironmentHelper` class contained methods that weren't called, duplicated existing functionality, or belonged in a different PR.

> "Is this method even called? We have other code that does this, we should find it and unify. I'd remove this for now, until it's needed."
>
> "We should completely remove this and use `AndroidVersions` classes directly."

**Why it matters:** Dead code is worse than no code — it still needs to be read, understood, and maintained. It also misleads future contributors into thinking it's the right way to do things. Ship only what's needed for the current PR.

---

## 29. Don't tell AI to run `dotnet format` globally

**PR #275** (thread 77) — The Copilot instructions initially told the AI to run `dotnet format`.

> "I wouldn't tell it to `dotnet-format`, or it's going to make 100s of changes across the repo!"

**Why it matters:** An AI following "run dotnet format" will reformat every file in the repository, creating massive, unrelated diffs that obscure the actual changes and cause merge conflicts. Format only the files you're changing.

---

## 30. Guide AI with `copilot-instructions.md` for netstandard2.0 awareness

**PR #275** (thread 94) — Rather than listing specific API incompatibilities, Jonathan suggested a general instruction.

> "Maybe this should just say: 'many modern .NET APIs are unavailable or have fewer overloads on `netstandard2.0`. When unsure about API availability, search mslearn to check documentation for the target framework.'"

**Why it matters:** You can't enumerate every API difference between `net10.0` and `netstandard2.0`. Teaching the AI *how to check* (search MS Learn) is more durable than giving it a static list that will inevitably go stale.

---

## 31. Simplify code — merge lists, short-circuit with LINQ

**PR #275** (thread 93) — Package-list parsing created two separate lists and used `AddRange()`.

> "This creates two lists and AddRange() one to the other. It's already using System.Linq (slowish), but could it just create 1 list? That would probably make it 'good enough'."

**Why it matters:** Unnecessary intermediate collections waste memory and make the code harder to follow. Even when absolute performance isn't critical, simpler code is easier to review and less likely to hide bugs.

---

## 32. Write thorough tests — especially for parsing and utilities

**PR #274** (commit `6e4d1174`: "MOAR Tests!", commit `7c85bd7c`) — Jonathan personally wrote 437 lines of tests across `DownloadUtilsTests.cs`, `FileUtilTests.cs`, `JdkVersionInfoTests.cs`, and `ProcessUtilsTests.cs`.

**Why it matters:** The AI-generated code came with some tests, but Jonathan significantly expanded coverage — particularly for parsing logic (`ParseChecksumFile`), file utilities (`MoveWithRollback`, `IsUnderDirectory`), and process argument construction. These are exactly the kind of functions where edge cases hide.

---

## 33. Stdin write-then-delay ordering matters

**PR #275** (threads 90–91) — The license acceptance code had a 500ms initial delay before sending input to `sdkmanager --licenses`, then a 200ms delay between subsequent writes.

> "Why delay 500ms? Can we remove this line and it sends `n` every 200ms?"
>
> "Seems like we could just put the delay first and remove the 500ms above."

**Why it matters:** Process stdin interaction is timing-sensitive. Writing before the process is ready for input loses the write; delaying too long slows down the operation. The correct pattern is delay-then-write (wait for the process to be ready, then send), not write-then-delay.

---

## 34. Zip Slip protection — validate archive entry paths

**PR #274** (Copilot reviewer + Jonathan's guidance) — The original code used `ZipFile.ExtractToDirectory()` which doesn't validate entry paths. A malicious archive could contain entries like `../../etc/passwd` that escape the target directory. This was replaced with entry-by-entry extraction that validates each path resolves under the destination.

**Why it matters:** Zip Slip is a well-known archive extraction vulnerability ([CVE-2018-1002200](https://snyk.io/research/zip-slip-vulnerability)). Libraries that extract archives from the internet (JDK downloads, SDK packages) are high-value targets. Always validate that `Path.GetFullPath(entryPath)` starts with the destination directory.

---

## 35. Checksum verification must be mandatory, not optional

**PR #274** (Copilot reviewer, threads 26, 37) — The original code proceeded with installation even if the checksum fetch failed, silently skipping verification.

> Copilot: "InstallAsync proceeds without checksum verification if the checksum fetch fails. This weakens the supply-chain guarantees."

This was fixed to throw `InvalidOperationException` when checksum fetch fails, making verification mandatory for both archive extraction and elevated platform-installer paths.

**Why it matters:** Optional checksum verification is the same as no checksum verification — an attacker who can interfere with the checksum URL gets a free pass. For supply-chain security, the download must fail if integrity cannot be confirmed.

---

## 36. `Directory.Move` fails across volumes — extract near the target

**PR #274** (Copilot reviewer, thread 25) — The code extracted archives to `Path.GetTempPath()` then used `Directory.Move()` to the target. On systems where `/tmp` is a different filesystem (common on Linux with tmpfs), this fails.

> Copilot: "Directory.Move fails across volumes/filesystems, so installs will reliably fail when targetPath is on a different drive/mount than the temp directory."

The fix was to create the temp extraction directory under the same parent as the target path.

**Why it matters:** This is a classic cross-platform pitfall. `Directory.Move` is really a rename syscall, which only works within the same filesystem. Extracting near the target ensures same-filesystem semantics. PR #275 additionally added a recursive-copy fallback for robustness.

---

## 37. Don't swallow `OperationCanceledException` in catch-all blocks

**PR #274** (Copilot reviewer, threads 34–35) — `DiscoverAsync` and `FetchChecksumAsync` caught all exceptions, including `OperationCanceledException`, converting cancellation into empty results or null checksums instead of properly propagating it.

> Copilot: "`DiscoverAsync` catches all exceptions inside the per-version loop, which will also swallow `OperationCanceledException`... That breaks expected cancellation semantics."

The fix was to explicitly catch and rethrow `OperationCanceledException` before the general `catch (Exception)` block.

**Why it matters:** Callers who pass a `CancellationToken` expect cancellation to propagate as `OperationCanceledException` or `TaskCanceledException`. Swallowing it means the caller gets wrong results (empty list, null checksum) instead of a proper cancellation signal. This breaks `async`/`await` contracts.

---

## 38. Rollback on extraction failure — don't delete the backup too early

**PR #274** (Copilot reviewer, threads 15, 21, 36) — The original code deleted the backup of the previous JDK immediately after moving the new one into place. If validation or a later step failed, the user was left with no working JDK.

> Copilot: "If InstallAsync later fails validation (or any post-extraction step throws), the previous JDK at targetPath has already been permanently deleted, leaving the user with no working JDK."

The fix was to keep the backup until after validation succeeds, with a `CommitMove` step that only deletes the backup on confirmed success.

**Why it matters:** Safe replacement of installed software requires a two-phase commit: (1) move new into place, (2) validate, (3) delete backup only on success. Deleting the backup before validation turns a recoverable failure into data loss.

---

## 39. Command injection in elevated `.cmd` scripts

**PR #275** (Copilot reviewer, thread 67) — The elevated execution path on Windows wrote a `.cmd` script that interpolated package arguments directly into a command line run under `cmd.exe`. Characters like `&`, `|`, `>` in package names could escape the command.

> Copilot: "The elevated path writes a .cmd script that interpolates `arguments` directly into a command line. Because this runs under `cmd.exe`, special characters... can lead to command injection, which is especially risky when running elevated."

The fix added `SanitizeCmdArgument()` plus argument validation that rejects dangerous characters.

**Why it matters:** Any code that generates shell commands from variable inputs is a command injection risk. This is doubly dangerous when the command runs elevated (admin/root). Always sanitize or use `ArgumentList` (which bypasses the shell entirely).

---

## 40. Copilot reviewer was wrong about `ANDROID_HOME` vs `ANDROID_SDK_ROOT`

**PR #275** (thread 42) — The Copilot reviewer incorrectly stated that `ANDROID_SDK_ROOT` is the recommended variable and `ANDROID_HOME` is deprecated — the exact opposite of the truth per [Android documentation](https://developer.android.com/tools/variables#envar).

> Copilot: "These docs describe `ANDROID_HOME` as the preferred variable and `ANDROID_SDK_ROOT` as the older one, but Android tooling guidance is the opposite."
>
> @rmarinho: "Copilot is wrong here right?"
>
> @jonathanpeppers: Added guidance to `copilot-instructions.md` to prevent this confusion.

**Why it matters:** AI reviewers can be confidently wrong about domain-specific facts. In this case, the Copilot reviewer would have introduced a regression by swapping to a deprecated variable. Human reviewers must verify AI claims against authoritative sources, especially for environment/configuration decisions.

---

## 41. Network tests are acceptable — don't over-mock

**PR #275** (thread 44) — The Copilot reviewer suggested replacing network-calling tests with mocked/faked alternatives for CI reliability.

> Copilot: "This test performs a real network call to the manifest feed... Prefer a unit test that injects/fakes the HTTP response."
>
> @jonathanpeppers: "Network calls are fine in these tests."

**Why it matters:** Not every Copilot suggestion should be accepted. Integration tests that hit real endpoints catch real problems (API changes, format changes, certificate issues) that mocks never will. The tests already use `Assert.Ignore` on network failure for CI resilience. Over-mocking creates tests that pass but don't actually verify the system works.

---

## 42. Don't reinvent existing infrastructure — AI ignores what's already there

**PRs #281–284** — The AI created a brand-new `AndroidToolRunner` class that wrapped `System.Diagnostics.Process` with timeout, cancellation, and output capture — duplicating everything `ProcessUtils` already provided. Jonathan left the same comment on PRs #282, #283, and #284:

> "This is inventing lots of new code that just wraps System.Diagnostics.Process. Can we just use the existing code for this instead? ProcessUtils.cs. This is adding lots of more lines of code to maintain, and it's like the AI didn't even look at what is already here."

**Why it matters:** This is the single most expensive AI code review pattern: the AI generates hundreds of lines of plausible-looking code that duplicates existing functionality. It compiles, it passes tests, but it creates a parallel maintenance burden and diverges from established patterns. Reviewers must check whether existing utilities already solve the problem before approving new infrastructure.

---

## 43. Port code from downstream consumers — don't rewrite

**PRs #283, #284** — Jonathan asked to port the device-listing logic from `dotnet/android`'s `GetAvailableAndroidDevices` MSBuild task and the emulator boot logic from `BootAndroidEmulator`, rather than writing new implementations.

> (PR #283): "Open a draft dotnet/android PR that updates its submodule to the branch of #283... Review/merge dotnet/android-tools. Bring dotnet/android out of draft, switch to dotnet/android-tools/main. Merge it second."

**Why it matters:** The downstream consumer (`dotnet/android`) already has battle-tested parsing and boot logic with real-world edge cases handled. Rewriting it means losing those edge cases and creating two implementations that can diverge. Porting preserves the institutional knowledge embedded in the existing code.

---

## 44. Prove code sharing with a draft downstream PR before merging

**PRs #283, #284** — Jonathan required a specific merge workflow: (1) create the shared library code in `android-tools`, (2) open a draft PR in `dotnet/android` that uses the new APIs via the submodule, (3) merge `android-tools` first, (4) update the submodule pointer and merge `dotnet/android` second.

**Why it matters:** A shared library that isn't actually consumed by its intended consumer is speculative code. The draft downstream PR proves the API surface actually works, catches design mismatches early, and demonstrates that the shared code reduces (not increases) total code. It also makes the review concrete — the reviewer can see both sides of the change.

---

## 45. Check exit codes consistently across all operations

**PR #283** (Copilot reviewer, thread 27) — `StopEmulatorAsync` didn't check the adb exit code, while `ListDevicesAsync` and `WaitForDeviceAsync` did. This inconsistency meant stop failures were silent.

> Copilot: "`StopEmulatorAsync` doesn't check the adb exit code, so it can succeed silently even if `adb -s <serial> emu kill` fails."

Fixed by adding `ThrowIfFailed` consistently.

**Why it matters:** Inconsistent error handling is worse than no error handling — it creates a false sense of safety. If some operations check exit codes and others don't, developers assume all operations are checked. Apply the same error-handling pattern (like `ThrowIfFailed`) to every process invocation.

---

## 46. New helper methods should default to `internal`

**PR #283** (Copilot reviewer, thread 29) — `ProcessUtils.ThrowIfFailed` was added as `public` even though it was only used internally.

> Copilot: "`ThrowIfFailed` is introduced as a new `public` API on `ProcessUtils`, but it's only used internally in this PR. Given the repo convention to keep the public API minimal, consider making this `internal`."

**Why it matters:** Every `public` method is a compatibility contract. Once external consumers depend on it, you can't change the signature without a breaking change. Default to `internal` and promote to `public` only when a confirmed external consumer needs it. This repo uses `InternalsVisibleTo` for test access.

---

## 47. Return `IReadOnlyList<T>`, not `List<T>`, from public APIs

**PR #284** (Copilot reviewer) — Public methods returned `Task<List<string>>`, exposing a mutable concrete collection.

> Copilot: "Public API returns `Task<List<string>>`, which exposes a mutable concrete collection. Consider returning `Task<IReadOnlyList<string>>` to avoid leaking mutability."

**Why it matters:** Returning `List<T>` lets callers mutate the collection, potentially corrupting internal state if the list is cached. `IReadOnlyList<T>` communicates intent ("you can read this, not change it") and allows the implementation to switch to arrays, immutable lists, or other backing stores without breaking callers.

---

## 48. Don't redirect stdout/stderr on background processes without draining

**PR #284** (Copilot reviewer) — `StartBackground` set `RedirectStandardOutput = true` and `RedirectStandardError = true` but never started async readers to drain the output.

> Copilot: "`StartBackground` sets `RedirectStandardOutput/RedirectStandardError = true` but does not start any readers. This can cause the emulator process to block once stdout/stderr buffers fill up."

**Why it matters:** Redirected output goes to an OS pipe buffer (typically 4-64 KB). When the buffer fills, the child process blocks on its next write. For long-running processes like the Android emulator, this means the process silently hangs. Either don't redirect (set to `false`) or immediately start async readers.

---

## 49. `CancellationToken` accepted but never observed is a broken contract

**PRs #281, #284** (Copilot reviewer) — `AndroidToolRunner.Run()` accepted a `CancellationToken` parameter but never checked it or passed it to any downstream operation.

> Copilot: "`Run` method accepts a `cancellationToken` parameter but never uses it. The token should be checked before starting the process and should trigger process termination when cancelled."

**Why it matters:** An unused `CancellationToken` parameter is an API lie. Callers write `await runner.Run(..., cts.Token)` expecting cancellation to work, then wonder why the process keeps running after they cancel. Either honor the token (register a callback to kill the process) or don't accept it.

---

## 50. Take structured argument lists, not interpolated strings

**PR #284** (Copilot reviewer) — `StartAvd` built a single argument string by interpolating `avdName` and appending `additionalArgs` verbatim, risking argument injection.

> Copilot: "If `avdName` contains quotes/whitespace (or `additionalArgs` comes from untrusted input), this can break parsing or allow argument injection. Consider... taking additional args as a structured list (`IEnumerable<string>`)."

**Why it matters:** String interpolation into process arguments is the process-launch equivalent of SQL injection. Use `ProcessUtils.CreateProcessStartInfo()` which populates `ArgumentList` (safe, no shell parsing) on net5+ and falls back to proper escaping on netstandard2.0. Accept `IEnumerable<string>` instead of `string` for additional arguments.

---

## 51. Doc comments must match actual behavior

**PR #283** (Copilot reviewer, thread 28) — A file-level doc comment said "bootstraps cmdline-tools" but the code only bootstrapped when `ANDROID_HOME` was missing — it never checked for cmdline-tools specifically.

> Copilot: "The file-level doc comment says 'When the pre-installed SDK lacks cmdline-tools, the tests bootstrap them', but `OneTimeSetUp` currently only bootstraps when `ANDROID_HOME` is missing/invalid."

**Why it matters:** Inaccurate doc comments are worse than no doc comments — they actively mislead. When AI generates both code and documentation, the docs often describe the *intended* behavior rather than the *actual* behavior. Review doc comments against the code, not against the PR description.

---

## 52. Never use the null-forgiving operator (`!`)

**PR #283** (review 3892527868, `AdbRunner.cs:195` and `AndroidEnvironmentHelper.cs:20`) — Code had parameters typed as nullable but then used `!` to suppress warnings at call sites. Jonathan flagged both spots.

> "We should avoid using `!`, do you even need it if you use our `IsNullOrEmpty()` extension method?"

> "Why are the paths allowed to be null? Then we have `!` below? Can we make sure `copilot-instructions.md` says to never use `!` and to actually fix nullable warnings correctly?"

**Why it matters:** The null-forgiving operator `!` silences the compiler without fixing the bug. If a value can actually be null at runtime, `!` turns a compile-time warning into a `NullReferenceException`. The correct fix is to either make the parameter non-nullable (if null is never valid) or add a proper null check. AI frequently sprinkles `!` to make warnings disappear.

---

## 53. Add overloads to reduce caller ceremony

**PR #283** (review 3892527868, `AdbRunner.cs:207`) — Multiple callers were calling `writer.ToString()` before passing to `ThrowIfFailed()`. Jonathan suggested adding a `StringWriter` overload instead.

> "Should `ThrowIfFailed()` just have an overload for `StringWriter` and you wouldn't have to call `ToString()` from callers?"

**Why it matters:** When every caller has to perform the same conversion before calling a method, the conversion belongs inside the method (or an overload). This reduces boilerplate, eliminates a class of copy-paste errors, and makes the API easier to use correctly.

---

## 54. Delete unused overloads

**PR #283** (review 3892527868, `AdbRunner.cs:233`) — An overload existed but wasn't needed.

> "Can we delete this overload?"

**Why it matters:** Dead code is a maintenance burden and a trap for readers who assume it exists for a reason. AI tends to generate speculative overloads "for completeness" — review each public method and ask whether any caller actually needs it.

---

## 55. Prefer C# pattern matching

**PR #283** (review 3892527868, `AdbRunner.cs:312`) — Code used traditional `if`/`else` chains to check types or values. Jonathan asked for pattern matching and to update `copilot-instructions.md`.

> "Can this use C# pattern matching, and update `copilot-instructions.md` to do this for all new code?"

**Why it matters:** Pattern matching (`is`, `switch` expressions, property patterns) is more concise, avoids unnecessary casts, and makes exhaustiveness checks possible. Encoding this in `copilot-instructions.md` prevents AI from generating old-style conditional chains.

---

## 56. Section-separator comments are `#region` in disguise

**PR #283** (review 3892527868, `RunnerIntegrationTests.cs:151`) — Test files had block comments acting as visual section dividers between groups of tests.

> "It doesn't seem like we should be writing comments like this, they are basically `#region` and `#endregion`"

**Why it matters:** This extends the `#region` ban (Finding #12). Any mechanism to partition code into visual "sections" — whether `#region`, banner comments, or ASCII art dividers — signals the file should be split or the test organization rethought. AI loves generating these as pseudo-structure.

---

## Summary of Themes

| Theme | Occurrences | Key Lesson |
|-------|------------|------------|
| **AI reinvents the wheel** | AndroidToolRunner vs ProcessUtils, rewriting downstream logic | Check existing code FIRST — AI doesn't look at what's already there |
| **AI over-engineers** | HttpClient injection, IsElevated, speculative code, unused overloads | Remove code until you need it (YAGNI) |
| **AI ignores target framework** | netstandard2.0, new lang features | Always check API availability against the lowest TFM |
| **AI swallows errors** | Empty catch blocks, chmod failure, checksumType, OperationCanceledException, unchecked exit codes | Fail fast with clear errors; apply error handling consistently |
| **AI generates sloppy structure** | One type per file, #region, section-separator comments, empty lines, naming, `public` when `internal` suffices | Encode conventions in copilot-instructions.md |
| **AI fights the type system** | Null-forgiving `!`, nullable params that shouldn't be, `!` to silence warnings | Fix nullability at the source; never use `!` |
| **API design** | `List<T>` vs `IReadOnlyList<T>`, unused CancellationToken, string args vs structured args, missing overloads | Public APIs are contracts — get them right the first time |
| **Modern C# idioms** | Pattern matching, records, file-scoped namespaces | Use modern syntax when available on the target framework |
| **Performance awareness** | ArrayPool, XmlReader, p/invoke, list merging, cached arrays | Small allocations add up in library code |
| **Security & correctness** | Zip Slip, command injection, path traversal, mandatory checksums, license consent | Libraries must be correct by default |
| **AI reviewer can be wrong** | ANDROID_HOME vs ANDROID_SDK_ROOT, over-mocking | Always verify AI claims against authoritative docs |
| **AI docs diverge from code** | Doc comments describing intended not actual behavior | Review docs against the code, not the PR description |
| **Rollback & resilience** | Cross-volume moves, backup-before-replace, cross-device fallback, stdout buffer deadlocks | Assume failure at every step; design for recovery |
| **Code sharing workflow** | Port from downstream, draft PR before merge, submodule coordination | Prove shared code works with its consumer before merging |
| **Reviewer wrote code too** | 11 fix-up commits on PR #274, 437 lines of new tests | Good review includes hands-on fixes, not just comments |
