using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;
using MonoDroid.Utils;

namespace Java.Interop.Tools.Generator.Transformation
{
	public static class KotlinFixups
	{
		public static void Fixup (List<GenBase> gens)
		{
			foreach (var c in gens.OfType<ClassGen> ())
				FixupClass (c);
		}

		private static void FixupClass (ClassGen c)
		{
			// Kotlin mangles the name of some methods to make them
			// inaccessible from Java, like `add-impl` and `add-V5j3Lk8`.
			// We need to generate C# compatible names as well as prevent overriding 
			// them as we cannot generate JCW for them.
			var invalid_methods = c.Methods.Where (m => m.IsKotlinNameMangled).ToList ();

			foreach (var method in invalid_methods) {

				// If the method is virtual, mark it as !virtual as it can't be overridden in Java
				if (!method.IsFinal)
					method.IsFinal = true;

				if (method.IsVirtual)
					method.IsVirtual = false;

				// Only run this if it's the default name (ie: not a user's "managedName")
				if (method.Name == StringRocks.MemberToPascalCase (method.JavaName).Replace ('-', '_')) {
					// We want to remove the hyphen and anything afterwards to fix mangled names,
					// but a previous step converted it to an underscore. Remove the final
					// underscore and anything after it.
					var index = method.Name.LastIndexOf ('_');

					method.Name = method.Name.Substring (0, index);
				}
			}
		}
	}
}
