//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class LicenseParser : ElementParser
	{
		string id;
		string type;

		public License License { get; private set; }

		public LicenseParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return new Dictionary<string, Action<XAttribute>> (StringComparer.Ordinal) {
				{"id", ParseAttribute_Id},
				{"type", ParseAttribute_Type}
			};
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return null;
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			if (String.IsNullOrEmpty (id)) {
				ErrorHandler.Error (Context.CurrentManifestURL, Element, $"License element is missing the required 'id' attribute");
				return;
			}
			License = new License (id, type, Element.Value ?? String.Empty);
		}

		void ParseAttribute_Id (XAttribute attr)
		{
			id = attr?.Value ?? String.Empty;
		}

		void ParseAttribute_Type (XAttribute attr)
		{
			type = attr?.Value ?? String.Empty;
		}
	}
}
