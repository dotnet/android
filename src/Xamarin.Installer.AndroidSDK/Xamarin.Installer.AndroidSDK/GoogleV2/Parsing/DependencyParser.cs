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

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class DependencyParser : ElementParser
	{
		string path;
		AndroidRevision minRevision;

		public Dependency Dependency { get; private set; }

		public DependencyParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return new Dictionary<string, Action<XAttribute>> (StringComparer.Ordinal) {
				{"path", ParseAttribute_Path}
			};
		}

		void ParseAttribute_Path (XAttribute attr)
		{
			path = attr.Value;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"min-revision", ParseChildElement_MinRevision}
			};
		}

		void ParseChildElement_MinRevision (XElement element)
		{
			var rp = new RevisionParser (Context, element, Namespaces);
			rp.Parse ();
			minRevision = rp.Revision;
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			if (String.IsNullOrEmpty (path))
				return;
			Dependency = new Dependency (path, minRevision);
		}
	}
}
