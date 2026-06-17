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
	class TypeDetailsSystemImageParser : TypeDetailsParser
	{
		protected AndroidSystemImageAbi? Abi { get; private set; }
		protected string AbiName { get; private set; }
		protected string ApiLevel { get; private set; }
		protected string CodeName { get; private set; }
		protected PackageTag Tag { get; private set; }
		protected PackageVendor Vendor { get; private set; }

		public TypeDetailsSystemImageParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			Dictionary<string, Action<XElement>> dict = EnsureDictionary (base.GetKnownChildElements ());
			dict["abi"] = ParseChildElement_ABI;
			dict["api-level"] = (XElement e) => ApiLevel = e.GetElementValue ();
			dict["codename"] = (XElement e) => CodeName = e.GetElementValue ();
			dict["tag"] = (XElement e) => Tag = ParseChildElement_Tag (e);
			dict["vendor"] = (XElement e) => Vendor = ParseChildElement_Vendor (e);

			// https://dl.google.com/android/repository/sys-img/android/sys-img2-3.xml [702:4]
			dict["base-extension"] = (XElement e) => {}; // ignore

			return dict;
		}

		protected override AndroidComponentInfo CreateComponentInfo ()
		{
			return new AndroidComponentInfoSystemImage (Type, Abi.Value, AbiName, ApiLevel, CodeName, Tag, Vendor);
		}

		void ParseChildElement_ABI (XElement element)
		{
			string abiName = element.Value?.Trim ();

			if (String.IsNullOrEmpty (abiName))
				return;

			AbiName = abiName;
			AndroidSystemImageAbi abi = AndroidComponentInfoSystemImage.DefaultAbi;
			try {
				abi = AndroidUtilities.StringToAbi (abiName);
			} catch (ArgumentOutOfRangeException) {
				ErrorHandler.Warning (Context.CurrentManifestURL, Element, $"Unknown ABI: '{abiName}'");
				return;
			}
			Abi = abi;
		}

		protected override bool Validate ()
		{
			bool ret = base.Validate () && ValidateApiLevel (ApiLevel);

			if (!Abi.HasValue) {
				ErrorHandler.Error (Context.CurrentManifestURL, Element, $"Missing 'abi' child element in element {Element.Name}");
				ret = false;
			}

			return ret;
		}
	}
}
