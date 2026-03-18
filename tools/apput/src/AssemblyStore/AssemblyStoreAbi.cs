namespace ApplicationUtility;

/// <summary>
/// Identifies the ABI (Application Binary Interface) target of an assembly store.
/// Values correspond to those in the native <c>xamarin-app.hh</c> header.
/// </summary>
enum AssemblyStoreABI : uint
{
	Unknown = 0x00000000,

	Arm64   = 0x00010000,
	Arm     = 0x00020000,
	X64     = 0x00030000,
	X86     = 0x00040000,
}
