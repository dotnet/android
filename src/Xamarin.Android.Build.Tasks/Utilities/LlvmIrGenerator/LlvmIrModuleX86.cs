using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// LLVM IR module target implementation for the x86 (Intel/AMD 32-bit) architecture.
/// Provides x86-specific data layout, target triple, and compilation settings.
/// </summary>
class LlvmIrModuleX86 : LlvmIrModuleTarget
{
	/// <summary>
	/// Gets the data layout specification for the x86 architecture.
	/// </summary>
	public override LlvmIrDataLayout DataLayout { get; }
	/// <summary>
	/// Gets the LLVM target triple for x86 Android.
	/// </summary>
	public override string Triple => "i686-unknown-linux-android21";
	/// <summary>
	/// Gets the Android target architecture (x86).
	/// </summary>
	public override AndroidTargetArch TargetArch => AndroidTargetArch.X86;
	/// <summary>
	/// Gets the size of native pointers in bytes (4 for 32-bit architecture).
	/// </summary>
	public override uint NativePointerSize => 4;
	/// <summary>
	/// Gets a value indicating whether this is a 64-bit architecture.
	/// </summary>
	public override bool Is64Bit => false;

	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrModuleX86"/> class.
	/// Sets up the x86-specific data layout based on Android NDK specifications.
	/// </summary>
	public LlvmIrModuleX86 ()
	{
		//
		// As per Android NDK:
		//   target datalayout = "e-m:e-p:32:32-p270:32:32-p271:32:32-p272:64:64-f64:32:64-f80:32-n8:16:32-S128"
		//
		DataLayout = new LlvmIrDataLayout {
			LittleEndian = true,
			Mangling = new LlvmIrDataLayoutMangling (LlvmIrDataLayoutManglingOption.ELF),

			PointerSize = new List<LlvmIrDataLayoutPointerSize> {
				new LlvmIrDataLayoutPointerSize (size: 32, abi: 32),
				new LlvmIrDataLayoutPointerSize (size: 32, abi: 32) {
					AddressSpace = 270,
				},
				new LlvmIrDataLayoutPointerSize (size: 32, abi: 32) {
					AddressSpace = 271,
				},
				new LlvmIrDataLayoutPointerSize (size: 64, abi: 64) {
					AddressSpace = 272,
				},
			},

			FloatAlignment = new List<LlvmIrDataLayoutFloatAlignment> {
				new LlvmIrDataLayoutFloatAlignment (size: 64, abi: 32, pref: 64), // f64
				new LlvmIrDataLayoutFloatAlignment (size: 80, abi: 32), // f80
			},

			NativeIntegerWidths = new List<uint> { 8, 16, 32 },
			StackAlignment = 128,
		};
	}

	/// <summary>
	/// Adds x86-specific attributes to the function attribute set.
	/// </summary>
	/// <param name="attrSet">The function attribute set to add x86-specific attributes to.</param>
	public override void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
	{
		attrSet.Add (new TargetCpuFunctionAttribute ("i686"));
		attrSet.Add (new TargetFeaturesFunctionAttribute ("+cx8,+mmx,+sse,+sse2,+sse3,+ssse3,+x87"));
		attrSet.Add (new TuneCpuFunctionAttribute ("generic"));
		attrSet.Add (new StackrealignFunctionAttribute ());
	}

	/// <summary>
	/// Sets x86-specific parameter flags for function parameters.
	/// </summary>
	/// <param name="parameter">The function parameter to set flags on.</param>
	public override void SetParameterFlags (LlvmIrFunctionParameter parameter)
	{
		base.SetParameterFlags (parameter);
		SetIntegerParameterUpcastFlags (parameter);
	}

	/// <summary>
	/// Adds x86-specific metadata to the metadata manager.
	/// </summary>
	/// <param name="manager">The metadata manager to add x86-specific metadata to.</param>
	public override void AddTargetSpecificMetadata (LlvmIrMetadataManager manager)
	{
		LlvmIrMetadataItem flags = GetFlagsMetadata (manager);

		flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "NumRegisterParameters", 0));
	}
}
