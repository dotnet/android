using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

// NOTE: silences the CA1416 analyzer about supported Android APIs
[assembly: TargetPlatformAttribute("Android35.0")]
[assembly: SupportedOSPlatformAttribute("Android21.0")]

// Required for LibraryImport with struct parameters passed by reference
[assembly: DisableRuntimeMarshalling]
