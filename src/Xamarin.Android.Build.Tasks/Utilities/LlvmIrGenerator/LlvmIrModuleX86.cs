using System;
using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once the refactoring is done
	using LlvmIrModuleMergeBehavior = LLVMIR.LlvmIrModuleMergeBehavior;

	class LlvmIrModuleX86 : LlvmIrModuleTarget
	{
		public override LlvmIrDataLayout DataLayout { get; }
		public override string Triple => "i686-unknown-linux-android21";
		public override AndroidTargetArch TargetArch => AndroidTargetArch.X86;
		public override uint NativePointerSize => 4;
		public override bool Is64Bit => true;

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

		public override void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
		{
			attrSet.Add (new TargetCpuFunctionAttribute ("i686"));
			attrSet.Add (new TargetFeaturesFunctionAttribute ("+cx8,+mmx,+sse,+sse2,+sse3,+ssse3,+x87"));
			attrSet.Add (new TuneCpuFunctionAttribute ("generic"));
			attrSet.Add (new StackrealignFunctionAttribute ());
		}

		public override void SetParameterFlags (LlvmIrFunctionParameter parameter)
		{
			base.SetParameterFlags (parameter);
			SetIntegerParameterUpcastFlags (parameter);
		}

		public override void AddTargetSpecificMetadata (LlvmIrMetadataManager manager)
		{
			LlvmIrMetadataItem flags = GetFlagsMetadata (manager);

			flags.AddReferenceField (manager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "NumRegisterParameters", 0));
		}
	}
}
