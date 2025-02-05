# Notes

## Potential optimizations

  * https://github.com/dotnet/runtime/blob/9b24fb60a19f62620ca1fc5e4eb2e3ae0b3b086d/src/coreclr/binder/assemblybindercommon.cpp#L844-L889
    * Managed C++ assemblies aren't available on Unix, no point in looking for them
    * `candidates[]` is `WCHAR*`, while `ProbeAppBundle` takes UTF-8 - no point in doing the
      conversion here
  * Host contract
    * It might make sense to pass strings as Unicode to host and back, to avoid conversions.
      p/invoke names for instance, can be Unicode. So can be the assembly names. Native library
      names should be UTF-8, but we can generate lookup tables for those at application build time
      (e.g. indexed by an xxHash of the Unicode version of the name) - no conversion at run time.
    * We need declarations of all he possible HRESULT errors (`S_OK` etc)

## Stuff that should be changed

### Logging
Currently, most of the messages logged by the runtime end up in `/dev/null` (either because they
are disabled in release build or because they log to stdio which doesn't work on Android).

Logcat is the only way to get information from remote devices, especially via Google Play Console.

We should log to logcat:

    + C++ exception messages
    + abort() messages / fatal errors
    + warnings
    + errors

A subsystem should be added which will provide a single function that will do actual output, implementation of which
will be specific to the platform.  API should allow specification of severity, the actual message, and possibly a flag
to indicate whether the process should be aborted (the decision might also be based on the severity).  Severity should
be shared between all targets, which then can (if needed) translate it to the target platform's value(s), if any.

### Process termination
Runtime currently calls `abort()` in several places.  This should probably become part of the host contract instead.
Being part of the contract, the target platform could implement process termination on `abort()` in a uniform way
(includes platform-specific logging, preparation etc)
