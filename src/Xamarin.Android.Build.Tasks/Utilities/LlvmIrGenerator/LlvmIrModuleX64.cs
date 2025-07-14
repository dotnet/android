using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// LLVM IR module target implementation for the x86-64 (Intel/AMD 64-bit) architecture.
/// Provides x86-64-specific data layout, target triple, and compilation settings.
/// </summary>
class LlvmIrModuleX64 : LlvmIrModuleTarget
{
	/// <summary>
	/// Gets the data layout specification for the x86-64 architecture.
	/// </summary>
	public override LlvmIrDataLayout DataLayout { get; }
	/// <summary>
	/// Gets the LLVM target triple for x86-64 Android.
	/// </summary>
	public override string Triple => "x86_64-unknown-linux-android21";
	/// <summary>
	/// Gets the Android target architecture (x86-64).
	/// </summary>
	public override AndroidTargetArch TargetArch => AndroidTargetArch.X86_64;
	/// <summary>
	/// Gets the size of native pointers in bytes (8 for 64-bit architecture).
	/// </summary>
	public override uint NativePointerSize => 8;
	/// <summary>
	/// Gets a value indicating whether this is a 64-bit architecture.
	/// </summary>
	public override bool Is64Bit => true;

	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrModuleX64"/> class.
	/// Sets up the x86-64-specific data layout based on Android NDK specifications.
	/// </summary>
	public LlvmIrModuleX64 ()
	{
		//
		// As per Android NDK:
		//   target datalayout = "e-m:e-p270:32:32-p271:32:32-p272:64:64-i64:64-f80:128-n8:16:32:64-S128"
		//
		DataLayout = new LlvmIrDataLayout {
			LittleEndian = true,
			Mangling = new LlvmIrDataLayoutMangling (LlvmIrDataLayoutManglingOption.ELF),

			PointerSize = new List<LlvmIrDataLayoutPointerSize> {
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

			IntegerAlignment = new List<LlvmIrDataLayoutIntegerAlignment> {
				new LlvmIrDataLayoutIntegerAlignment (size: 64, abi: 64), // i64
			},

			FloatAlignment = new List<LlvmIrDataLayoutFloatAlignment> {
				new LlvmIrDataLayoutFloatAlignment (size: 80, abi: 128), // f80
			},

			NativeIntegerWidths = new List<uint> { 8, 16, 32, 64 },
			StackAlignment = 128,
		};
	}

	/// <summary>
	/// Adds x86-64-specific attributes to the function attribute set.
	/// </summary>
	/// <param name="attrSet">The function attribute set to add x86-64-specific attributes to.</param>
	public override void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
	{
		attrSet.Add (new TargetCpuFunctionAttribute ("x86-64"));
		attrSet.Add (new TargetFeaturesFunctionAttribute ("+crc32,+cx16,+cx8,+fxsr,+mmx,+popcnt,+sse,+sse2,+sse3,+sse4.1,+sse4.2,+ssse3,+x87"));
		attrSet.Add (new TuneCpuFunctionAttribute ("generic"));
	}

	/// <summary>
	/// Sets x86-64-specific parameter flags for function parameters.
	/// </summary>
	/// <param name="parameter">The function parameter to set flags on.</param>
	public override void SetParameterFlags (LlvmIrFunctionParameter parameter)
	{
		base.SetParameterFlags (parameter);
		SetIntegerParameterUpcastFlags (parameter);
	}

	/// <summary>
	/// Gets the alignment for aggregate objects according to the System V ABI for x86-64.
	/// Aggregates 16 bytes or larger must be aligned to at least 16 bytes.
	/// </summary>
	/// <param name="maxFieldAlignment">The maximum alignment requirement of any field in the aggregate.</param>
	/// <param name="dataSize">The total size of the aggregate data.</param>
	/// <returns>The alignment to use for the aggregate object.</returns>
	public override int GetAggregateAlignment (int maxFieldAlignment, ulong dataSize)
	{
		// System V ABI for x86_64 mandates that any aggregates 16 bytes or more long will
		// be aligned at at least 16 bytes
		//
		//  See: https://refspecs.linuxbase.org/elf/x86_64-abi-0.99.pdf (Section '3.1.2 Data Representation', "Aggregates and Unions")
		//
		if (dataSize >= 16 && maxFieldAlignment < 16) {
			return 16;
		}

		return maxFieldAlignment;
	}
}
