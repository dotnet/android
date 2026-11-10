using System;
using System.Globalization;

namespace MonoDroid.Utils
{
	static class LowercaseComparison
	{
		public static StringComparison PrepareInvariant (ref string value)
		{
			// Callers compare against lowercase ASCII literals; non-ASCII input
			// must retain the original invariant lowercasing.
			for (int i = 0; i < value.Length; i++) {
				if (value [i] > 0x7f) {
					value = value.ToLowerInvariant ();
					return StringComparison.Ordinal;
				}
			}

			return StringComparison.OrdinalIgnoreCase;
		}

		public static StringComparison PrepareCurrentCulture (ref string value)
		{
			// Other cultures can lowercase even ASCII differently.
			if (ReferenceEquals (CultureInfo.CurrentCulture, CultureInfo.InvariantCulture))
				return PrepareInvariant (ref value);

			value = value.ToLower ();
			return StringComparison.Ordinal;
		}
	}
}
