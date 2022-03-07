using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class X86LlvmIrGenerator : LlvmIrGenerator
	{
		// See https://llvm.org/docs/LangRef.html#data-layout
		//
		//   Value as used by Android NDK's clang++
		//
		protected override string DataLayout => "e-m:e-p:32:32-p270:32:32-p271:32:32-p272:64:64-f64:32:64-f80:32-n8:16:32-S128";
		public override int PointerSize   => 4;
		protected override string Triple     => "i686-unknown-linux-android"; // NDK appends API level, we don't need that

		public X86LlvmIrGenerator (AndroidTargetArch arch, StreamWriter output, string fileName)
			: base (arch, output, fileName)
		{}

		protected override void AddModuleFlagsMetadata (List<LlvmIrMetadataItem> flagsFields)
		{
			base.AddModuleFlagsMetadata (flagsFields);
			flagsFields.Add (MetadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "NumRegisterParameters", 0));
		}
	}
}
