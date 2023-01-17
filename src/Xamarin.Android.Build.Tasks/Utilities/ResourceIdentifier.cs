using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class ResourceIdentifier {

		//https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/lexical-structure#identifiers
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

		// We use [^ ...] to detect any character that is NOT a match.
		static Regex validIdentifier = new Regex ($"[^{Identifier}]", RegexOptions.Compiled);

		public static string CreateValidIdentifier (string identifier)
		{
			if (String.IsNullOrWhiteSpace (identifier)) return string.Empty;

			var normalizedIdentifier = identifier.Normalize ();

			string result = validIdentifier.Replace (normalizedIdentifier, "_");

			return result;
		}

		internal static string GetResourceName (string type, string name, Dictionary<string, string> map, TaskLoggingHelper log)
		{
			string mappedValue;
			string key = string.Format ("{0}{1}{2}", type, Path.DirectorySeparatorChar, name).ToLowerInvariant ();

			if (map.TryGetValue (key, out mappedValue)) {
				log.LogDebugMessage ("  - Remapping resource: {0}.{1} -> {2}", type, name, mappedValue);
				return ResourceIdentifier.CreateValidIdentifier (mappedValue.Substring (mappedValue.LastIndexOf (Path.DirectorySeparatorChar) + 1));
			}

			log.LogDebugMessage ("  - Not remapping resource: {0}.{1}", type, name);

			return ResourceIdentifier.CreateValidIdentifier (name);
		}
	}
}
