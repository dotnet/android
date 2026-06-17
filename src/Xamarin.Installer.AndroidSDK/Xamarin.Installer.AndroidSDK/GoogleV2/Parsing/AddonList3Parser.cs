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
	public class AddonList3Parser : ElementParser
	{
		List<AddonSite> sites;

		public IList<AddonSite> Sites { get; private set; }

		public AddonList3Parser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
			IgnoreNamespaceAttributes = true;
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"site", ParseChildElement_Site}
			};
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			Sites = sites;
		}

		void ParseChildElement_Site (XElement element)
		{
			var asp = new AddonSiteParser (Context, element, Namespaces);
			asp.Parse ();

			if (asp.Type == AddonSiteType.Unknown)
				return;

			if (sites == null)
				sites = new List<AddonSite> ();

			sites.Add (new AddonSite (asp.Type, asp.DisplayName, asp.Url));
		}
	}
}
