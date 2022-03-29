using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class Arm32LlvmIrGenerator : LlvmIrGenerator
	{
		// See https://llvm.org/docs/LangRef.html#data-layout
		//
		//   Value as used by Android NDK's clang++
		//
		protected override string DataLayout => "e-m:e-p:32:32-Fi8-i64:64-v128:64:128-a:0:32-n32-S64";
		public override int PointerSize   => 4;
		protected override string Triple     => "armv7-unknown-linux-android"; // NDK appends API level, we don't need that

		static readonly LlvmFunctionAttributeSet commonAttributes = new LlvmFunctionAttributeSet {
			new FramePointerFunctionAttribute ("all"),
			new TargetCpuFunctionAttribute ("generic"),
			new TargetFeaturesFunctionAttribute ("+armv7-a,+d32,+dsp,+fp64,+neon,+thumb-mode,+vfp2,+vfp2sp,+vfp3,+vfp3d16,+vfp3d16sp,+vfp3sp,-aes,-fp-armv8,-fp-armv8d16,-fp-armv8d16sp,-fp-armv8sp,-fp16,-fp16fml,-fullfp16,-sha2,-vfp4,-vfp4d16,-vfp4d16sp,-vfp4sp"),
		};

		public Arm32LlvmIrGenerator (AndroidTargetArch arch, StreamWriter output, string fileName)
			: base (arch, output, fileName)
		{}

		protected override void AddModuleFlagsMetadata (List<LlvmIrMetadataItem> flagsFields)
		{
			base.AddModuleFlagsMetadata (flagsFields);
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "min_enum_size", 4));
		}

		protected override void InitFunctionAttributes ()
		{
			base.InitFunctionAttributes ();

			FunctionAttributes[FunctionAttributesXamarinAppInit].Add (commonAttributes);
			FunctionAttributes[FunctionAttributesJniMethods].Add (commonAttributes);
		}
	}
}
