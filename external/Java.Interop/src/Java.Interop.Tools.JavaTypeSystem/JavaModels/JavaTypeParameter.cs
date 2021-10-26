using System;
using System.Collections.Generic;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaTypeParameter : IJavaResolvable
	{
		public string Name { get; set; }

		public JavaTypeParameters Parent { get; }

		public string? ExtendedJniClassBound { get; set; }
		public string? ExtendedClassBound { get; set; }
		public string? ExtendedInterfaceBounds { get; set; }
		public string? ExtendedJniInterfaceBounds { get; set; }

		public List<JavaGenericConstraint> GenericConstraints { get; } = new List<JavaGenericConstraint> ();

		public JavaTypeParameter (string name, JavaTypeParameters parent)
		{
			Name = name;
			Parent = parent;
		}

		public void Resolve (JavaTypeCollection types, ICollection<JavaUnresolvableModel> unresolvables)
		{
			// TODO: Resolve generic constraints
			//var type_parameters = GetApplicableTypeParameters ().ToArray ();
		}

		public override string ToString () => Name ?? "";
	}
}
