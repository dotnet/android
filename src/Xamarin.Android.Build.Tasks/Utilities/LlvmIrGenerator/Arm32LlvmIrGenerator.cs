using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class Arm32LlvmIrGenerator : LlvmIrGenerator
	{
		// See https://llvm.org/docs/LangRef.html#data-layout
		//
		//   Value as used by Android NDK's clang++
		//
		protected override string DataLayout => "e-m:e-p:32:32-Fi8-i64:64-v128:64:128-a:0:32-n32-S64";
		protected override int PointerSize   => 4;
		protected override string Triple     => "armv7-unknown-linux-android"; // NDK appends API level, we don't need that

		public Arm32LlvmIrGenerator (StreamWriter output, string fileName)
			: base (output, fileName)
		{}

		protected override void AddModuleFlagsMetadata (List<LlvmIrMetadataItem> flagsFields)
		{
			base.AddModuleFlagsMetadata (flagsFields);
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "min_enum_size", 4));
		}
	}
}
