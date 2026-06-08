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
using System.Xml;
using System.Xml.Linq;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	public abstract class ElementParser
	{
		protected XElement Element { get; }
		protected ParserContext Context { get; }
		protected Dictionary<string, XNamespace> Namespaces { get; }
		protected IParserErrorHandler ErrorHandler { get; }
		protected bool IgnoreNamespaceAttributes { get; set; }

		protected ElementParser (ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces)
		{
			Context = parserContext ?? throw new ArgumentNullException (nameof (parserContext));
			ErrorHandler = parserContext.ErrorHandler;
			Element = element ?? throw new ArgumentNullException (nameof (element));
			Namespaces = namespaces;
		}

		public void Parse ()
		{
			Dictionary<string, Action<XAttribute>> knownAttributes = GetKnownAttributes ();
			Dictionary<string, Action<XElement>> knownElements = GetKnownChildElements ();

			if (Element.HasAttributes) {
				if (knownAttributes == null) {
					if (!IgnoreNamespaceAttributes || !AllAttributesAreNamespaceDeclarations (Element))
						ErrorHandler.Warning (Context.CurrentManifestURL, Element, $"Element '{Element.Name.GetFullName ()}' with parser {GetType().Name} has attributes but this version of the library does not support any of them");
				} else {
					Action<XAttribute> handler;
					foreach (XAttribute attr in Element.Attributes ()) {
						if (IgnoreNamespaceAttributes && attr.IsNamespaceDeclaration)
							continue;

						if (!knownAttributes.TryGetValue (attr.Name.GetFullName (), out handler))
							handler = null;

						if (handler == null) {
							ErrorHandler.Warning (Context.CurrentManifestURL, Element, $"Element '{Element.Name.GetFullName ()}' with parser {GetType().Name} has attribute '{attr.Name.GetFullName ()}' but this version of the library does not support it");
							continue;
						}

						handler (attr);
					}
				}
			}

			if (Element.HasElements) {
				if (knownElements == null) {
					ErrorHandler.Warning (Context.CurrentManifestURL, Element, $"Element '{Element.Name.GetFullName ()}' with parser {GetType().Name} has child elements but this version of the library does not support any children in this element");
				} else {
					Action<XElement> handler;
					foreach (XElement child in Element.Elements ()) {
						if (!knownElements.TryGetValue (child.Name.GetFullName (), out handler))
							handler = null;

						if (handler == null) {
							ErrorHandler.Error (Context.CurrentManifestURL, Element, $"Element '{Element.Name.GetFullName ()}' with parser {GetType().Name} has a child element '{child.Name.GetFullName ()}' but this version of the library does not support it");
							continue;
						}

						handler (child);
					}
				}
			}

			Parsed ();
		}

		protected virtual void Parsed ()
		{
		}

		bool AllAttributesAreNamespaceDeclarations (XElement element)
		{
			foreach (XAttribute attr in element.Attributes ()) {
				if (!attr.IsNamespaceDeclaration)
					return false;
			}

			return true;
		}

		protected string GetAttributeName (string namespaceName, string attributeName, bool warnIfMissing = true)
		{
			XNamespace xsins = GetNamespace (namespaceName);
			if (xsins == null && warnIfMissing)
				ErrorHandler.Warning (Context.CurrentManifestURL, Element, $"Required '{namespaceName}' namespace not declared, parsing results might be invalid");

			return xsins == null ? $"{namespaceName}:{attributeName}" : (xsins + attributeName).GetFullName ();
		}

		protected XNamespace GetNamespace (string name)
		{
			return GetNamespace (name, Namespaces);
		}

		public static XNamespace GetNamespace (string name, Dictionary<string, XNamespace> namespaces)
		{
			if (String.IsNullOrEmpty (name) || namespaces == null)
				return null;

			XNamespace ns;
			if (!namespaces.TryGetValue (name, out ns))
				return null;

			return ns;
		}

		protected Uri EnsureAbsoluteUrl (Uri url)
		{
			if (url.IsAbsoluteUri)
				return url;

			return new Uri (Context.BaseURL, url);
		}

		protected abstract Dictionary<string, Action<XElement>> GetKnownChildElements ();
		protected abstract Dictionary<string, Action<XAttribute>> GetKnownAttributes ();
	}
}
