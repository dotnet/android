using System;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop.Tools.Generator
{
	public static class UtilityExtensions
	{
		public static bool In<T> (this T enumeration, params T [] values)
		{
			if (enumeration is null)
				return false;

			foreach (var en in values)
				if (enumeration.Equals (en))
					return true;

			return false;
		}

		public static bool StartsWithAny (this string value, params string [] values)
		{
			foreach (var en in values)
				if (value.StartsWith (en, StringComparison.OrdinalIgnoreCase))
					return true;

			return false;
		}
		
		public static bool HasValue ([NotNullWhen (true)]this string? str) => !string.IsNullOrEmpty (str);
	}
}
