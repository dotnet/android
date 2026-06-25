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

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	class TypeDetailsParserFactory
	{
		delegate TypeDetailsParser TypeCreator (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces);

		static TypeCreator genericCreator = (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces) => new TypeDetailsGenericParser (parserContext, element, namespaces);
		static TypeCreator platfomCreator = (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces) => new TypeDetailsPlatformParser (parserContext, element, namespaces);
		static TypeCreator sourceCreator = (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces) => new TypeDetailsSourceParser (parserContext, element, namespaces);
		static TypeCreator systemImageCreator = (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces) => new TypeDetailsSystemImageParser (parserContext, element, namespaces);
		static TypeCreator addonCreator = (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces) => new TypeDetailsAddOnParser (parserContext, element, namespaces);
		static TypeCreator extraCreator = (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces) => new TypeDetailsExtraParser (parserContext, element, namespaces);
		static TypeCreator mavenCreator = (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces) => new TypeDetailsMavenParser (parserContext, element, namespaces);

		// The map looks a bit convoluted but it is necessary in order to support type details lookups from different sources.
		// The package type is namespaced and uses different namespace names (but the same URIs) in the online repository and
		// package.xml on disk. The namespace-less version is just a fallback.
		static readonly Dictionary<string, TypeCreator> typeDetailsMap = new Dictionary<string, TypeCreator> {
			// Generic
			{"genericDetailsType", genericCreator},
			{"generic:genericDetailsType", genericCreator},
			{"http://schemas.android.com/repository/android/generic/01:genericDetailsType", genericCreator},

			// Platform
			{"platformDetailsType", platfomCreator},
			{"sdk:platformDetailsType", platfomCreator},
			{"http://schemas.android.com/sdk/android/repo/repository2/01:platformDetailsType", platfomCreator},

			// Source
			{"sourceDetailsType", sourceCreator},
			{"sdk:sourceDetailsType", sourceCreator},
			{"http://schemas.android.com/sdk/android/repo/repository2/01:sourceDetailsType", sourceCreator},

			// System image
			{"sysImgDetailsType", systemImageCreator},
			{"sys-img:sysImgDetailsType", systemImageCreator},
			{"http://schemas.android.com/sdk/android/repo/sys-img2/01:sysImgDetailsType", systemImageCreator},

			// Addon
			{"addonDetailsType", addonCreator},
			{"addon:addonDetailsType", addonCreator},
			{"http://schemas.android.com/sdk/android/repo/addon2/01:addonDetailsType", addonCreator},

			// Extra
			{"extraDetailsType", extraCreator},
			{"addon:extraDetailsType", extraCreator},
			{"http://schemas.android.com/sdk/android/repo/addon2/01:extraDetailsType", extraCreator},

			// Maven
			{"mavenType", mavenCreator},
			{"addon:mavenType", mavenCreator},
			{"http://schemas.android.com/sdk/android/repo/addon2/01:mavenType", mavenCreator},
		};

		public static TypeDetailsParser CreateInstance (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces)
		{
			if (parserContext == null)
				throw new ArgumentNullException (nameof (parserContext));

			if (element == null)
				throw new ArgumentNullException (nameof (element));

			element.GetNamespaces (parserContext.CurrentManifestURL, ref namespaces);
			XNamespace xsins = ElementParser.GetNamespace ("xsi", namespaces);
			if (xsins == null) {
				parserContext.ErrorHandler.Error (parserContext.CurrentManifestURL, element, $"Namespace 'xsi' undefined, unable to determine the TypeDetails mapping");
				return null;
			}

			XAttribute xsiType = element.GetAttribute (xsins, "type");
			if (xsiType == null) {
				parserContext.ErrorHandler.Error (parserContext.CurrentManifestURL, element, $"'xsi:type' attribute not found on element {element.Name} {element.GetLocation ()}");
				return null;
			}

			TypeCreator creator = FindCreator (parserContext, xsiType.Value, element, namespaces);
			if (creator == null)
				return null;

			TypeDetailsParser ret = creator (parserContext, element, namespaces);
			if (ret == null) {
				parserContext.ErrorHandler.Error (parserContext.CurrentManifestURL, element, $"Failed to map TypeDetails element {element.Name} ('{xsiType.Value}') to a managed type {element.GetLocation ()}");
				return null;
			}

			return ret;
		}

		static TypeCreator FindCreator (ParserContext parserContext, string typeName, XElement element, Dictionary<string, XNamespace> namespaces)
		{
			TypeCreator creator = LookupCreator (typeName);
			if (creator != null)
				return creator;

			string[] parts = typeName.Split (':');
			if (parts.Length != 2)
				goto noCreator;

			XNamespace ns = ElementParser.GetNamespace (parts[0], namespaces);
			creator = LookupCreator ($"{ns.NamespaceName}:{parts[1]}");
			if (creator != null)
				return creator;

			creator = LookupCreator (parts[1]);
			if (creator == null)
				goto noCreator;
			return creator;

			noCreator:
			parserContext.ErrorHandler.Error (parserContext.CurrentManifestURL, element, $"Unknown type of the TypeDetails element {element.Name}: '{typeName}' {element.GetLocation ()}");
			return null;
		}

		static TypeCreator LookupCreator (string typeName)
		{
			if (!typeDetailsMap.TryGetValue (typeName, out TypeCreator creator) || creator == null)
				return null;
			return creator;
		}
	}
}
