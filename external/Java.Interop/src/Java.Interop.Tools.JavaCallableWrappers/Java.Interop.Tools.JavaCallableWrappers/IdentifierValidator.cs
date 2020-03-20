using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Java.Interop.Tools.JavaCallableWrappers
{
	public static class IdentifierValidator
	{
		// https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/lexical-structure#identifiers
		private const string FormattingCharacter = @"\p{Cf}";
		private const string ConnectingCharacter = @"\p{Pc}";
		private const string DecimalDigitCharacter = @"\p{Nd}";
		private const string CombiningCharacter = @"\p{Mn}\p{Mc}";
		private const string LetterCharacter = @"\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}";

		private const string IdentifierPartCharacter = LetterCharacter +
			DecimalDigitCharacter +
			ConnectingCharacter +
			CombiningCharacter +
			FormattingCharacter;

		private const string IdentifierStartCharacter = "(" + LetterCharacter + "_)";

		private const string Identifier = IdentifierStartCharacter + "(" + IdentifierPartCharacter + ")";

		static Regex IsValidIdentifierRegex = new Regex ($"^[{IdentifierStartCharacter}][{IdentifierPartCharacter}]*$", RegexOptions.Compiled);

		// We use [^ ...] to detect any character that is NOT a match.
		static Regex validIdentifier = new Regex ($"[^{Identifier}]", RegexOptions.Compiled);

		public static string CreateValidIdentifier (string identifier, bool useEncodedReplacements = false)
		{
			if (string.IsNullOrWhiteSpace (identifier)) return string.Empty;

			var normalizedIdentifier = identifier.Normalize ();

			if (useEncodedReplacements)
				return validIdentifier.Replace (normalizedIdentifier, new MatchEvaluator (EncodeReplacement));

			return validIdentifier.Replace (normalizedIdentifier, "_");
		}

		public static bool IsValidIdentifier (string identifier)
		{
			return IsValidIdentifierRegex.IsMatch (identifier);
		}

		// Makes uglier but unique identifiers by encoding each invalid character with its character value
		static string EncodeReplacement (Match match) => $"_x{(ushort) match.Value [0]}_";
	}
}
