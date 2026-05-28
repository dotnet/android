//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Marek Habersack
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class PackageTagParser : ElementParser
	{
		string id;
		string display;

		public PackageTag Tag { get; private set; }

		public PackageTagParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"id", (XElement e) => id = e.Value?.Trim ()},
				{"display", (XElement e) => display = e.Value?.Trim ()}
			};
		}

		protected override void Parsed ()
		{
			base.Parsed ();

			if (String.IsNullOrEmpty (id)) {
				ErrorHandler.Error (Context.CurrentManifestURL, Element, $"Missing child element 'id' on element {Element.Name}");
				return;
			}

			Tag = new PackageTag (id, display);
		}
	}
}
