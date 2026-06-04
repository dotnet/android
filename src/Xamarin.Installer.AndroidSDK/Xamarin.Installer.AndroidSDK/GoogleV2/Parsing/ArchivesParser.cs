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
using System.Linq;
using System.Xml.Linq;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class ArchivesParser : ElementParser
	{
		List<Archive> archives;

		public IList<Archive> Archives { get; private set; }

		public ArchivesParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"archive", ParseChildElement_Archive}
			};
		}

		void ParseChildElement_Archive (XElement element)
		{
			var ap = new ArchiveParser (Context, element, Namespaces);
			ap.Parse ();
			if (ap.Archive == null)
				return;

			if (archives == null)
				archives = new List<Archive> ();
			archives.Add (ap.Archive);
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			Archives = archives?.AsReadOnly ();
		}
	}
}
