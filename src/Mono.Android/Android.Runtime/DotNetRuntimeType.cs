using System;

namespace Android.Runtime;

enum DotNetRuntimeType
{
	Unknown,
	MonoVM,
	CoreCLR,
	NativeAOT,
}

// This looks weird, see comments in RuntimeTypeInternal.cs
class DotNetRuntimeTypeConverter
{
	// Values for the JnienvInitializeArgs.runtimeType field
	//
	// NOTE: Keep this in sync with managed side in src/native/common/include/managed-interface.hh
	const uint RuntimeTypeMonoVM    = 0x0001;
	const uint RuntimeTypeCoreCLR   = 0x0002;
	const uint RuntimeTypeNativeAOT = 0x0004;

	public static DotNetRuntimeType Convert (uint runtimeType)
	{
		return runtimeType switch {
			RuntimeTypeMonoVM => DotNetRuntimeType.MonoVM,
			RuntimeTypeCoreCLR => DotNetRuntimeType.CoreCLR,
			RuntimeTypeNativeAOT => DotNetRuntimeType.NativeAOT,
			_ => throw new NotSupportedException ($"Internal error: unsupported runtime type {runtimeType:x}")
		};
	}
}
