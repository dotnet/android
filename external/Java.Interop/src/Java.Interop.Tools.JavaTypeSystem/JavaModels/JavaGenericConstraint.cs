using System;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaGenericConstraint
	{
		public string Type { get; }

		public JavaGenericConstraint (string type) => Type = type;
	}
}
