using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class X64LlvmIrGenerator : LlvmIrGenerator
	{
		// See https://llvm.org/docs/LangRef.html#data-layout
		//
		//   Value as used by Android NDK's clang++
		//
		protected override string DataLayout => "e-m:e-p270:32:32-p271:32:32-p272:64:64-i64:64-f80:128-n8:16:32:64-S128";
		public override int PointerSize   => 8;
		protected override string Triple     => "x86_64-unknown-linux-android"; // NDK appends API level, we don't need that

		static readonly LlvmFunctionAttributeSet commonAttributes = new LlvmFunctionAttributeSet {
			new FramePointerFunctionAttribute ("none"),
			new TargetCpuFunctionAttribute ("x86-64"),
			new TargetFeaturesFunctionAttribute ("+cx16,+cx8,+fxsr,+mmx,+popcnt,+sse,+sse2,+sse3,+sse4.1,+sse4.2,+ssse3,+x87"),
			new TuneCpuFunctionAttribute ("generic"),
		};

		public X64LlvmIrGenerator (AndroidTargetArch arch, StreamWriter output, string fileName)
			: base (arch, output, fileName)
		{}

		protected override int GetAggregateAlignment (int maxFieldAlignment, ulong dataSize)
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

		protected override void InitFunctionAttributes ()
		{
			base.InitFunctionAttributes ();

			FunctionAttributes[FunctionAttributesXamarinAppInit].Add (commonAttributes);
			FunctionAttributes[FunctionAttributesJniMethods].Add (commonAttributes);
		}
	}
}
