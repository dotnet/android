using System;
using System.Collections.Generic;
using System.Linq;
using Java.Interop.Tools.Generator;
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

			var mangled = c.Methods.Where (m => m.IsKotlinNameMangled).ToList ();

			foreach (var method in mangled) {

				// If the method is virtual, mark it as !virtual as it can't be overridden in Java
				if (!method.IsFinal)
					method.IsFinal = true;

				if (method.IsVirtual)
					method.IsVirtual = false;

				FixMethodName (method);
			}

			RemoveCollidingSiblings (c, mangled);
		}

		private static void FixupInterface (InterfaceGen gen)
		{
			var mangled = gen.Methods.Where (m => m.IsKotlinNameMangled).ToList ();

			foreach (var method in mangled)
				FixMethodName (method);

			RemoveCollidingSiblings (gen, mangled);
		}

		// After the rename above, hash-mangled siblings can collide with each
		// other AND with pre-existing non-mangled overloads. Both cases produce
		// CS0111 in the generated code. Until step 2 of dotnet/java-interop#1431
		// projects inline-class params as strongly-typed wrappers, drop the
		// mangled duplicate deterministically and warn so the user can override
		// via Metadata.xml if desired. Non-mangled methods are always kept; only
		// mangled methods are ever removed.
		private static void RemoveCollidingSiblings (GenBase gen, List<Method> renamed)
		{
			if (renamed.Count == 0)
				return;

			foreach (var method in renamed) {
				// A mangled method is always the "lesser" choice compared to a
				// real non-mangled Kotlin API, so drop it whenever ANY non-mangled
				// overload matches — regardless of source order.
				var nonMangledMatch = gen.Methods
					.FirstOrDefault (m => m != method && !m.IsKotlinNameMangled
						&& m.Name == method.Name && m.Matches (method));
				if (nonMangledMatch == null) {
					// Otherwise (only mangled siblings collide), keep the
					// first-declared one and drop later mangled duplicates.
					var earlierMangled = gen.Methods
						.TakeWhile (m => m != method)
						.FirstOrDefault (m => m.Name == method.Name && m.Matches (method));
					if (earlierMangled == null)
						continue;
				}

				Report.LogCodedWarning (0, Report.WarningKotlinNameMangledCollision, method, gen.FullName, method.Name, method.JavaName);
				gen.Methods.Remove (method);
			}
		}

		private static void FixMethodName (Method method)
		{
			// Only run this if it's the default name (ie: not a user's "managedName")
			if (method.Name == StringRocks.MemberToPascalCase (method.JavaName).Replace ('-', '_')) {
				// We want to remove the hyphen and anything afterwards to fix mangled names,
				// but a previous step converted it to an underscore. Remove the final
				// underscore and anything after it.
				var index = method.Name.IndexOf ("_impl", StringComparison.Ordinal);

				if (index >= 0)
					method.Name = method.Name.Substring (0, index);
				else
					method.Name = method.Name.Substring (0, method.Name.Length - 8);
			}
		}
	}
}
