using System;
using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once the refactoring is done
	using LlvmIrModuleMergeBehavior = LLVMIR.LlvmIrModuleMergeBehavior;

	class LlvmIrModuleAArch64 : LlvmIrModuleTarget
	{
		public override LlvmIrDataLayout DataLayout { get; }
		public override string Triple => "aarch64-unknown-linux-android21";
		public override AndroidTargetArch TargetArch => AndroidTargetArch.Arm64;
		public override uint NativePointerSize => 8;
		public override bool Is64Bit => true;

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

		public override void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
		{
			attrSet.Add (new TargetCpuFunctionAttribute ("generic"));
			attrSet.Add (new TargetFeaturesFunctionAttribute ("+fix-cortex-a53-835769,+neon,+outline-atomics,+v8a"));
		}

		public override void AddTargetSpecificMetadata (LlvmIrMetadataManager manager)
		{
			LlvmIrMetadataItem flags = GetFlagsMetadata (manager);

			flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "branch-target-enforcement", 0));
			flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "sign-return-address", 0));
			flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "sign-return-address-all", 0));
			flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "sign-return-address-with-bkey", 0));
		}
	}
}
