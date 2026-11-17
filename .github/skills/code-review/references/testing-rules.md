# Testing Review Rules

Guidance for test code. The repo-specific conventions (e.g., `BaseTest`,
`dotnet-local`) are included here alongside general best practices.

---

## Testing Checks

| Check | What to look for |
|-------|-----------------|
| **`BaseTest`** | Build integration fixtures that need its project, SDK, or platform helpers can follow existing `BaseTest` patterns; standalone tooling fixtures need not inherit it. |
| **Test framework** | Use MSTest conventions (`[TestClass]`, `[TestMethod]`, `[DataRow]`/`[DynamicData]`, `[TestCategory]`); add `[DoNotParallelize]` only when needed. |
| **Test command** | See the `tests` skill: standalone tooling uses `dotnet test`; full-build tests use the local SDK. |
| **Tests must earn their place** | Flag new tests that duplicate coverage, overfit the implementation, or rely on GC timing. Request a test only for an unprotected real regression, never as a default. |
| **Test assertions** | Assert the relevant behavior and add diagnostic context when a failure would otherwise be ambiguous. |
| **Deterministic test data** | Tests should not depend on system locale, timezone, or current date. Use explicit `CultureInfo.InvariantCulture` and hardcoded dates when testing formatting. |
| **Android tools SDK/JDK fixtures** | Tests under `tests/Xamarin.Android.Tools.AndroidSdk-Tests/` commonly build isolated fake SDK/JDK layouts and platform-specific tool scripts (`.bat` on Windows, shell scripts on Unix). Keep these fixtures self-contained and cleaned up in setup/teardown rather than depending on the developer machine's installed SDK/JDK. |
| **Generator Invoker tests** | When a change affects invoker generation, cover the relevant interface and `*Invoker` behavior, including default methods or virtual dispatch where applicable. |
| **JVM-dependent tests** | Tests that require a running JVM must be in projects that configure the JVM environment (e.g., `Java.Interop-Tests`). Verify that test classes requiring a JVM are not placed in unit-test-only projects, where they will silently skip or fail with obscure errors. |
| **Expected codegen output tests** | Generator tests that compare against expected output files should be updated when the expected format changes. Stale expected-output files cause spurious test failures that mask real regressions. |
| **Test nested Java/JNI names at boundaries** | Name-conversion tests must include a `$` nested type and assert the final manifest, map, rule, or generated-source output—not only the helper. |
| **Retain temporary peers in JNI tests** | Don't chain `.Class.Handle` on temporaries (e.g., `peer.Class.Handle`): on CoreCLR/NativeAOT, `JavaMarshalGCBridge` deletes grefs of unreachable peers (even `java.lang.Class`), causing CheckJNI aborts. Reuse `using var klass = peer.Class;` (or `GC.KeepAlive`). |
| **No GC-timing tests for `GC.KeepAlive` fixes** | Don't require or add `GC.Collect ()`, `WeakReference.IsAlive`, or GC stress tests — JIT liveness and GC timing aren't deterministic and make tests flaky. Use deterministic coverage only where it catches a real regression: codegen expected-output assertions, `JniEnvironment.LocalReferenceCount` leak checks, or targeted functional tests. |

---

## Test Reliability (Flake Prevention)

Flag as ❌ in new or modified tests when they can fail without a product bug; ask for the deterministic alternative.

| Check | What to look for |
|-------|-----------------|
| **No wall-clock thresholds** | Elapsed-time assertions measure agent load. Assert behavior (`AssertTargetIsSkipped`, changed output); benchmark performance separately. |
| **No sleeps as synchronization** | Fixed `Thread.Sleep`/`Task.Delay` waits for a process, device, or log line are races. Wait for an explicit condition (exit code, `sys.boot_completed`, unique log marker, `TaskCompletionSource`) with a generous deadline and diagnostics on timeout. |
| **Deterministic concurrency tests** | Reach the intended state with gates (`TaskCompletionSource`, `ManualResetEventSlim`, blocking streams), not delays, GC timing, or finalizers. |
| **Unique test directories** | Use a per-case subdirectory under the shared `Root`, named with every distinguishing parameter; `TestName` alone collides across `[TestCase]`/`[Values]` rows. |
| **No shared mutable state** | Don't share caches (NuGet, Maven, `~/.android`), fixed `%TEMP%` paths, environment variables, the current directory, or statics. Use `[NonParallelizable]`, with a reason, only when isolation is impossible. |
| **No process-wide counter crosstalk** | Other tests and pending finalizers change gref counts, peer maps, GC counts, and thread-pool state. Don't assert exact deltas; assert on objects the test owns (a specific handle or weak reference). For `GC.KeepAlive` fixes, follow the GC-timing rule above. |
| **No avoidable network** | Prefer small in-repo fixtures to remote SDKs or dependency graphs; when a download is under test, report network failures separately. |
| **Current run's outputs only** | Delete old logs before each build. Clear logcat before the run or search after a unique per-run marker; `logcat -d` alone includes old entries. |
| **No timestamp assumptions** | Prefer content hashes or target-skip evidence to "A is newer than B". |
| **Bounded process I/O** | Output EOF never arrives if a child (build server, `adb`) inherits the pipes. Wait for exit with a timeout, bound the drain, and disable build servers and node reuse. |
| **Windows file locks** | Identify the holder (`handle.exe`, ProcMon) and fix it if it's ours. Retry only a proven transient external lock: that error only, bounded, logging each attempt; never broadly on `IOException`. |
| **Reset and await device state** | After changing locale, time zone, or settings, wait for the device and package manager to be ready; restore in teardown. |
| **Small device matrices** | Use representative cases; put exhaustive culture/runtime×ABI coverage in host-side tests. |
| **No retries that hide flakes** | No `[Retry]`, rerun loops, or catch-and-ignore around assertions. Fix the race, or remove a test without unique coverage. |
| **Ignored tests need an issue** | Flaky `[Ignore]`, `Assert.Ignore`, or platform skips must link a tracking issue. |
| **Justify expensive tests** | New slow or device tests must cover behavior no cheaper test covers. |
