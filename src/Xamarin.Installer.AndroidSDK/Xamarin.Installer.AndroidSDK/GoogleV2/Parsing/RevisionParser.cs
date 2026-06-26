//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft Corp. (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class RevisionParser : ElementParser
	{
		int major;
		int minor;
		int micro;
		int preview;

		public AndroidRevision Revision { get; private set; }

		public RevisionParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
			major = minor = micro = preview = -1;
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			if (major < 0) {
				ErrorHandler.Error (Context.CurrentManifestURL, Element, $"Required 'major' version component is either missing or has invalid value ({major})");
				Revision = null;
			} else
				Revision = new AndroidRevision (major, minor, micro, preview);
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"major", (XElement e) => major = ParseValue (e)},
				{"minor", (XElement e) => minor = ParseValue (e)},
				{"micro", (XElement e) => micro = ParseValue (e)},
				{"preview", (XElement e) => preview = ParseValue (e)},
			};
		}

		int ParseValue (XElement element)
		{
			string v = element?.Value?.Trim ();
			if (String.IsNullOrEmpty (v))
				return -1;

			int ret;
			if (!Int32.TryParse (v, out ret)) {
				ErrorHandler.Error (Context.CurrentManifestURL, element, $"Invalid value of element '{element.Name}'. Expected integer larger than 0");
				return -1;
			}

			return ret;
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}
	}
}
