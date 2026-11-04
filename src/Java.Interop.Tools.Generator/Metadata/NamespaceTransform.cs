using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace Java.Interop.Tools.Generator
{
	public class NamespaceTransform
	{
		public string OldValue { get; }
		public string NewValue { get; }
		public bool IsStartsWith { get; }
		public bool IsEndsWith { get; }

		public NamespaceTransform (string oldValue, string newValue)
		{
			OldValue = oldValue;
			NewValue = newValue;

			if (OldValue.EndsWith (".", StringComparison.Ordinal)) {
				IsStartsWith = true;
				OldValue = OldValue.Substring (0, OldValue.Length - 1);
			}

			if (OldValue.StartsWith (".", StringComparison.Ordinal)) {
				IsEndsWith = true;
				OldValue = OldValue.Substring (1);
			}
		}

		public string ApplyInternal (string value)
		{
			string result;

			while (true) {
				result = ApplyInternal (value);

				if (result == value)
					return result;

				value = result;
			}
		}

		public string Apply (string value)
		{
			// Handle a "starts with" and "ends with" transform
			if (IsStartsWith && IsEndsWith) {
				if (value.Equals (OldValue, StringComparison.OrdinalIgnoreCase))
					return NewValue;

				// Don't let this fall through
				return value;
			}

			// Handle a "starts with" transform
			if (IsStartsWith) {
				if (value.StartsWith (OldValue, StringComparison.OrdinalIgnoreCase))
					return (NewValue + value.Substring (OldValue.Length)).TrimStart ('.');

				return value;
			}

			// Handle an "ends with" transform
			if (IsEndsWith) {
				if (value.EndsWith (OldValue, StringComparison.OrdinalIgnoreCase))
					return (value.Substring (0, value.Length - OldValue.Length) + NewValue).TrimEnd ('.');

				return value;
			}

			// Handle an "anywhere" transform
			var value_tokens = value.Split ('.');
			var match_tokens = OldValue.Split ('.');

			var results = new List<string> ();

			for (var i = 0; i < value_tokens.Length; i++) {
				if (AtMatch (value_tokens, i, match_tokens, 0)) {
					if (NewValue.HasValue ())
						results.Add (NewValue);

					i += match_tokens.Length - 1;
				} else {
					results.Add (value_tokens [i]);
				}
			}

			return string.Join (".", results);
		}

		public static bool TryParse (XElement element, [NotNullWhen (true)] out NamespaceTransform? transform)
		{
			var source = element.XGetAttribute ("source");
			var replacement = element.XGetAttribute ("replacement");

			if (!source.HasValue () || replacement is null) {
				Report.LogCodedWarning (0, Report.WarningInvalidNamespaceTransform, null, element, element.ToString ());
				transform = null;
				return false;
			}

			transform = new NamespaceTransform (source, replacement);
			return true;
		}

		private bool AtMatch (string [] valueTokens, int valueIndex, string [] matchTokens, int matchIndex)
		{
			if (matchIndex >= matchTokens.Length)
				return true;

			if (valueIndex >= valueTokens.Length)
				return false;

			if (string.Compare (valueTokens [valueIndex], matchTokens [matchIndex], StringComparison.OrdinalIgnoreCase) == 0)
				return AtMatch (valueTokens, valueIndex + 1, matchTokens, matchIndex + 1);

			return false;
		}
	}
}

