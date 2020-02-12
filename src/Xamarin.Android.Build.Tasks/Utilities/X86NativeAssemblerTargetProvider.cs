using System;

namespace Xamarin.Android.Tasks
{
	class X86NativeAssemblerTargetProvider : NativeAssemblerTargetProvider
	{
		const string X86 = "x86";
		const string X86_64 = "x86_64";

		public override bool Is64Bit { get; }
		public override string PointerFieldType { get; }
		public override string TypePrefix { get; } = "@";
		public override string AbiName => Is64Bit ? X86_64 : X86;
		public override uint MapModulesAlignBits => Is64Bit ? 4u : 2u;
		public override uint MapJavaAlignBits => Is64Bit ? 4u : 2u;

		public X86NativeAssemblerTargetProvider (bool is64Bit)
		{
			Is64Bit = is64Bit;
			PointerFieldType = is64Bit ? ".quad" : ".long";
		}

		public override string MapType <T> ()
		{
			if (typeof(T) == typeof(Int32) || typeof(T) == typeof(UInt32))
				return ".long";
			return base.MapType <T> ();
		}
	}
}
