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
	class TypeDetailsMavenParser : TypeDetailsParser
	{
		public PackageVendor Vendor { get; set; }

		public TypeDetailsMavenParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{ }

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			Dictionary<string, Action<XElement>> dict = EnsureDictionary (base.GetKnownChildElements ());
			dict["vendor"] = (XElement e) => Vendor = ParseChildElement_Vendor (e);
			return dict;
		}

		protected override AndroidComponentInfo CreateComponentInfo ()
		{
			return new AndroidComponentInfoMaven (Type, Vendor);
		}
	}
}
