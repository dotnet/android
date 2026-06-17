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
using System.Linq;
using System.Xml.Linq;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class ArchivePatchesParser : ElementParser
	{
		List<ArchivePatch> patches;

		public IList<ArchivePatch> Patches { get; private set; }

		public ArchivePatchesParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"patch", ParseChildElement_Patch}
			};
		}

		void ParseChildElement_Patch (XElement element)
		{
			var ap = new ArchivePatchParser (Context, element, Namespaces);
			ap.Parse ();
			if (ap.Patch == null)
				return;

			if (patches == null)
				patches = new List<ArchivePatch> ();
			patches.Add (ap.Patch);
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			Patches = patches?.AsReadOnly ();
		}
	}
}
