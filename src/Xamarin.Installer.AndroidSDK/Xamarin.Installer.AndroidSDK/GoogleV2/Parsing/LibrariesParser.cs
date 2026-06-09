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
	class LibrariesParser : ElementParser
	{
		List<PackageLibrary> libraries;

		public IList<PackageLibrary> Libraries { get; private set; }

		public LibrariesParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"library", ParseChildElement_Archive}
			};
		}

		void ParseChildElement_Archive (XElement element)
		{
			var lp = new LibraryParser (Context, element, Namespaces);
			lp.Parse ();
			if (lp.Library == null)
				return;

			if (libraries == null)
				libraries = new List<PackageLibrary> ();
			libraries.Add (lp.Library);
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			Libraries = libraries?.AsReadOnly ();
		}
	}
}
