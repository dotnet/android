using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

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

		public static XDocument? LoadXmlDocument (string filename)
		{
			try {
				return XDocument.Load (filename, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
			} catch (XmlException e) {
				Report.Verbose (0, "Exception: {0}", e);
				Report.LogCodedWarning (0, Report.WarningInvalidXmlFile, e, filename, e.Message);
			}

			return null;
		}

		// A case-insensitive Replace doesn't exist in classic .NET Framework.  Loosely based on:
		// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/String.Manipulation.cs
		public static string ReplaceOrdinalIgnoreCase (this string source, string oldValue, string newValue)
		{
			var result = new StringBuilder ();
			var pos = 0;

			while (true) {
				var index = source.IndexOf (oldValue, pos, StringComparison.OrdinalIgnoreCase);

				// Not found, bail
				if (index < 0)
					break;

				// Append the unmodified portion of search space
				result.Append (source.Substring (pos, index));

				// Append the replacement
				result.Append (newValue);

				pos = index + oldValue.Length;
			}

			// Append what remains of the search space, then allocate the new string.
			result.Append (source.Substring (pos));
			return result.ToString ();
		}
	}
}
