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
