using System;
using System.Collections.Generic;
using System.Linq;

namespace Java.Interop.Tools.JavaTypeSystem
{
	static class CollectionExtensions
	{
		public static bool ContainsAny<T> (this ICollection<T> collection, params T[] values)
		{
			foreach (var v in values)
				if (collection.Contains (v))
					return true;

			return false;
		}

		public static IEnumerable<T> WhereNotNull<T> (this IEnumerable<T?> values) where T : class
		{
			return values.Where (p => p != null).Cast<T> ();
		}
	}
}
