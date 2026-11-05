using System;
using System.Xml;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace Java.Interop.Tools.Generator
{
	public class ApiXmlDocument
	{
		public XDocument ApiDocument { get; }
		public string ApiLevel { get; }
		public int ProductVersion { get; }

		public string? ApiSource => ApiDocument.Root?.XGetAttribute ("api-source");

		public ApiXmlDocument (XDocument document, string apiLevel, int productVersion)
		{
			ApiDocument = document;
			ApiLevel = apiLevel;
			ProductVersion = productVersion;
		}

		public static ApiXmlDocument? Load (string filename, string apiLevel, int productVersion)
		{
			if (UtilityExtensions.LoadXmlDocument (filename) is XDocument doc)
				return new ApiXmlDocument (doc, apiLevel, productVersion);

			return null;
		}

		public void ApplyFixupFile (string filename)
		{
			if (FixupXmlDocument.Load (filename) is FixupXmlDocument fixup)
				ApplyFixupFile (fixup);
		}

		public void ApplyFixupFile (FixupXmlDocument fixup)
		{
			try {
				fixup.Apply (this, ApiLevel, ProductVersion);
			} catch (XmlException ex) {
				// BG4200
				Report.LogCodedErrorAndExit (Report.ErrorFailedToProcessMetadata, null, fixup.FixupDocument, ex.Message);
			}
		}
	}
}
