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

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2.Parsing
{
	public class RepositoryParser : ElementParser
	{
		Dictionary<string, License> licenses;
		Dictionary<string, Channel> channels;
		List<RemotePackage> packages;
		GoogleV2Repository repository;

		public Dictionary<string, License> Licenses => licenses;
		public Dictionary<string, Channel> Channels => channels;
		public List<RemotePackage> Packages => packages;
		public Uri RepositoryURL { get; }

		public bool IncludeAllArchives { get; set; } // For Xamarin manifest generator this is 'true'

		public RepositoryParser (GoogleV2Repository repository, ParserContext parserContext, XElement element, Dictionary<string, XNamespace> namespaces = null) : base (parserContext, element, namespaces)
		{
			this.repository = repository ?? throw new ArgumentNullException (nameof (repository));
			IgnoreNamespaceAttributes = true;
			RepositoryURL = parserContext.CurrentManifestURL;
		}

		protected override Dictionary<string, Action<XAttribute>> GetKnownAttributes ()
		{
			return null;
		}

		protected override Dictionary<string, Action<XElement>> GetKnownChildElements ()
		{
			return new Dictionary<string, Action<XElement>> (StringComparer.Ordinal) {
				{"license", ParseChildElement_License},
				{"channel", ParseChildElement_Channel},
				{"remotePackage", ParseChildElement_RemotePackage},
			};
		}

		void ParseChildElement_RemotePackage (XElement element)
		{
			var rp = new RemotePackageParser (repository, Context, element, Namespaces) {
				IncludeAllArchives = IncludeAllArchives
			};
			rp.Parse ();
			AddPackage (rp.Package);
		}

		void ParseChildElement_Channel (XElement element)
		{
			string id = element.Attribute ("id")?.Value;
			if (String.IsNullOrEmpty (id)) {
				ErrorHandler.Error (Context.CurrentManifestURL, element, $"Channel element misses the required 'id' attribute");
				return;
			}

			string value = element.Value ?? id;
			AddChannel (id, value);
		}

		void ParseChildElement_License (XElement element)
		{
			var lp = new LicenseParser (Context, element, Namespaces);
			lp.Parse ();
			AddLicense (lp.License);
		}

		void AddPackage (RemotePackage package)
		{
			if (packages == null)
				packages = new List<RemotePackage> ();
			packages.Add (package);
		}

		void AddChannel (string id, string text)
		{
			AddDictionaryItem (
				new Channel (id, text),
				id,
				ref channels,
				$"Duplicate channel with id '{id}' found. Will replace the old value"
			);
		}

		void AddLicense (License license)
		{
			if (license == null)
				return;

			AddDictionaryItem (
				license,
				license.ID,
				ref licenses,
				$"Duplicate license with id '{license.ID}' found. Will replace the old value"
			);
		}

		void AddDictionaryItem<T> (T item, string key, ref Dictionary<string, T> dict, string duplicateMessage, Func <T, string> getDuplicateLocation = null)
		{
			if (dict == null)
				dict = new Dictionary<string, T> (StringComparer.Ordinal);

			if (dict.ContainsKey (key)) {
				ErrorHandler.Warning (Context.CurrentManifestURL, Element, duplicateMessage);
				if (getDuplicateLocation != null) {
					string location = getDuplicateLocation (dict[key]);
					if (!String.IsNullOrEmpty (location))
						ErrorHandler.Warning ($"Location of the previous value: {location}");
				}
				dict[key] = item;
			} else
				dict.Add (key, item);
		}
	}
}
