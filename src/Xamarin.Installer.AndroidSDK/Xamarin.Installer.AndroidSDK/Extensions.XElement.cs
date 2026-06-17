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
using System.Xml;
using System.Xml.Linq;

using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK
{
	public static partial class Extensions
	{
		public static void GetLineInfo (this XElement e, out int line, out int column)
		{
			IXmlLineInfo linfo = e.GetLineInfo ();
			if (linfo == null || !linfo.HasLineInfo ()) {
				line = column = -1;
				return;
			}
			line = linfo.LineNumber;
			column = linfo.LinePosition;
		}

		public static IXmlLineInfo GetLineInfo (this XElement e)
		{
			return e as IXmlLineInfo;
		}

		public static string GetLocation (this XElement e, Uri documentUrl = null)
		{
			if (e == null)
				return String.Empty;
			
			if (documentUrl == null)
				return e.GetLineInfo ().AsString ("(", ")");

			return $"({documentUrl} {e.GetLineInfo ().AsString ()}";
		}

		public static string GetChildElementValue (this XElement element, string childName)
		{
			if (element == null)
				return String.Empty;

			XElement child = element.Elements (childName)?.FirstOrDefault ();
			return child?.Value ?? String.Empty;
		}

		public static string GetAttributeValue (this XElement e, string name, bool required = true, Uri documentUrl = null)
		{
			XAttribute ret = e.GetAttribute (name);
			if (ret == null) {
				if (required)
					throw new InvalidOperationException ($"Required attribute '{name}' missing for element {e?.Name} at {e.GetLocation (documentUrl)}");
				return String.Empty;
			}

			return ret.Value;
		}

		public static XAttribute GetAttribute (this XElement e, string name)
		{
			return e?.Attribute (name);
		}

		public static XAttribute GetAttribute (this XElement e, XNamespace ns, string name)
		{
			return e?.Attribute (ns + name);
		}

		public static string GetElementValue (this XElement e)
		{
			return e?.Value?.Trim ();
		}

		public static void GetNamespaces (this XElement root, Uri docURL, ref Dictionary<string, XNamespace> namespaces)
		{
			if (root == null || !root.HasAttributes)
				return;

			foreach (XAttribute attr in root.Attributes ())
				GetNamespace (root, attr, docURL, ref namespaces);
		}

		static void GetNamespace (XElement element, XAttribute attr, Uri docURL, ref Dictionary<string, XNamespace> namespaces)
		{
			if (!attr.IsNamespaceDeclaration)
				return;

			if (namespaces == null)
				namespaces = new Dictionary<string, XNamespace> (StringComparer.Ordinal);

			if (namespaces.ContainsKey (attr.Name.LocalName)) {
				if (namespaces[attr.Name.LocalName] != attr.Value)
					Logger.Warning ($"Element '{element.Name.GetFullName ()}' has a duplicate attribute '{attr.Name.GetFullName ()}' in document '{docURL}'. New value will override the old one");
				namespaces[attr.Name.LocalName] = attr.Value;
			} else
				namespaces.Add (attr.Name.LocalName, attr.Value);
		}
	}
}
