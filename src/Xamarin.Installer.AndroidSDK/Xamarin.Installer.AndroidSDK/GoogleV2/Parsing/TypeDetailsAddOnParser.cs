//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class TypeDetailsAddOnParser : TypeDetailsParser
	{
		public string ApiLevel { get; private set; }
		public string CodeName { get; private set; }
		public PackageVendor Vendor { get; private set; }
		public PackageTag Tag { get; private set; }
		public IList<PackageLibrary> Libraries { get; private set; }

		public TypeDetailsAddOnParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			Dictionary<string, Action<XElement>> dict = EnsureDictionary (base.GetKnownChildElements ());
			dict["api-level"] = (XElement e) => ApiLevel = e.GetElementValue ();
			dict["codename"] = (XElement e) => CodeName = e.GetElementValue ();
			dict["libraries"] = ParseChildElement_Libraries;
			dict["tag"] = (XElement e) => Tag = ParseChildElement_Tag (e);
			dict["vendor"] = (XElement e) => Vendor = ParseChildElement_Vendor (e);

			// https://dl.google.com/android/repository/addon2-3.xml [605:4]
			dict["base-extension"] = (XElement e) => {}; // ignore
			return dict;
		}

		void ParseChildElement_Libraries (XElement element)
		{
			var lp = new LibrariesParser (Context, element, Namespaces);
			lp.Parse ();
			if (lp.Libraries == null || lp.Libraries.Count == 0)
				return;
			Libraries = lp.Libraries;
		}

		protected override AndroidComponentInfo CreateComponentInfo ()
		{
			return new AndroidComponentInfoAddon (Type, ApiLevel, CodeName, Tag, Vendor, Libraries);
		}

		protected override bool Validate ()
		{
			return base.Validate () && ValidateApiLevel (ApiLevel);
		}
	}
}
