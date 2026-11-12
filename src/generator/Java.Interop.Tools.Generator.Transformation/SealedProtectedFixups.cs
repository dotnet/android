using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;

namespace Java.Interop.Tools.Generator.Transformation
{
	public static class SealedProtectedFixups
	{
		public static void Fixup (List<GenBase> gens)
		{
			foreach (var c in gens.OfType<ClassGen> ().Where (c => c.IsFinal)) {
				foreach (var m in c.Methods.Where (m => m.Visibility == "protected" && !m.IsOverride))
					m.Visibility = "private";

				foreach (var p in c.Properties.Where (p => p.Getter?.Visibility == "protected" && !p.Getter.IsOverride))
					p.Getter.Visibility = "private";

				foreach (var p in c.Properties.Where (p => p.Setter?.Visibility == "protected" && !p.Setter.IsOverride))
					p.Setter.Visibility = "private";

				foreach (var p in c.Fields.Where (f => f.Visibility == "protected"))
					p.Visibility = "private";

				foreach (var p in c.NestedTypes.Where (t => t.Visibility == "protected"))
					p.Visibility = "private";
			}
		}
	}
}
