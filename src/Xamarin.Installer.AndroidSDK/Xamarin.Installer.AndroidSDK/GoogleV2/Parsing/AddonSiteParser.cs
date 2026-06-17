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

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class AddonSiteParser : ElementParser
	{
		static readonly Dictionary<string, AddonSiteType> siteTypeMap = new Dictionary<string, AddonSiteType> (StringComparer.Ordinal) {
			{"sdk:sysImgSiteType", AddonSiteType.SystemImage},
			{"sdk:addonSiteType", AddonSiteType.Addon}
		};

		public AddonSiteType Type { get; private set; } = AddonSiteType.Unknown;
		public string DisplayName { get; private set; } = "Unnamed";
		public Uri Url { get; private set; }

		public AddonSiteParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return new Dictionary<string, Action<XAttribute>> (StringComparer.Ordinal) {
				{GetAttributeName ("xsi", "type"), ParseAttribute_Type}
			};
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"displayName", ParseChildElement_DisplayName},
				{"url", ParseChildElement_Url}
			};
		}

		void ParseChildElement_DisplayName (XElement element)
		{
			DisplayName = element.Value ?? "Unnamed";
		}

		void ParseChildElement_Url (XElement element)
		{
			Uri url;

			if (!Uri.TryCreate (element.Value?.Trim (), UriKind.RelativeOrAbsolute, out url)) {
				ErrorHandler.Error (Context.CurrentManifestURL, Element, $"The '{element.Name.LocalName}' element contains invalid URL: '{element.Value}'");
				return;
			}

			Url = EnsureAbsoluteUrl (url);
		}

		void ParseAttribute_Type (XAttribute attr)
		{
			AddonSiteType type;
			if (!siteTypeMap.TryGetValue (attr.Value, out type)) {
				ErrorHandler.Warning (Context.CurrentManifestURL, Element, $"'xsi:type' attribute with value '{attr.Value}' does not map to a known site type");
				return;
			}

			Type = type;
		}

		protected override void Parsed ()
		{
			base.Parsed ();

			if (Type == AddonSiteType.Unknown) {
				ErrorHandler.Warning (Context.CurrentManifestURL, Element, $"Unknown site type for element '{Element.Name.GetFullName ()}', site will be ignored");
				Url = null;
				DisplayName = null;
				return;
			}

			if (Url == null) {
				Type = AddonSiteType.Unknown;
				ErrorHandler.Error (Context.CurrentManifestURL, Element, $"Element '{Element.Name.GetFullName ()}' has a valid type but no URL, site will be ignored");
			}
		}
	}
}
