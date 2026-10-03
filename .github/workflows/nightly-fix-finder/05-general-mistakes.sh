#!/usr/bin/env bash
# Category: General Mistakes

cat << 'GUIDANCE'
## Category: General Mistakes

### What to look for
Read the randomly selected source files thoroughly and look for **real bugs**:
- Logic errors, off-by-one errors
- Null dereferences not protected by the surrounding code
- Race conditions on shared state
- Resource leaks (missing `using` / `Dispose`)
- Dead code, unreachable branches
- Copy-paste errors (same code repeated with subtle differences that look unintended)
- Incorrect exception handling (catching too broadly, swallowing without logging)
- Relative-path filesystem operations in asynchronous/yielding MSBuild tasks

### MSBuild working-directory races
Use the focused filesystem scan below as candidate data, not proof of a bug:
- Trace the task's base classes, `RunTaskAsync`, and `Yield` / `Reacquire`
  lifecycle and the actual shipped target invocation. `AsyncTask` defaults
  `YieldDuringToolExecution` to false; a similarly named MSBuild property does
  not enable it unless the task invocation passes that property. Confirm that
  yielding is enabled and a relative path can reach the filesystem operation.
  Inheriting `AsyncTask` or containing `await` alone is not evidence of a race.
- The process current directory is shared state. MSBuild can change it when
  servicing another project on a yielded node, even for already-built targets
  that execute no tasks. A task's captured `WorkingDirectory` remains stable.
- Follow each path through helpers to its actual filesystem operation. Watch
  for `File` / `Directory` calls, `Files.CopyIf*` helpers, and
  `Path.GetFullPath (relativePath)` that rely on the live current directory.
  A child process's `ProcessStartInfo.WorkingDirectory` does not anchor managed
  filesystem operations in the parent task.
- Check downstream consumers: an `Exists()` guard can silently omit a misplaced
  output from item groups such as `ProguardConfiguration` or `FileWrites`.
- Prefer the task's existing captured-directory path helper, such as
  `GetFullPath (path)`, while preserving absolute paths and the owning TFM.
  Do not disable yielding or change the process current directory as the fix.
- When practical, add a deterministic regression that captures directory A,
  changes the current directory to B before the filesystem operation, and
  verifies the output remains under A and is available to its consumer.
  Restore the current directory in `finally`, keep such tests nonparallel,
  and cover both relative and absolute inputs. The regression must fail before
  the fix and pass afterward.

Issue #12942 is an example: Aapt2Link used its captured directory for AAPT's
per-manifest rules, but not for the merged `ProguardRuleOutput` write.

### How to fix
Implement the smallest complete fix for the actual bug and add a focused
regression test when practical.

### What NOT to flag
- Formatting, whitespace, or style — not actionable for a fix PR
- "Could be cleaner" subjective preferences with no functional impact
- Generated files (`*.generated.cs`, `*.Designer.cs`, `AssemblyInfo.cs`)
- Paths already proven absolute or anchored to the task's captured directory
- Relative paths intentionally resolved against an explicit, stable base
- Tasks whose shipped invocations leave yielding disabled, unless another
  independently verified current-directory change makes the operation unsafe.
  Do not open a hardening-only PR based on hypothetical future yielding.
- Every `async` or filesystem call: prove a relative path reaches a filesystem
  operation while yielding or another verified directory change is possible
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Random C# source files in shipped code under src/ for general review (sample)"
find src -name '*.cs' -type f \
    ! -path '*/obj/*' ! -path '*/bin/*' \
    ! -path '*/Tests/*' ! -path '*/Test/*' ! -path '*/tests/*' \
    ! -name '*.generated.cs' ! -name '*.Designer.cs' ! -name 'AssemblyInfo.cs' \
    ! -name '*Test.cs' ! -name '*Tests.cs' \
    2>/dev/null | shuf -n 5

echo ""
echo "### MSBuild task filesystem operations to investigate for working-directory races (sample)"
if MATCHES=$(grep -rnE \
    '(File|Directory|Files|Path)[[:space:]]*\.[[:space:]]*(Write[A-Za-z]*|Read[A-Za-z]*|Append[A-Za-z]*|Copy[A-Za-z]*|Move|Delete|Exists|CreateDirectory|Enumerate[A-Za-z]*|GetFiles|GetDirectories|GetFullPath)[[:space:]]*\(' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' --exclude='AssemblyInfo.cs' \
    --exclude='*Test.cs' --exclude='*Tests.cs' \
    src/Xamarin.Android.Build.Tasks/Tasks \
    src/Microsoft.Android.Build.Tasks/Tasks \
    src/Microsoft.Android.Build.BaseTasks); then
    printf '%s\n' "$MATCHES" | shuf -n 40
else
    STATUS=$?
    if [ "$STATUS" -eq 1 ]; then
        echo "None found"
    else
        echo "Filesystem scan failed (grep exit code $STATUS)" >&2
        exit "$STATUS"
    fi
fi
