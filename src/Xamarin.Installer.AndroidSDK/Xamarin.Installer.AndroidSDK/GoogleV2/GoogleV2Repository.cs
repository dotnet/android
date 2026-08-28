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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.AndroidSDK.GoogleV2.Parsing;
using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK.GoogleV2
{
	public class GoogleV2Repository : Repository
	{
		static readonly Uri DefaultRepositoryBaseURL = new Uri ("https://dl.google.com/android/repository/");
		static readonly Uri DefaultManifestURL = new Uri (DefaultRepositoryBaseURL, $"repository2-3.xml");
		static readonly Uri DefaultAddonsListURL = new Uri (DefaultRepositoryBaseURL, $"addons_list-5.xml");
		static readonly Regex MANIFEST_FILE_NAME_SANITIZER_REGEX = new Regex ("[-,\\s]+");

		ParserContext parserContext;

		List<RepositoryParser> repoParts;

		IParserErrorHandler errorHandler;

		public Uri RepositoryBaseURL { get; private set; }
		public Uri AddonsListURL { get; private set; }

		public GoogleV2Repository (IParserErrorHandler errorHandler, Uri manifestURL = null, Uri addonsListURL = null, Uri repositoryBaseURL = null, bool cacheManifest = false)
			: base ("Google Repository V2", manifestURL ?? DefaultManifestURL)
		{
			this.errorHandler = errorHandler ?? throw new ArgumentNullException (nameof (errorHandler));
			if (repositoryBaseURL != null)
				RepositoryBaseURL = repositoryBaseURL;
			else if (manifestURL == null) 
				RepositoryBaseURL = DefaultRepositoryBaseURL;
			else
				RepositoryBaseURL = Helpers.GetBaseURL (manifestURL);

			AddonsListURL = addonsListURL ?? DefaultAddonsListURL;

			if (cacheManifest)
				ManifestCacher = LocalManifestProvider.CreateGoogleManifestProvider ();
		}

		public override void Parse ()
		{
			string manifest = LoadManifest ();
			if (string.IsNullOrEmpty (manifest)) {
				Logger.Warning ($"Android manifest should not be empty! Manifest URL: {ManifestURL}");
				// todo: should we throw here?
				return;
			}

			string addonManifest = null;
			bool haveAddons = false;
			try {
				haveAddons = CommonUtilities.Helpers.DownloadToString (AddonsListURL, out addonManifest);

				if (haveAddons && addonManifest != null)
					ManifestCacher?.SaveManifest (addonManifest, ManifestNameToFileName (AddonsListURL.Segments.Last ()));
			} catch (Exception ex) {
				Logger.Error ($"Failed to download addons manifest {AddonsListURL}. Ex: {ex}");
			}

			if (addonManifest == null) {
				Logger.Info ("Trying to load cached addon manifest...");

				addonManifest = ManifestCacher?.GetManifest (ManifestNameToFileName (AddonsListURL.Segments.Last ()));
				if (addonManifest != null)
					haveAddons = true;
			}
			try {
				parserContext = new ParserContext (
					errorHandler,
					RepositoryBaseURL,
					ManifestURL, new MemoryStream (Encoding.UTF8.GetBytes (manifest)),
					AddonsListURL, haveAddons ? new MemoryStream (Encoding.UTF8.GetBytes (addonManifest)) : null
				);
				ParseInternal ();
				DefaultChannel = GetChannel ("channel-0");
				Parsed = true;
			} finally {
				parserContext?.Dispose ();
				parserContext = null;
			}
		}

		void ParseInternal ()
		{
			Dictionary<string, XNamespace> namespaces = null;
			repoParts = new List<RepositoryParser> ();

			Components?.Clear ();
			if (!ParseRepository (parserContext.RepositoryManifest, ManifestURL, ref namespaces)) {
				Logger.Error ("Unable to parse repository {0} at URL `{1}`.", parserContext.RepositoryManifest, ManifestURL);
				return;
			}
			if (parserContext.AddonsListManifest != null)
				ParseAddons (parserContext.AddonsListManifest, parserContext.AddonsListManifestURL, ref namespaces);

			Namespaces = namespaces;
			parserContext.ErrorHandler.Debug ("Namespaces:");
			foreach (var kvp in namespaces) {
				parserContext.ErrorHandler.Debug ($"  {kvp.Key}:{kvp.Value}");
			}

			Dictionary<string, License> licenses = null;
			Dictionary<string, Channel> channels = null;
			int duplicateChannelIdCounter = 0;
			int duplicateLicenseIdCounter = 0;

			foreach (RepositoryParser rp in repoParts) {
				if (rp == null)
					continue;

				MergeItems (rp.Channels, ref channels, ref duplicateChannelIdCounter, (string newID, Channel ch) => {
					UpdateIDs (rp.Packages, ch, (RemotePackage p) => p.ChannelID, (RemotePackage p) => p.ChannelID = newID);
				});

				MergeItems (rp.Licenses, ref licenses, ref duplicateLicenseIdCounter, (string newID, License lic) => {
					UpdateIDs (rp.Packages, lic, (RemotePackage p) => p.LicenseID, (RemotePackage p) => p.LicenseID = newID);
				});

				AddComponents (rp);
			}

			if (Components != null)
				parserContext.ErrorHandler.Debug ($"{Name}: found {Components.Count} packages loaded from {repoParts.Count} repositories");

			if (licenses != null && licenses.Count > 0) {
				parserContext.ErrorHandler.Debug ($"{Name}: found {licenses.Count} unique licenses");
				CopyDictionary (licenses, Licenses);
				licenses.Clear ();
				licenses = null;
			}

			if (channels != null && channels.Count > 0) {
				parserContext.ErrorHandler.Debug ($"{Name}: found {channels.Count} unique channels");
				CopyDictionary (channels, Channels);
				channels.Clear ();
				channels = null;
			}
		}

		void AddComponents (RepositoryParser rp)
		{
			if (rp.Packages == null || rp.Packages.Count == 0)
				return;

			foreach (RemotePackage package in rp.Packages) {
				if (package == null)
					continue;

				Components.Add (package);
			}
		}

		void UpdateIDs<T> (List<RemotePackage> packages, T item, Func<RemotePackage, string> getID, Action<RemotePackage> setID) where T : ItemWithID
		{
			if (packages == null || packages.Count == 0)
				return;

			foreach (RemotePackage rp in packages) {
				if (rp == null || String.Compare (getID (rp), item.ID, StringComparison.Ordinal) != 0)
					continue;
				setID (rp);
			}
		}

		void MergeItems<T> (Dictionary<string, T> sourceDict, ref Dictionary<string, T> items, ref int duplicateIdCounter, Action<string, T> updateID) where T : ItemWithID
		{
			if (sourceDict == null || sourceDict.Count == 0)
				return;

			foreach (KeyValuePair <string, T> kvp in sourceDict) {
				if (kvp.Value == null)
					continue;
				MergeItemWithID (kvp.Value, ref duplicateIdCounter, ref items, updateID);
			}
		}

		void MergeItemWithID<T> (T item, ref int duplicateIdCounter, ref Dictionary<string, T> items, Action<string, T> updateID) where T : ItemWithID
		{
			if (item == null)
				return;

			if (items == null)
				items = new Dictionary<string, T> (StringComparer.Ordinal);

			if (items.ContainsValue (item)) {
				if (items.ContainsKey (item.ID))
					return;

				// We have an identical item but with a different dictionary key, we must update the ID
				KeyValuePair <string, T> kvp = items.FirstOrDefault ((KeyValuePair<string, T> i) => item == i.Value);
				if (kvp.Value == null)
					return;
				updateID (kvp.Key, kvp.Value);
				return;
			}

			string itemID;
			if (items.ContainsKey (item.ID)) {
				// We have an item with identical ID but different contents
				duplicateIdCounter++;
				itemID = $"{item.ID}_{duplicateIdCounter}";
			} else
				itemID = item.ID;
			items.Add (itemID, item);
		}

		bool ParseRepository (Stream manifest, Uri url, ref Dictionary<string, XNamespace> namespaces)
		{
			XDocument doc = LoadManifest (parserContext, manifest, url, ref namespaces);
			if (doc == null) {
				return false;
			}

			var repoParser = new RepositoryParser (this, parserContext, doc.Root, namespaces);
			repoParser.Parse ();
			repoParts.Add (repoParser);
			return true;
		}

		void ParseAddons (Stream manifest, Uri url, ref Dictionary<string, XNamespace> namespaces)
		{
			XDocument doc = LoadManifest (parserContext, manifest, url, ref namespaces);
			if (doc == null) {
				return;
			}
			var addonListParser = new AddonList3Parser (parserContext, doc.Root, namespaces);
			addonListParser.Parse ();

			if (addonListParser.Sites == null || addonListParser.Sites.Count == 0) {
				parserContext.ErrorHandler.Error (parserContext.CurrentManifestURL, doc.Root, $"No addon or system image sites found at URL '{url}'");
				return;
			}

			foreach (AddonSite site in addonListParser.Sites) {
				string addonManifest = null;
				bool gotAddonManifest = false;

				try {
					gotAddonManifest = CommonUtilities.Helpers.DownloadToString (site.Url, out addonManifest);
					if (gotAddonManifest && addonManifest != null)
						ManifestCacher?.SaveManifest (addonManifest, ManifestNameToFileName (site.DisplayName + '_' + AddonsListURL.Segments.Last ()));
				} catch (Exception ex) {
					Logger.Error ($"Failed to load local addon manifest: {ex}");
				}

				if (addonManifest == null) {
					Logger.Info ("Trying to load cached sites manifest...");
					addonManifest = ManifestCacher?.GetManifest (ManifestNameToFileName (site.DisplayName + '_' + AddonsListURL.Segments.Last ()));
					if (addonManifest != null)
						gotAddonManifest = true;
				}

				if (!gotAddonManifest && addonManifest == null)
					continue;

				using (Stream addonManifestContents = new MemoryStream (Encoding.UTF8.GetBytes (addonManifest))) {
					if (!ParseRepository (addonManifestContents, site.Url, ref namespaces)) {
						Logger.Error ("Unable to parse addon manifest contents");
						addonManifestContents.Position = 0;
						Logger.Debug ("{0}", new StreamReader(addonManifestContents).ReadToEnd ());
					}
				}
			}
		}

		string ManifestNameToFileName (string name)
		{
			return "google_" + MANIFEST_FILE_NAME_SANITIZER_REGEX.Replace (name, "_").ToLower();
		}

		XDocument LoadManifest (ParserContext context, Stream manifest, Uri url, ref Dictionary<String, XNamespace> namespaces)
		{
			try {
				context.CurrentManifestURL = null;
				XDocument doc = ParseManifest (GetManifestNameForUrl (url), manifest, LoadOptions.SetLineInfo);
				doc.Root.GetNamespaces (url, ref namespaces);

				context.CurrentManifestURL = url;
				return doc;
			}
			catch (Exception) {
				// Error message logged by ParseManifest()
				return null;
			}
		}

		string GetManifestNameForUrl (Uri url)
		{
			var segments = url.Segments;
			var result = "GoogleAndroidManifest";
			if (segments.Length > 0) {
				var lastUrlSegment = segments[segments.Length - 1];
				return result += $"-{lastUrlSegment.Replace (".xml", "")}";
			}
			return result;
		}
	}
}
