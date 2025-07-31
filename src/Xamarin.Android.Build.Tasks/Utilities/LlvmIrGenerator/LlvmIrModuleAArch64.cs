using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// LLVM IR module target implementation for the AArch64 (ARM 64-bit) architecture.
/// Provides AArch64-specific data layout, target triple, and compilation settings.
/// </summary>
class LlvmIrModuleAArch64 : LlvmIrModuleTarget
{
	/// <summary>
	/// Gets the data layout specification for the AArch64 architecture.
	/// </summary>
	public override LlvmIrDataLayout DataLayout { get; }
	/// <summary>
	/// Gets the LLVM target triple for AArch64 Android.
	/// </summary>
	public override string Triple => "aarch64-unknown-linux-android21";
	/// <summary>
	/// Gets the Android target architecture (ARM 64-bit).
	/// </summary>
	public override AndroidTargetArch TargetArch => AndroidTargetArch.Arm64;
	/// <summary>
	/// Gets the size of native pointers in bytes (8 for 64-bit architecture).
	/// </summary>
	public override uint NativePointerSize => 8;
	/// <summary>
	/// Gets a value indicating whether this is a 64-bit architecture.
	/// </summary>
	public override bool Is64Bit => true;

	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrModuleAArch64"/> class.
	/// Sets up the AArch64-specific data layout based on Android NDK specifications.
	/// </summary>
	public LlvmIrModuleAArch64 ()
	{
		//
		// As per Android NDK:
		//   target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
		//
		DataLayout = new LlvmIrDataLayout {
			LittleEndian = true,
			Mangling = new LlvmIrDataLayoutMangling (LlvmIrDataLayoutManglingOption.ELF),

			IntegerAlignment = new List<LlvmIrDataLayoutIntegerAlignment> {
				new LlvmIrDataLayoutIntegerAlignment (size: 8, abi: 8, pref: 32), // i8
				new LlvmIrDataLayoutIntegerAlignment (size: 16, abi: 16, pref: 32), // i16
				new LlvmIrDataLayoutIntegerAlignment (size: 64, abi: 64), // i64
				new LlvmIrDataLayoutIntegerAlignment (size: 128, abi: 128), // i128
			},

			NativeIntegerWidths = new List<uint> { 32, 64},
			StackAlignment = 128,
		};
	}

	/// <summary>
	/// Adds AArch64-specific attributes to the function attribute set.
	/// </summary>
	/// <param name="attrSet">The function attribute set to add AArch64-specific attributes to.</param>
	public override void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
	{
		attrSet.Add (new TargetCpuFunctionAttribute ("generic"));
		attrSet.Add (new TargetFeaturesFunctionAttribute ("+fix-cortex-a53-835769,+neon,+outline-atomics,+v8a"));
	}

	/// <summary>
	/// Adds AArch64-specific metadata to the metadata manager.
	/// </summary>
	/// <param name="manager">The metadata manager to add AArch64-specific metadata to.</param>
	public override void AddTargetSpecificMetadata (LlvmIrMetadataManager manager)
	{
		LlvmIrMetadataItem flags = GetFlagsMetadata (manager);

		flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "branch-target-enforcement", 0));
		flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "sign-return-address", 0));
		flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "sign-return-address-all", 0));
		flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "sign-return-address-with-bkey", 0));
	}
}
