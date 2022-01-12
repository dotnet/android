using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks
{
	class Arm64NativeAssemblyGenerator : ArmNativeAssemblyGenerator
	{
		static readonly NativeType pointer = new NativeType {
			Size = 8,
			Alignment = 8,
			Name = ".xword",
		};

		protected override string ArchName => "armv8-a";
		public override bool Is64Bit => true;

		public Arm64NativeAssemblyGenerator (StreamWriter output, string fileName)
			: base (output, fileName)
		{}

		protected override NativeType GetPointerType () => pointer;

		protected override void ConfigureTypeMappings (Dictionary<Type, NativeType?> mapping)
		{
			base.ConfigureTypeMappings (mapping);

			// Alignments and sizes as per https://github.com/ARM-software/abi-aa/blob/320a56971fdcba282b7001cf4b84abb4fd993131/aapcs64/aapcs64.rst#fundamental-data-types
			// Assembler type directives are described in https://sourceware.org/binutils/docs-2.37/as/index.html
			ConfigureTypeMapping<short>	 (".hword", size: 2, alignment: 2);
			ConfigureTypeMapping<ushort> (".hword", size: 2, alignment: 2);
			ConfigureTypeMapping<int>	 (".word",	size: 4, alignment: 4);
			ConfigureTypeMapping<uint>	 (".word",	size: 4, alignment: 4);
			ConfigureTypeMapping<long>	 (".xword", size: 8, alignment: 8);
			ConfigureTypeMapping<ulong>	 (".xword", size: 8, alignment: 8);
			ConfigureTypeMapping<float>	 (".word",	size: 4, alignment: 4);
			ConfigureTypeMapping<double> (".xword", size: 8, alignment: 8);
			ConfigureTypeMapping<nint>	 (".xword", size: 8, alignment: 8);
			ConfigureTypeMapping<nuint>	 (".xword", size: 8, alignment: 8);
			ConfigureTypeMapping<IntPtr> (".xword", size: 8, alignment: 8);
		}
	}
}
