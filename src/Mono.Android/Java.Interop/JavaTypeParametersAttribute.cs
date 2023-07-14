using System;

#if NET
using System.Runtime.CompilerServices;

// PublicApiAnalyzers doesn't like TypeForwards
#pragma warning disable RS0016 // Symbol is not part of the declared API
[assembly: TypeForwardedTo (typeof (Java.Interop.JavaTypeParametersAttribute))]
#pragma warning restore RS0016 // Symbol is not part of the declared API

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
