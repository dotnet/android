using System;
using System.Collections.Generic;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public abstract class JavaMemberModel : IJavaResolvable
	{
		public string Name { get; }
		public bool IsStatic { get; }
		public JavaTypeModel DeclaringType { get; }
		public bool IsFinal { get; }
		public string Visibility { get; }
		public string Deprecated { get; }
		public string JniSignature { get; }

		public Dictionary<string, string> PropertyBag { get; } = new Dictionary<string, string> ();

		public JavaMemberModel (string name, bool isStatic, bool isFinal, string visibility, JavaTypeModel declaringType, string deprecated, string jniSignature)
		{
			Name = name;
			IsStatic = isStatic;
			IsFinal = isFinal;
			Visibility = visibility;
			DeclaringType = declaringType;
			Deprecated = deprecated;
			JniSignature = jniSignature;
		}

		public abstract void Resolve (JavaTypeCollection types, ICollection<JavaUnresolvableModel> unresolvables);
	}
}
