using System;
using System.Collections.Generic;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public interface IJavaResolvable
	{
		void Resolve (JavaTypeCollection types, ICollection<JavaUnresolvableModel> unresolvables);
	}
}
