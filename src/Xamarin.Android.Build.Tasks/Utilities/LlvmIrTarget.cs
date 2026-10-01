#nullable enable
using System;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Per-ABI constants used when writing textual LLVM IR modules.  The data layouts and triples
/// are the ones used by the Android NDK clang for the respective targets.
/// </summary>
sealed class LlvmIrTarget
{
	public static readonly LlvmIrTarget Arm = new (
		AndroidTargetArch.Arm,
		triple: "armv7-unknown-linux-android21",
		dataLayout: "e-m:e-p:32:32-Fi8-i64:64-v128:64:128-a:0:32-n32-S64",
		pointerSize: 4,
		moduleFlags: [
			"i32 1, !\"min_enum_size\", i32 4",
		]
	);

	public static readonly LlvmIrTarget Arm64 = new (
		AndroidTargetArch.Arm64,
		triple: "aarch64-unknown-linux-android21",
		dataLayout: "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128",
		pointerSize: 8,
		moduleFlags: [
			"i32 1, !\"branch-target-enforcement\", i32 0",
			"i32 1, !\"sign-return-address\", i32 0",
			"i32 1, !\"sign-return-address-all\", i32 0",
			"i32 1, !\"sign-return-address-with-bkey\", i32 0",
		]
	);

	public static readonly LlvmIrTarget X86 = new (
		AndroidTargetArch.X86,
		triple: "i686-unknown-linux-android21",
		dataLayout: "e-m:e-p:32:32-p270:32:32-p271:32:32-p272:64:64-f64:32:64-f80:32-n8:16:32-S128",
		pointerSize: 4,
		moduleFlags: [
			"i32 1, !\"NumRegisterParameters\", i32 0",
		]
	);

	public static readonly LlvmIrTarget X86_64 = new (
		AndroidTargetArch.X86_64,
		triple: "x86_64-unknown-linux-android21",
		dataLayout: "e-m:e-p270:32:32-p271:32:32-p272:64:64-i64:64-f80:128-n8:16:32:64-S128",
		pointerSize: 8,
		moduleFlags: []
	);

	public AndroidTargetArch Arch { get; }
	public string Triple { get; }
	public string DataLayout { get; }
	public uint PointerSize { get; }
	public bool Is64Bit => PointerSize == 8;

	/// <summary>
	/// Target-specific entries appended to the <c>!llvm.module.flags</c> metadata.
	/// </summary>
	public string[] ModuleFlags { get; }

	LlvmIrTarget (AndroidTargetArch arch, string triple, string dataLayout, uint pointerSize, string[] moduleFlags)
	{
		Arch = arch;
		Triple = triple;
		DataLayout = dataLayout;
		PointerSize = pointerSize;
		ModuleFlags = moduleFlags;
	}

	public static LlvmIrTarget Get (AndroidTargetArch arch)
	{
		return arch switch {
			AndroidTargetArch.Arm    => Arm,
			AndroidTargetArch.Arm64  => Arm64,
			AndroidTargetArch.X86    => X86,
			AndroidTargetArch.X86_64 => X86_64,
			_ => throw new InvalidOperationException ($"Unsupported Android target ABI {arch}")
		};
	}

	/// <summary>
	/// Returns the alignment of an aggregate (array or structure) whose most strictly aligned
	/// field requires <paramref name="maxFieldAlignment"/> and whose data is <paramref name="dataSize"/>
	/// bytes long.  For arrays of structures, and for structures, <paramref name="dataSize"/> is computed
	/// using the sum of sizes of all the non-pointer structure members (i.e. excluding any padding and
	/// pointers), in order to produce the same symbol alignment the native code has always been built with.
	/// </summary>
	public ulong GetAggregateAlignment (ulong maxFieldAlignment, ulong dataSize)
	{
		// System V ABI for x86_64 mandates that any aggregates 16 bytes or more long will
		// be aligned at at least 16 bytes
		//
		//  See: https://refspecs.linuxbase.org/elf/x86_64-abi-0.99.pdf (Section '3.1.2 Data Representation', "Aggregates and Unions")
		//
		if (Arch == AndroidTargetArch.X86_64 && dataSize >= 16 && maxFieldAlignment < 16) {
			return 16;
		}

		return maxFieldAlignment;
	}
}
