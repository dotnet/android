using System;
using System.Diagnostics.CodeAnalysis;
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
	}
}
