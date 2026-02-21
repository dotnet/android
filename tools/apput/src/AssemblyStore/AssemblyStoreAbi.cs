namespace ApplicationUtility;

// Values correspond to those in `xamarin-app.hh`
enum AssemblyStoreABI : uint
{
	Unknown = 0x00000000,

	Arm64   = 0x00010000,
	Arm     = 0x00020000,
	X86     = 0x00030000,
	X64     = 0x00040000,
}
