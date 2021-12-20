using System;

#if NET
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo (typeof (Java.Interop.JavaTypeParametersAttribute))]

#else   // !NET

namespace Java.Interop
{
	public class JavaTypeParametersAttribute : Attribute
	{
		public JavaTypeParametersAttribute (string [] typeParameters)
		{
			TypeParameters = typeParameters;
		}

		public string [] TypeParameters { get; set; }
	}
}

#endif  // !NET
