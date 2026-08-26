# APK tests in Helix prototype

This guarded prototype builds the existing on-device APK test flavors once in Azure
Pipelines, then submits one deterministic Helix work item per flavor. Existing
measurements put every device test invocation below the configurable 15-minute target,
so splitting an APK's NUnit inventory further would add installation overhead without
improving the tail.

Each small work item contains a signed APK, Android `platform-tools`, its
package/instrumentation metadata, and a PowerShell runner, matching the proven
PR #12020 payload layout.
The runner installs the APK, invokes `am instrument`, pulls the test-generated TRX,
captures logcat/device state, and returns failure when instrumentation or any test
fails.

The prototype uses `windows.11.amd64.android.open`, following the MAUI R2R Android
Helix implementation. The existing macOS emulator lanes remain enabled for same-build
inventory and outcome comparison.
