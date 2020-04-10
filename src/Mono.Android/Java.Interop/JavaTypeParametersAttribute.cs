using System;

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

