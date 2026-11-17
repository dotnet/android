using System;
using System.Diagnostics.CodeAnalysis;

namespace generator
{
	static class StringExtensions
	{
		/// <summary>
		/// Shortcut for !string.IsNullOrWhiteSpace (s)
		/// </summary>
		public static bool HasValue (this string s) => !string.IsNullOrWhiteSpace (s);

		/// <summary>
		/// Removes the final subset of a delimited string. ("127.0.0.1" -> "127.0.0")
		/// </summary>
		public static string ChompLast (this string s, char separator)
		{
			if (!s.HasValue ())
				return s;

			var index = s.LastIndexOf (separator);

			if (index < 0)
				return string.Empty;

			return s.Substring (0, index);
		}

		/// <summary>
		/// Returns the final subset of a delimited string. ("127.0.0.1" -> "1")
		/// </summary>
		public static string LastSubset (this string s, char separator)
		{
			if (!s.HasValue ())
				return s;

			var index = s.LastIndexOf (separator);

			if (index < 0)
				return s;

			return s.Substring (index + 1);
		}
	}
}
