using System;
using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	class LlvmIrModuleX64 : LlvmIrModuleTarget
	{
		public override LlvmIrDataLayout DataLayout { get; }
		public override string Triple => "x86_64-unknown-linux-android21";
		public override AndroidTargetArch TargetArch => AndroidTargetArch.X86_64;
		public override uint NativePointerSize => 8;

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

		public override void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
		{
			attrSet.Add (new TargetCpuFunctionAttribute ("x86-64"));
			attrSet.Add (new TargetFeaturesFunctionAttribute ("+crc32,+cx16,+cx8,+fxsr,+mmx,+popcnt,+sse,+sse2,+sse3,+sse4.1,+sse4.2,+ssse3,+x87"));
			attrSet.Add (new TuneCpuFunctionAttribute ("generic"));
		}

		public override void SetParameterFlags (LlvmIrFunctionParameter parameter)
		{
			base.SetParameterFlags (parameter);
			SetIntegerParameterUpcastFlags (parameter);
		}
	}
}
