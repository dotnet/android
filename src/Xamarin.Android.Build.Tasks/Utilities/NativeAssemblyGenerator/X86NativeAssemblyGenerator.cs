using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks
{
	abstract class X86NativeAssemblyGenerator : NativeAssemblyGenerator
	{
		protected override string LineCommentStart => "#";

		protected X86NativeAssemblyGenerator (StreamWriter output, string fileName)
			: base (output, fileName)
		{}

		protected override void ConfigureTypeMappings (Dictionary<Type, NativeType?> mapping)
		{
			base.ConfigureTypeMappings (mapping);

			ConfigureTypeMapping<short>  (".short", size: 2, alignment: 2);
			ConfigureTypeMapping<ushort> (".short", size: 2, alignment: 2);
			ConfigureTypeMapping<int>    (".long",  size: 4, alignment: 4);
			ConfigureTypeMapping<uint>   (".long",  size: 4, alignment: 4);
			ConfigureTypeMapping<float>  (".long",  size: 4, alignment: 4);
		}
	}
}
