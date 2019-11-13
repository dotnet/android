using System;

namespace Xamarin.Android.Tasks
{
	class X86NativeAssemblerTargetProvider : NativeAssemblerTargetProvider
	{
		public override bool Is64Bit { get; }
		public override string PointerFieldType { get; }
		public override string TypePrefix { get; } = "@";

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
