using System;

#if NET

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method,
		AllowMultiple=false)]
	public sealed class JavaTypeParametersAttribute : Attribute
	{
		public JavaTypeParametersAttribute (string [] typeParameters)
		{
			TypeParameters = typeParameters;
		}

		public string [] TypeParameters { get; }
	}
}

#endif  // NET
