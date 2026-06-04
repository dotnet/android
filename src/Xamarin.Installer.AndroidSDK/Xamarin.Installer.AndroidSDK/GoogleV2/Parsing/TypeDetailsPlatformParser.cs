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
	class TypeDetailsPlatformParser : TypeDetailsParser
	{
		protected string ApiLevel { get; private set; }
		protected string CodeName { get; private set; }
		protected string LayoutLibApi { get; private set; }
		protected string BaseExtension { get; private set; }
		protected string ExtensionLevel { get; private set; }

		public TypeDetailsPlatformParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			Dictionary<string, Action<XElement>> dict = EnsureDictionary (base.GetKnownChildElements ());
			dict["api-level"] = (XElement e) => ApiLevel = e.GetElementValue ();
			dict["codename"] = (XElement e) => CodeName = e.GetElementValue ();
			dict["layoutlib"] = ParseChildElement_LayoutLib;

			// https://dl.google.com/android/repository/repository2-3.xml [304:4]
			dict["base-extension"] = (XElement e) => BaseExtension = e.GetElementValue ();

			// https://dl.google.com/android/repository/repository2-3.xml [304:4]
			dict["extension-level"] = (XElement e) => ExtensionLevel = e.GetElementValue ();

			return dict;
		}

		protected override AndroidComponentInfo CreateComponentInfo ()
		{
			// Android SDK component is considered "experimental" / "not stable" when codename is not empty & "base-extension" is not empty
			// See android-Tiramisu for example
			return new AndroidComponentInfoPlatform (Type, ApiLevel, CodeName, LayoutLibApi) {
				Preview = !string.IsNullOrEmpty (CodeName) && !string.IsNullOrEmpty (BaseExtension) && !string.IsNullOrEmpty (ExtensionLevel),
			};
		}

		protected override bool Validate ()
		{
			bool ret = base.Validate () && ValidateApiLevel (ApiLevel);
			
			if (String.IsNullOrEmpty (LayoutLibApi)) {
				ErrorHandler.Error (Context.CurrentManifestURL, Element, $"Missing attribute 'layoutlib' on element {Element.Name}");
				ret = false;
			}

			return ret;
		}

		void ParseChildElement_LayoutLib (XElement element)
		{
			XAttribute api = element.GetAttribute ("api");
			LayoutLibApi = api?.Value?.Trim ();
		}
	}
}
