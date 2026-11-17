using System;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaInterfaceModel : JavaTypeModel
	{
		public JavaInterfaceModel (JavaPackage javaPackage, string javaNestedName, string javaVisibility, string javaDeprecated, bool javaStatic, string jniSignature, string annotatedVisibility) :
			base (javaPackage, javaNestedName, javaVisibility, false, false, javaDeprecated, javaStatic, jniSignature, annotatedVisibility)
		{
		}

		public override string ToString () => $"Interface: {FullName}";
	}
}
