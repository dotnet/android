using System;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaInterfaceModel : JavaTypeModel
	{
		public JavaInterfaceModel (JavaPackage javaPackage, string javaNestedName, string javaVisibility, string javaDeprecated, bool javaStatic, string jniSignature) :
			base (javaPackage, javaNestedName, javaVisibility, false, false, javaDeprecated, javaStatic, jniSignature)
		{
		}

		public override string ToString () => $"Interface: {FullName}";
	}
}
