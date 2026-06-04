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
	class TypeDetailsSourceParser : TypeDetailsParser
	{
		protected string ApiLevel { get; private set; }
		protected string CodeName { get; private set; }

		public TypeDetailsSourceParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			Dictionary<string, Action<XElement>> dict = EnsureDictionary (base.GetKnownChildElements ());
			dict["api-level"] = (XElement e) => ApiLevel = e.GetElementValue ();
			dict["codename"] = (XElement e) => CodeName = e.GetElementValue ();

			// https://dl.google.com/android/repository/repository2-3.xml [1200:4]
			dict["base-extension"] = (XElement e) => {}; // ignore
			dict["extension-level"] = (XElement e) => { }; // ignore

			return dict;
		}

		protected override AndroidComponentInfo CreateComponentInfo ()
		{
			return new AndroidComponentInfoSource (Type, ApiLevel, CodeName);
		}

		protected override bool Validate ()
		{
			return base.Validate () && ValidateApiLevel (ApiLevel);
		}
	}
}
