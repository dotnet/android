using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks
{
	class X86_64NativeAssemblyGenerator : X86NativeAssemblyGenerator
	{
		static readonly NativeType pointer = new NativeType {
			Size = 8,
			Alignment = 8,
			Name = ".quad",
		};

		public override bool Is64Bit => true;

		public X86_64NativeAssemblyGenerator (StreamWriter output, string fileName)
			: base (output, fileName)
		{}

		protected override NativeType GetPointerType () => pointer;

		protected override void ConfigureTypeMappings (Dictionary<Type, NativeType?> mapping)
		{
			base.ConfigureTypeMappings (mapping);

			// Alignments and sizes as per https://refspecs.linuxbase.org/elf/x86_64-abi-0.95.pdf section 3.1.2 (Fundamental Types), table 3.1 (Scalar Types)
			// Assembler type directives are described in https://sourceware.org/binutils/docs-2.37/as/index.html
			ConfigureTypeMapping<long>	 (".quad", size: 8, alignment: 8);
			ConfigureTypeMapping<ulong>	 (".quad", size: 8, alignment: 8);
			ConfigureTypeMapping<double> (".quad", size: 8, alignment: 8);
			ConfigureTypeMapping<nint>	 (".quad", size: 8, alignment: 8);
			ConfigureTypeMapping<nuint>	 (".quad", size: 8, alignment: 8);
			ConfigureTypeMapping<IntPtr> (".quad", size: 8, alignment: 8);
		}
	}
}
