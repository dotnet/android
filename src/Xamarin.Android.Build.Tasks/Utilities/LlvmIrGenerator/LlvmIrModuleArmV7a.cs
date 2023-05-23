using System;
using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	class LlvmIrModuleArmV7a : LlvmIrModuleTarget
	{
		public override LlvmIrDataLayout DataLayout { get; }
		public override string Triple => "armv7-unknown-linux-android21";
		public override AndroidTargetArch TargetArch => AndroidTargetArch.Arm;
		public override uint NativePointerSize => 4;

		public LlvmIrModuleArmV7a ()
		{
			//
			// As per Android NDK:
			//   target datalayout = "e-m:e-p:32:32-Fi8-i64:64-v128:64:128-a:0:32-n32-S64"
			//
			DataLayout = new LlvmIrDataLayout {
				LittleEndian = true,
				Mangling = new LlvmIrDataLayoutMangling (LlvmIrDataLayoutManglingOption.ELF),

				PointerSize = new List<LlvmIrDataLayoutPointerSize> {
					new LlvmIrDataLayoutPointerSize (size: 32, abi: 32),
				},

				FunctionPointerAlignment = new LlvmIrDataLayoutFunctionPointerAlignment (LlvmIrDataLayoutFunctionPointerAlignmentType.Independent, abi: 8),

				IntegerAlignment = new List<LlvmIrDataLayoutIntegerAlignment> {
					new LlvmIrDataLayoutIntegerAlignment (size: 64, abi: 64), // i64
				},

				VectorAlignment = new List<LlvmIrDataLayoutVectorAlignment> {
					new LlvmIrDataLayoutVectorAlignment (size: 128, abi: 64, pref: 128), // v128
				},

				AggregateObjectAlignment = new LlvmIrDataLayoutAggregateObjectAlignment (abi: 0, pref: 32),
				NativeIntegerWidths = new List<uint> { 32 },
				StackAlignment = 64,
			};
		}

		public override void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
		{
			throw new NotImplementedException ();
		}
	}
}
