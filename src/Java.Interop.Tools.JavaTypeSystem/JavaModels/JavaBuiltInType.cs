using System;
using System.Collections.Generic;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	// Represents a Java built-in type like 'int' or 'float'
	public class JavaBuiltInType : JavaTypeModel
	{
		public JavaBuiltInType (string name) : base (new JavaPackage ("", "", null), name, "public", false, true, "not deprecated", false, "", "") { }

		public override void Resolve (JavaTypeCollection types, ICollection<JavaUnresolvableModel> unresolvables)
		{
			throw new NotImplementedException ();
		}
	}
}
