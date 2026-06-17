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
	class LibraryParser : ElementParser
	{
		string localJarPath;
		string name;
		string description;

		public PackageLibrary Library { get; private set; }

		public LibraryParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return new Dictionary<string, Action<XAttribute>> (StringComparer.Ordinal) {
				{"localJarPath", (XAttribute attribute) => localJarPath = attribute.Value ?? String.Empty },
				{"name", (XAttribute attribute) => name = attribute.Value ?? String.Empty }
			};
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"description", (XElement element) => description = element.Value ?? String.Empty },
			};
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			Library = new PackageLibrary {
				LocalJarPath = localJarPath,
				Name = name,
				Description = description
			};
		}
	}
}
