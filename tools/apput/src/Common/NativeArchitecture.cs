using System;

namespace ApplicationUtility;

/// <summary>
/// Identifies supported Android native CPU architectures. Values are flags and may be combined.
/// </summary>
[Flags]
public enum NativeArchitecture
{
	Unknown = 0x00,

	Arm     = 0x01,
	Arm64   = 0x02,
	X86     = 0x04,
	X64     = 0x08,
}
