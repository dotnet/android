using System.Globalization;

namespace Microsoft.Android.Tasks;

internal static class TypeMapClassName
{
	public static bool TryGetClassName (string name, out string? className)
	{
		className = null;
		int dimensions = 0;
		while (dimensions < name.Length && name [dimensions] == '[') {
			dimensions++;
		}
		if (dimensions > 0) {
			if (dimensions > 255 || dimensions == name.Length) {
				return false;
			}
			// Primitive arrays are valid typemap entries but do not name a Java class to keep.
			if (dimensions == name.Length - 1 && "BCDFIJSZ".IndexOf (name [dimensions]) >= 0) {
				return true;
			}
			if (name [dimensions] != 'L' || name [name.Length - 1] != ';') {
				return false;
			}
			name = name.Substring (dimensions + 1, name.Length - dimensions - 2);
		}
		if (!IsClassName (name)) {
			return false;
		}
		className = name;
		return true;
	}

	internal static bool IsClassName (string name)
	{
		bool first = true;
		for (int i = 0; i < name.Length; i++) {
			if (name [i] == '/') {
				if (first) {
					return false;
				}
				first = true;
				continue;
			}
			var category = CharUnicodeInfo.GetUnicodeCategory (name, i);
			bool start = category == UnicodeCategory.UppercaseLetter ||
				category == UnicodeCategory.LowercaseLetter ||
				category == UnicodeCategory.TitlecaseLetter ||
				category == UnicodeCategory.ModifierLetter ||
				category == UnicodeCategory.OtherLetter ||
				category == UnicodeCategory.LetterNumber ||
				category == UnicodeCategory.CurrencySymbol ||
				category == UnicodeCategory.ConnectorPunctuation;
			if (!start && (first || (category != UnicodeCategory.DecimalDigitNumber &&
				category != UnicodeCategory.NonSpacingMark && category != UnicodeCategory.SpacingCombiningMark))) {
				return false;
			}
			if (char.IsHighSurrogate (name [i])) {
				i++;
			}
			first = false;
		}
		return !first;
	}
}
