using System;

namespace ApplicationUtility;

class AssemblyStoreVersion
{
	public uint RawVersion      { get; }
	public uint MainVersion     { get; }
	public AssemblyStoreABI ABI { get; }
	public bool Is64Bit         { get; }

	internal AssemblyStoreVersion ()
	{
		ABI = AssemblyStoreABI.Unknown;
	}

	internal AssemblyStoreVersion (uint rawVersion)
	{
		RawVersion = rawVersion;
		Log.Debug ($"AssemblyStoreVersion: raw version is 0x{rawVersion:x}");

		// Main store version is kept in the lower 16 bits of the version word
		MainVersion = rawVersion & 0xFFFF;
		Log.Debug ($"AssemblyStoreVersion: main version is {MainVersion}");

		// ABI is kept in the higher 15 bits of the version word
		uint abi = rawVersion & 0x7FFF0000;
		Log.Debug ($"AssemblyStoreVersion: raw ABI value is 0x{abi:x}");

		if (Enum.IsDefined (typeof(AssemblyStoreABI), abi)) {
			ABI = (AssemblyStoreABI)abi;
		} else {
			ABI = AssemblyStoreABI.Unknown;
		}
		Log.Debug ($"AssemblyStoreVersion: ABI is {ABI}");

		// 64-bit flag is the leftmost bit in the word
		Is64Bit = (rawVersion & 0x80000000) == 0x80000000;
		Log.Debug ($"AssemblyStoreVersion: is store 64-bit? {Is64Bit}");
	}
}
