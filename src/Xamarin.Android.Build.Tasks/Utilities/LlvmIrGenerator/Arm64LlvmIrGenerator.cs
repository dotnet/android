using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class Arm64LlvmIrGenerator : LlvmIrGenerator
	{
		// See https://llvm.org/docs/LangRef.html#data-layout
		//
		//   Value as used by Android NDK's clang++
		//
		protected override string DataLayout => "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128";
		public override int PointerSize   => 8;
		protected override string Triple     => "aarch64-unknown-linux-android"; // NDK appends API level, we don't need that

		static readonly LlvmFunctionAttributeSet commonAttributes = new LlvmFunctionAttributeSet {
			new FramePointerFunctionAttribute ("non-leaf"),
			new TargetCpuFunctionAttribute ("generic"),
			new TargetFeaturesFunctionAttribute ("+neon,+outline-atomics"),
		};

		public Arm64LlvmIrGenerator (AndroidTargetArch arch, StreamWriter output, string fileName)
			: base (arch, output, fileName)
		{}

		protected override void AddModuleFlagsMetadata (List<LlvmIrMetadataItem> flagsFields)
		{
			base.AddModuleFlagsMetadata (flagsFields);

			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "branch-target-enforcement", 0));
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "sign-return-address", 0));
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "sign-return-address-all", 0));
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "sign-return-address-with-bkey", 0));
		}

		protected override void InitFunctionAttributes ()
		{
			base.InitFunctionAttributes ();

			FunctionAttributes[FunctionAttributesXamarinAppInit].Add (commonAttributes);
			FunctionAttributes[FunctionAttributesJniMethods].Add (commonAttributes);
		}
	}
}
