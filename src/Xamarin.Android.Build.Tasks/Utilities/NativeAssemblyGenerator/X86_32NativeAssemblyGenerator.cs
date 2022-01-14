using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks
{
	class X86_32NativeAssemblyGenerator : X86NativeAssemblyGenerator
	{
		static readonly NativeType pointer = new NativeType {
			Size = 4,
			Alignment = 4,
			Name = ".long",
		};

		public override bool Is64Bit => false;

		public X86_32NativeAssemblyGenerator (StreamWriter output, string fileName)
			: base (output, fileName)
		{}

		protected override NativeType GetPointerType () => pointer;

		protected override void ConfigureTypeMappings (Dictionary<Type, NativeType?> mapping)
		{
			base.ConfigureTypeMappings (mapping);

			// Alignments and sizes as per https://refspecs.linuxbase.org/elf/abi386-4.pdf section 3.2 (Fundamental Types), table 3.1 (Scalar Types)
			// Assembler type directives are described in https://sourceware.org/binutils/docs-2.37/as/index.html
			ConfigureTypeMapping<long>	 (".quad", size: 8, alignment: 4);
			ConfigureTypeMapping<ulong>	 (".quad", size: 8, alignment: 4);
			ConfigureTypeMapping<double> (".quad", size: 8, alignment: 4);
			ConfigureTypeMapping<nint>	 (".long", size: 4, alignment: 4);
			ConfigureTypeMapping<nuint>	 (".long", size: 4, alignment: 4);
			ConfigureTypeMapping<IntPtr> (".long", size: 4, alignment: 4);
		}
	}
}
