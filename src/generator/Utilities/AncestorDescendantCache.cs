using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoDroid.Generation.Utilities
{
	// Finding all descendants of a type is expensive, so we cache the results here
	public class AncestorDescendantCache
	{
		readonly List<GenBase> gens;
		readonly Dictionary<GenBase, IEnumerable<GenBase>> cache = new Dictionary<GenBase, IEnumerable<GenBase>> ();

		public AncestorDescendantCache (List<GenBase> gens)
		{
			this.gens = gens;
		}

		public IEnumerable<GenBase> GetAncestorsAndDescendants (GenBase gen)
		{
			if (cache.TryGetValue (gen, out var value))
				return value;

			var new_value = GetAncestors (gen).Concat (GetDescendants (gen)).ToList ();

			cache [gen] = new_value;

			return new_value;
		}

		IEnumerable<GenBase> GetAncestors (GenBase gen)
		{
			for (var g = gen.BaseGen; g != null; g = g.BaseGen)
				yield return g;
		}

		IEnumerable<GenBase> GetDescendants (GenBase gen)
		{
			foreach (var directDescendants in gens.Where (x => x.BaseGen == gen)) {
				yield return directDescendants;

				foreach (var indirectDescendants in GetDescendants (directDescendants))
					yield return indirectDescendants;
			}
		}
	}
}
