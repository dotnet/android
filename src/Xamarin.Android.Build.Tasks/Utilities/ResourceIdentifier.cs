using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

		//https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/lexical-structure#keywords
		private static readonly HashSet<string> _keywords = new HashSet<string> () {
			  "abstract" , "as"       , "base"       , "bool"      , "break"
			, "byte"     , "case"     , "catch"      , "char"      , "checked"
			, "class"    , "const"    , "continue"   , "decimal"   , "default"
			, "delegate" , "do"       , "double"     , "else"      , "enum"
			, "event"    , "explicit" , "extern"     , "false"     , "finally"
			, "fixed"    , "float"    , "for"        , "foreach"   , "goto"
			, "if"       , "implicit" , "in"         , "int"       , "interface"
			, "internal" , "is"       , "lock"       , "long"      , "namespace"
			, "new"      , "null"     , "object"     , "operator"  , "out"
			, "override" , "params"   , "private"    , "protected" , "public"
			, "readonly" , "ref"      , "return"     , "sbyte"     , "sealed"
			, "short"    , "sizeof"   , "stackalloc" , "static"    , "string"
			, "struct"   , "switch"   , "this"       , "throw"     , "true"
			, "try"      , "typeof"   , "uint"       , "ulong"     , "unchecked"
			, "unsafe"   , "ushort"   , "using"      , "virtual"   , "void"
			, "volatile" , "while",
		};

		// We use [^ ...] to detect any character that is NOT a match.
		static Regex validIdentifier = new Regex ($"[^{Identifier}]", RegexOptions.Compiled);

		public static string CreateValidIdentifier (string identifier)
		{
			if (String.IsNullOrWhiteSpace (identifier)) return string.Empty;

			var normalizedIdentifier = identifier.Normalize ();

			string result = validIdentifier.Replace (normalizedIdentifier, "_");

			if (_keywords.Contains (result, StringComparer.Ordinal))
				return $"@{result}";
			return result;
		}
	}
}