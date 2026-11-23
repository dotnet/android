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
| **Android tools SDK/JDK fixtures** | Tests under `tests/Xamarin.Android.Tools.AndroidSdk-Tests/` commonly build isolated fake SDK/JDK layouts and platform-specific tool scripts (`.bat` on Windows, shell scripts on Unix). Keep these fixtures self-contained and cleaned up in setup/teardown rather than depending on the developer machine's installed SDK/JDK. |
| **Generator tests must include Invoker types** | Tests for generated binding code (under `src/generator/` and related test projects) should verify both the interface/class output and the `*Invoker` type behavior. Invoker codegen has historically had subtle bugs with default interface methods and virtual dispatch. |
| **JVM-dependent tests** | Tests that require a running JVM must be in projects that configure the JVM environment (e.g., `Java.Interop-Tests`). Verify that test classes requiring a JVM are not placed in unit-test-only projects, where they will silently skip or fail with obscure errors. |
| **Expected codegen output tests** | Generator tests that compare against expected output files should be updated when the expected format changes. Stale expected-output files cause spurious test failures that mask real regressions. |
| **Test nested Java/JNI names at boundaries** | Name-conversion tests must include a `$` nested type and assert the final manifest, map, rule, or generated-source output—not only the helper. |

---

## Test Reliability (Flake Prevention)

Patterns that produced repeated CI flakes in this repository. Flag them in new or modified tests as ❌ when they can fail without a product bug, and ask for the deterministic alternative.

| Check | What to look for |
|-------|-----------------|
| **No wall-clock thresholds** | Assertions such as `elapsed < 160 ms` or build/install times compared against a reference measure agent load, not correctness. Assert the behavior instead, for example that a target or task was skipped (`AssertTargetIsSkipped`) or that the output changed. Performance belongs in a benchmark, not a pass/fail test. |
| **No fixed sleeps as synchronization** | `Thread.Sleep`, `Task.Delay`, or a short fixed timeout that waits for a process, device, emulator, logcat line, or background task to be "ready" is a race. Wait for an explicit condition (exit code, readiness property, `sys.boot_completed`, a unique log marker, `TaskCompletionSource`) with a generous deadline, and print diagnostics when the deadline expires. |
| **Deterministic concurrency and cancellation tests** | Cancellation, dispose, and threading tests must control ordering with gates (`TaskCompletionSource`, `ManualResetEventSlim`, blocking streams) so the code under test is provably in the intended state before the action. A test that "usually" hits the window by delaying is flaky. Do not rely on GC timing or finalizers without a deterministic trigger. |
| **Unique, isolated test directories** | Every parameterized case must get its own output directory. Directory names derived from `TestName` or a subset of parameters can collide across `[TestCase]`/`[Values]` rows (for example rows that differ only by runtime or string arguments), so concurrent or sequential cases overwrite each other. Include all distinguishing parameters, or use the per-test `Root`. |
| **No shared mutable global state** | Tests that write to shared caches (NuGet, Maven, `~/.android`, `%TEMP%` paths with fixed names), environment variables, the current directory, or static fields can interfere when run in parallel or in a different order. Isolate the state per test; use `[NonParallelizable]` only when isolation is impossible, and explain why. |
| **No crosstalk when observing process-wide runtime state** | Global reference counts (`JniRuntime.CurrentRuntime.GlobalReferenceCount`, weak grefs), peer/object-map counts, GC collection counts or heap size, finalizer activity, thread-pool state, and other process-wide counters are changed by other tests, test-framework threads, background work, and objects awaiting finalization. Asserting exact before/after values or deltas is flaky. Establish a quiescent baseline (collect and wait for pending finalizers before measuring, and isolate work that the test does not own), then assert a property of the test's own objects, such as a specific handle being released or a weak reference being cleared. Mark tests that cannot avoid process-wide state `[NonParallelizable]`, and allow tolerances only where the noise source is understood. |
| **No remote dependencies when a local fixture works** | Downloading a third-party SDK or a large Maven/NuGet dependency graph makes the test depend on network and upstream availability. Prefer a small in-repo library or package fixture that exercises the same path. When a real download is the behavior under test, classify network failures separately from product failures in the output. |
| **Assert on the current run's outputs only** | Logs, build outputs, TRX files, and logcat buffers left from an earlier run or build can satisfy or fail an assertion. Delete or rotate the log before each build, clear logcat or search after a per-run marker, and take a fresh `logcat -d` before reporting a device failure. |
| **No filesystem-timestamp assumptions** | "File A is newer than B" assertions depend on filesystem timestamp granularity and on tools that preserve timestamps. Prefer content hashes or target/task skip evidence for incremental-build tests. |
| **Bounded process I/O** | Waiting for redirected stdout/stderr to reach EOF hangs when a child process (e.g. a build server, `adb`, or a daemon) inherits the pipes. Wait for process exit with a timeout and bound the output drain. Disable build servers and node reuse in tests that spawn builds. |
| **Windows file locks** | Moving or deleting a just-built tree on Windows can hit sharing violations from lingering processes or scanners. Use the existing helpers (`FileSystemUtils.DeleteDirectoryWithRetry`), retry only the specific transient error (e.g. `ERROR_SHARING_VIOLATION`), bound the total delay, and log each retry. Never retry broadly over `IOException`/`Exception`. |
| **Device state must be reset and awaited** | After changing locale, time zone, settings, or restarting device services, wait until the device and package manager report ready before installing or launching. Restore any changed state in teardown. |
| **Keep device and integration matrices small** | Exhaustive matrices (every culture, every time zone, every runtime × configuration × ABI) multiply device time and flake surface. Use representative cases that cover distinct behaviors; push exhaustive coverage to host-side unit tests. |
| **No test-level retries to hide flakes** | Do not add `[Retry]`, rerun loops, or catch-and-ignore around assertions to make a test pass. Fix the race, or remove the test if it provides no unique coverage. A narrow retry is acceptable only around a documented external transient condition, and it must log every attempt. |
| **Ignored tests need a tracking issue** | `[Ignore]`, `Assert.Ignore`, or platform skips added because a test is flaky must link an issue, so the test is fixed or deleted rather than forgotten. |
| **Justify expensive tests** | A new slow or device test must cover behavior that no cheaper test already covers. If an existing host-side or unit test asserts the same thing, prefer extending it. |
