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
	class ArchivePatchParser : ElementParser
	{
		AndroidRevision basedOn;

		public ArchivePatch Patch { get; private set; }

		public ArchivePatchParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"based-on", ParseChildElement_BasedOn},

				// The three elements below are special treatment, done in Parsed()
				{"size", (XElement) => {}},
				{"checksum", (XElement) => {}},
				{"url", (XElement) => {}},
			};
		}

		void ParseChildElement_BasedOn (XElement element)
		{
			var rp = new RevisionParser (Context, element, Namespaces);
			rp.Parse ();
			basedOn = rp.Revision;
		}

		protected override void Parsed ()
		{
			base.Parsed ();

			var ame = new ArchiveMetadataExtractor (Element, Context);
			Patch = new ArchivePatch {
				BasedOn = basedOn,
				Checksum = ame.Checksum,
				ChecksumType = ame.ChecksumType,
				Size = ame.Size,
				Url = ame.Url
			};
		}
	}
}
