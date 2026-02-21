using System;

namespace ApplicationUtility;

[Flags]
public enum NativeArchitecture
{
	Unknown = 0x00,

	Arm     = 0x01,
	Arm64   = 0x02,
	X86     = 0x04,
	X64     = 0x08,
}
