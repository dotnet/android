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
	abstract class TypeDetailsParser : ElementParser
	{
		protected string Type { get; private set; }
		public AndroidComponentInfo Info { get; private set; }

		protected TypeDetailsParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
			IgnoreNamespaceAttributes = true; // for package.xml
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return new Dictionary<string, Action<XAttribute>> (StringComparer.Ordinal) {
				{GetAttributeName ("xsi", "type"), (XAttribute e) => Type = e.Value}
			};
		}

		protected Dictionary<string, Action<T>> EnsureDictionary<T> (Dictionary<string, Action<T>> dict)
		{
			if (dict == null)
				return new Dictionary<string, Action<T>> (StringComparer.Ordinal);
			return dict;
		}

		protected override void Parsed ()
		{
			base.Parsed ();
			Info = Validate () ? CreateComponentInfo () : null;
		}

		protected virtual bool Validate ()
		{
			if (String.IsNullOrEmpty (Type)) // Not critical when missing, just warn
				ErrorHandler.Warning (Context.CurrentManifestURL, Element, $"Type not specified on element {Element.Name}");

			return true;
		}

		protected bool ValidateApiLevel (string apiLevel)
		{
			if (String.IsNullOrEmpty (apiLevel)) {
				ErrorHandler.Error (Context.CurrentManifestURL, Element, $"Missing 'api-level' child element in element {Element.Name}");
				return false;
			}

			return true;
		}

		protected PackageTag ParseChildElement_Tag (XElement element)
		{
			var ptp = new PackageTagParser (Context, element, Namespaces);
			ptp.Parse ();
			return ptp.Tag;
		}

		protected PackageVendor ParseChildElement_Vendor (XElement element)
		{
			var vp = new PackageVendorParser (Context, element, Namespaces);
			vp.Parse ();
			return vp.Vendor;
		}

		protected abstract AndroidComponentInfo CreateComponentInfo ();
	}
}
