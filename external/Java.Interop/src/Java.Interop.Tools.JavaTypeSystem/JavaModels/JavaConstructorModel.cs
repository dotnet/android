using System;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaConstructorModel : JavaMethodModel
	{
		public JavaConstructorModel (string javaName, string javaVisibility, bool javaStatic, JavaTypeModel javaDeclaringType, string deprecated, string jniSignature, bool isSynthetic, bool isBridge)
			: base (javaName, javaVisibility, false, false, javaStatic, "void", javaDeclaringType, deprecated, jniSignature, isSynthetic, isBridge, string.Empty, false, false, false)
		{
		}

		public override string ToString () => $"Constructor: {DeclaringType.FullName}.{Name}";
	}
}
