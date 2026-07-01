using System;
using System.Linq;

namespace MonoDroid.Utils {

	static class StringRocks {

		public static string ToLowerCase (string value)
		{
			if (string.IsNullOrEmpty (value))
				return value;
			string[] parts = value.Split ('.');
			for (int i = 0; i < parts.Length; ++i) {
				parts [i] = parts [i].ToLowerInvariant ();
			}
			return string.Join (".", parts);
		}

		public static string MemberToPascalCase (string value)
		{
			if (string.IsNullOrEmpty (value))
				return value;

			if (value.Contains ("."))
				throw new NotSupportedException ("Methods cannot contain '.'.");

			return ToPascalCasePart (value, 1);
		}

		public static string TypeToPascalCase (string value)
		{
			return ToPascalCase (value, 1);
		}

		public static string PackageToPascalCase (string value)
		{
			return ToPascalCase (value, 2);
		}

		static string ToPascalCase (string value, int minLength)
		{
			if (string.IsNullOrEmpty (value))
				return value;

			string[] parts = value.Split ('.');
			for (int i = 0; i < parts.Length; ++i) {
				parts [i] = ToPascalCasePart (parts [i], minLength);
			}
			return string.Join (".", parts);
		}

		static string ToPascalCasePart (string value, int minLength)
		{
			return value.Length <= minLength
				? value.ToUpperInvariant ()
				: char.ToUpperInvariant (value [0]) + value.Substring (1);
		}
	}
}
