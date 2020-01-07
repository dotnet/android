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
			foreach (var i in gens.OfType<InterfaceGen> ())
				FixupInterface (i);
		}

		private static void FixupClass (ClassGen c)
		{
			// Kotlin mangles the name of some methods to make them
			// inaccessible from Java, like `add-impl` and `add-V5j3Lk8`.
			// We need to generate C# compatible names as well as prevent overriding 
			// them as we cannot generate JCW for them.

			foreach (var method in c.Methods.Where (m => m.IsKotlinNameMangled)) {

				// If the method is virtual, mark it as !virtual as it can't be overridden in Java
				if (!method.IsFinal)
					method.IsFinal = true;

				if (method.IsVirtual)
					method.IsVirtual = false;

				FixMethodName (method);
			}
		}

		private static void FixupInterface (InterfaceGen gen)
		{
			foreach (var method in gen.Methods.Where (m => m.IsKotlinNameMangled))
				FixMethodName (method);
		}

		private static void FixMethodName (Method method)
		{
			// Only run this if it's the default name (ie: not a user's "managedName")
			if (method.Name == StringRocks.MemberToPascalCase (method.JavaName).Replace ('-', '_')) {
				// We want to remove the hyphen and anything afterwards to fix mangled names,
				// but a previous step converted it to an underscore. Remove the final
				// underscore and anything after it.
				var index = method.Name.IndexOf ("_impl");

				if (index >= 0)
					method.Name = method.Name.Substring (0, index);
				else
					method.Name = method.Name.Substring (0, method.Name.Length - 8);
			}
		}
	}
}
