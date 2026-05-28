//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc (http://microsoft.com)
//
//  All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Linq;

using IniParser;
using IniParser.Model;
using Kajabity.Tools.Java;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.AndroidSDK.GoogleV2;
using Xamarin.Installer.AndroidSDK.GoogleV2.Parsing;
using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK.LocalSDK
{
	class LocalSDKRepository : Repository
	{
		sealed class InformationSource
		{
			public string Name;
			public Func<string, string, bool> Parser;
		}

		IParserErrorHandler errorHandler;

		readonly List<InformationSource> component_info_sources;

		public LocalSDKRepository (IParserErrorHandler errorHandler) : base ("Local SDK Repository")
		{
			this.errorHandler = errorHandler ?? throw new ArgumentNullException (nameof (errorHandler));
			component_info_sources = new List<InformationSource> {
				new InformationSource {Name = "package.xml", Parser = Parse_Package_Xml},
				new InformationSource {Name = "source.properties", Parser = Parse_Source_Properties}
			};
		}

		public override void Parse ()
		{
			// nothing to do here
			Parsed = true;
		}

		protected override void DetectInternal (AndroidSdkInstance sdkInstance)
		{
			if (!Directory.Exists (sdkInstance.Path)) {
				Logger.Info ($"SDK directory '{sdkInstance.Path}' doesn't exist, unable to perform detection");
				return;
			}

			Logger.Debug ($"Detecting Android SDK in '{sdkInstance.Path}'");
			foreach (string dir in Directory.EnumerateDirectories (sdkInstance.Path, "*", SearchOption.AllDirectories)) {
				MaybeParseComponent (dir, sdkInstance.Path);
			}
			sdkInstance.Components = Components.Where(c => c != null).Select(c => c.Clone()).ToList();
			base.DetectInternal(sdkInstance);
		}

		void MaybeParseComponent (string dir, string sdkRoot)
		{
			foreach (InformationSource info in component_info_sources) {
				if (String.IsNullOrEmpty (info?.Name) || info?.Parser == null)
					continue;

				string file = Path.Combine (dir, info.Name);
				if (!File.Exists (file))
					continue;

				Logger.Debug ($"Parsing Android component information from '{file}'");
				if (info.Parser (file, sdkRoot))
					return;
			}
		}

		bool Parse_Package_Xml (string filePath, string sdkRoot)
		{
			var manifest = new Uri (filePath);
			var namespaces = new Dictionary<string, XNamespace> (StringComparer.Ordinal);
			using (var parserContext = new ParserContext (errorHandler, manifest)) {
				parserContext.CurrentManifestURL = manifest;
				using (var fs = File.OpenRead (filePath)) {
					var doc = ParseManifest ("LocalAndroidManifest", fs);
					doc.Root.GetNamespaces (manifest, ref namespaces);
					Namespaces = namespaces;
					XElement element = doc.Root.Element ("localPackage");
					if (element == null)
						return false;

					var rp = new RemotePackageParser (this, parserContext, element, Namespaces);
					rp.Parse ();
					AddComponent (rp.Package);
					if (rp.Package == null)
						return false;
					element = doc.Root.Element ("license");
					if (element == null)
						return true;
					var lp = new LicenseParser (parserContext, element, Namespaces);
					lp.Parse ();
					AddLicense (lp.License);
				}
			}
			return true;
		}

		bool Parse_Source_Properties (string filePath, string sdkRoot)
		{
			JavaProperties props = AndroidUtilities.ReadAndroidProperties (filePath);

			string sv;
			AndroidRevision installedRevision;
			props.GetProperty ("Pkg.Revision", out sv);
			if (!String.IsNullOrEmpty (sv))
				installedRevision = new AndroidRevision (sv, false);
			else
				installedRevision = new AndroidRevision (0);

			string displayName;
			props.GetProperty ("Pkg.Desc", out displayName);

			string path;
			props.GetProperty ("Pkg.Path", out path);

			string license;
			props.GetProperty ("Pkg.License", out license);

			string licenseId;
			props.GetProperty ("Pkg.LicenseRef", out licenseId);

			string manifestUrl;
			props.GetProperty ("Pkg.SourceUrl", out manifestUrl);

			// Detect component info
			AndroidComponentInfo info = null;

			// Platform
			if (props.GetProperty ("Platform.Version", out sv))
				info = CreatePlatformInfo (props);

			// Source
			if (info == null && IsInParentDir (filePath, "sources"))
				info = CreateSourceInfo (props);

			// System image
			if (info == null && props.GetProperty ("SystemImage.Abi", out sv))
				info = CreateSystemImageInfo (props, sv);

			// Addon
			if (info == null && IsInParentDir (filePath, "add-ons"))
				info = CreateAddonInfo (props, filePath);

			// Extra
			if (info == null && props.GetProperty ("Extra.Path", out sv))
				info = CreateExtraInfo (props);

			// Maven
			if (info == null && props.GetProperty ("Maven.Version", out sv))
				info = CreateMavenInfo (props);

			// Generic
			if (info == null)
				info = new AndroidComponentInfoGeneric ("generic:genericDetailsType");

			if (!String.IsNullOrEmpty (licenseId) && !String.IsNullOrEmpty (license))
				AddLicense (new License (licenseId, "text", license));

			if (String.IsNullOrEmpty (path))
				path = GetRelativePath (filePath, sdkRoot);

			AddComponent (new RemotePackage (this, new Uri (filePath), null, errorHandler, null) {
				Present = true,
				Path = ConstructPackagePath (filePath, sdkRoot),
				FileSystemPath = path,
				Info = info,
				DisplayName = displayName,
				InstalledRevision = installedRevision,
				Revision = installedRevision,
				LicenseID = license
			});

			return true;
		}

		string ConstructPackagePath (string filePath, string sdkRoot)
		{
			return GetRelativePath (filePath, sdkRoot)?.Replace (Path.DirectorySeparatorChar, ';');
		}

		string GetRelativePath (string filePath, string sdkRoot)
		{
			if (String.IsNullOrEmpty (filePath) || String.IsNullOrEmpty (sdkRoot) || filePath.Length <= sdkRoot.Length)
				return null;
			
			return Path.GetDirectoryName (filePath).Substring (sdkRoot.Length + 1);
		}

		AndroidComponentInfoMaven CreateMavenInfo (JavaProperties props)
		{
			return new AndroidComponentInfoMaven (
				"addon:mavenType",
				CreateVendor (props, "Extra.VendorId", "Extra.VendorDisplay")
			);
		}

		AndroidComponentInfoExtra CreateExtraInfo (JavaProperties props)
		{
			return new AndroidComponentInfoExtra (
				"addon:extraDetailsType", 
				CreateVendor (props, "Extra.VendorId", "Extra.VendorDisplay")
			);
		}

		AndroidComponentInfoAddon CreateAddonInfo (JavaProperties props, string propsFilePath)
		{
			string apiLevel;
			if (!props.GetProperty ("AndroidVersion.ApiLevel", out apiLevel))
				return null;

			string codename;
			props.GetProperty ("AndroidVersion.CodeName", out codename);

			PackageTag tag = CreateTag (props, "SystemImage.TagId", "Addon.NameDisplay");
			PackageVendor vendor = CreateVendor (props, "Addon.VendorId", "Addon.VendorDisplay");
			List<PackageLibrary> libraries = GetAddonLibraries (Path.Combine (Path.GetDirectoryName (propsFilePath), "manifest.ini"));

			return new AndroidComponentInfoAddon ("addon:addonDetailsType", apiLevel, codename, tag, vendor, libraries);
		}

		List<PackageLibrary> GetAddonLibraries (string manifestIni)
		{
			if (!File.Exists (manifestIni))
				return null;

			var parser = new FileIniDataParser ();
			IniData data = parser.ReadFile (manifestIni);
			string libs = data.GetKey ("libraries")?.Trim ();
			if (String.IsNullOrEmpty (libs))
				return null;

			List<PackageLibrary> libraries = null;
			string[] libids = libs.Split (';');
			foreach (string lid in libids) {
				string id = lid.Trim ();
				if (String.IsNullOrEmpty (lid))
					continue;
				AddPackageLibrary (id, data.GetKey (id), ref libraries);
			}

			return libraries;
		}

		void AddPackageLibrary (string id, string details, ref List<PackageLibrary> libraries)
		{
			string jarPath, description;

			string[] dparts = details?.Split (';');
			if (dparts.Length == 1) {
				jarPath = dparts[0].Trim ();
				description = id;
			} else {
				jarPath = dparts[0].Trim ();
				description = dparts[1].Trim ();
			}

			if (libraries == null)
				libraries = new List<PackageLibrary> ();
			libraries.Add (new PackageLibrary {
				Name = id,
				LocalJarPath = jarPath,
				Description = description
			});
		}

		AndroidComponentInfoSystemImage CreateSystemImageInfo (JavaProperties props, string abiName)
		{
			string apiLevel;
			if (!props.GetProperty ("AndroidVersion.ApiLevel", out apiLevel))
				return null;

			AndroidSystemImageAbi abi;
			try {
				abi = AndroidUtilities.StringToAbi (abiName);
			} catch (ArgumentOutOfRangeException) {
				abi = AndroidSystemImageAbi.Any;
			}

			string codename;
			props.GetProperty ("AndroidVersion.CodeName", out codename);

			PackageTag tag = CreateTag (props, "SystemImage.TagId", "SystemImage.TagDisplay");
			PackageVendor vendor = CreateVendor (props, "Addon.VendorId", "Addon.VendorDisplay");

			return new AndroidComponentInfoSystemImage ("sys-img:sysImgDetailsType", abi, abiName, apiLevel, codename, tag, vendor);
		}

		AndroidComponentInfoSource CreateSourceInfo (JavaProperties props)
		{
			string apiLevel;
			if (!props.GetProperty ("AndroidVersion.ApiLevel", out apiLevel))
				return null;

			string codename;
			props.GetProperty ("AndroidVersion.CodeName", out codename);

			return new AndroidComponentInfoSource ("sdk:sourceDetailsType", apiLevel, codename);
		}

		AndroidComponentInfoPlatform CreatePlatformInfo (JavaProperties props)
		{
			string apiLevel;
			if (!props.GetProperty ("AndroidVersion.ApiLevel", out apiLevel))
				return null;
			
			string layoutLibApi;
			if (!props.GetProperty ("Layoutlib.Api", out layoutLibApi))
				return null;

			string codename;
			props.GetProperty ("Platform.CodeName", out codename);

			if (String.IsNullOrEmpty (codename))
				props.GetProperty ("AndroidVersion.CodeName", out codename);

			return new AndroidComponentInfoPlatform ("sdk:platformDetailsType", apiLevel, codename, layoutLibApi);
		}

		PackageVendor CreateVendor (JavaProperties props, string idKeyName, string displayKeyName)
		{
			return GetIdProperties (props, idKeyName, displayKeyName, out string id, out string display) ? new PackageVendor (id, display) : null;
		}
		                            
		PackageTag CreateTag (JavaProperties props, string idKeyName, string displayKeyName)
		{
			return GetIdProperties (props, idKeyName, displayKeyName, out string id, out string display) ? new PackageTag (id, display) : null;
		}

		bool GetIdProperties (JavaProperties props, string idKeyName, string displayKeyName, out string id, out string display)
		{
			display = null;
			id = null;
			if (String.IsNullOrEmpty (idKeyName))
				return false;
			
			if (props.GetProperty (idKeyName, out id) && !String.IsNullOrEmpty (id)) {
				if (!String.IsNullOrEmpty (displayKeyName))
					props.GetProperty (displayKeyName, out display);
				return true;
			}
			return false;
		}

		bool IsInParentDir (string filePath, string parentDirName)
		{
			if (String.IsNullOrEmpty (filePath) || String.IsNullOrEmpty (parentDirName))
				return false;
			
			string dir = Path.Combine (Path.GetDirectoryName (filePath), "..");
			return String.Compare (parentDirName, Path.GetFileName (Path.GetFullPath (dir)), StringComparison.OrdinalIgnoreCase) == 0;
		}

		void AddLicense (License license)
		{
			if (license == null)
				return;
			if (Licenses.Values.Contains (license))
				return;
			Licenses[license.ID] = license;
		}

		void AddComponent (IAndroidComponent component)
		{
			if (component == null)
				return;
			if (Components.Contains (component))
				return;	
			Components.Add (component);
		}
	}
}
