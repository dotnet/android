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
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK
{
	public class Helpers
	{
		static readonly string utf8BOM = Encoding.UTF8.GetString (Encoding.UTF8.GetPreamble ());

		public static void AddDictionaryItem<T> (T item, string key, ref Dictionary<string, T> dict, Action <string> duplicateHandler = null)
		{
			if (dict == null)
				dict = new Dictionary<string, T> (StringComparer.Ordinal);

			if (dict.ContainsKey (key)) {
				if (duplicateHandler != null)
					duplicateHandler (key);
				dict[key] = item;
			} else
				dict.Add (key, item);
		}

		public static string GetComponentArchiveSelector (string os, string arch, uint osbits)
		{
			os = os?.Trim ();
			if (String.IsNullOrEmpty (os))
				os = "any";
			arch = arch?.Trim ();
			if (String.IsNullOrEmpty (arch))
				arch = "any";

			return os + arch + osbits;
		}

		public static XmlDocument LoadXML (string xml, string documentName, bool initializeNamespace, out XmlNamespaceManager nsmgr)
		{
			// XmlDocument breaks when the XML starts with the BOM. Funny thing is, the XML
			// was previously written by XmlWriter... Because why not...???
			if (xml.StartsWith (utf8BOM, StringComparison.Ordinal))
				xml = xml.Remove (0, utf8BOM.Length);
			var doc = new XmlDocument ();
			doc.LoadXml (xml);

			nsmgr = new XmlNamespaceManager (doc.NameTable);
			if (initializeNamespace) {
				XmlElement root = doc.DocumentElement;
				if (root.Attributes != null) {

					foreach (XmlAttribute attr in root.Attributes) {
						if (attr == null || String.IsNullOrEmpty (attr.Name))
							continue;

						string name = GetXMLNamespaceName (attr.Name);
						if (String.IsNullOrEmpty (name))
							continue;

						Logger.Debug ("Adding {0} XML namespace '{1}' == '{2}'", documentName, name, attr.Value);
						nsmgr.AddNamespace (name, attr.Value);
					}
				}
			}

			return doc;
		}

		public static Uri GetBaseURL (Uri fullUrl)
		{
			if (fullUrl == null)
				throw new ArgumentNullException (nameof (fullUrl));
			
			string url = fullUrl.ToString ();
			int fileStart = url.LastIndexOf ('/');
			if (fileStart >= 0)
				return new Uri (url.Substring (0, fileStart + 1));
			else
				return fullUrl;
		}

		static string GetXMLNamespaceName (string xmlns)
		{
			xmlns = xmlns.SafeTrim ();
			if (String.IsNullOrEmpty (xmlns))
				return String.Empty;
			
			int colon;
			if ((colon = xmlns.IndexOf (':')) < 0)
				return xmlns;
			
			return xmlns.Substring (colon + 1);
		}
	}

}
