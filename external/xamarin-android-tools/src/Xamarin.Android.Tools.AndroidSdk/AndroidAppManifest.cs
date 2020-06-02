using System;
using System.Linq;
using System.Xml;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;

namespace Xamarin.Android.Tools
{
	public class AndroidAppManifest
	{
		AndroidVersions     versions;
		XDocument doc;
		XElement manifest, application, usesSdk;

		static readonly XNamespace aNS = "http://schemas.android.com/apk/res/android";
		static readonly XName aName = aNS + "name";

		public  static  XNamespace          AndroidXNamespace       => aNS;
		public  static  XName               NameXName               => aName;

		public  XDocument           Document            => doc;

		AndroidAppManifest (AndroidVersions versions, XDocument doc)
		{
			if (versions == null)
				throw new ArgumentNullException (nameof (versions));
			if (doc == null)
				throw new ArgumentNullException (nameof (doc));
			this.versions   = versions;
			this.doc = doc;
			manifest = doc.Root;
			if (manifest.Name != "manifest")
				throw new ArgumentException ("App manifest does not have 'manifest' root element", nameof (doc));

			application = manifest.Element ("application");
			if (application == null)
				manifest.Add (application = new XElement ("application"));

			usesSdk = manifest.Element ("uses-sdk");
			if (usesSdk == null)
				manifest.Add (usesSdk = new XElement ("uses-sdk"));
		}

		public static string CanonicalizePackageName (string packageNameOrAssemblyName)
		{
			if (packageNameOrAssemblyName == null)
				throw new ArgumentNullException ("packageNameOrAssemblyName");
			if (string.IsNullOrEmpty (packageNameOrAssemblyName = packageNameOrAssemblyName.Trim ()))
				throw new ArgumentException ("Must specify a package name or assembly name", "packageNameOrAssemblyName");

			string[] packageParts = packageNameOrAssemblyName.Split (new[]{'.'}, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < packageParts.Length; ++i) {
				packageParts [i] = Regex.Replace (packageParts [i], "[^A-Za-z0-9_]", "_");
				if (char.IsDigit (packageParts [i], 0) || packageParts [i][0] == '_')
					packageParts [i] = "x" + packageParts [i];
			}
			return packageParts.Length == 1
				? packageParts [0] + "." + packageParts [0]
					: string.Join (".", packageParts);
		}

		public static AndroidAppManifest Create (string packageName, string appLabel, AndroidVersions versions)
		{
			return new AndroidAppManifest (versions, XDocument.Parse (
				@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"">
  <uses-sdk />
  <application android:label="""">
  </application>
</manifest>")) {
				PackageName = packageName,
				ApplicationLabel = appLabel,
			};
		}

		public static AndroidAppManifest Load (string filename, AndroidVersions versions)
		{
			if (filename == null)
				throw new ArgumentNullException (nameof (filename));
			if (versions == null)
				throw new ArgumentNullException (nameof (versions));

			return Load (XDocument.Load (filename), versions);
		}

		public static AndroidAppManifest Load (XDocument doc, AndroidVersions versions)
		{
			if (doc == null)
				throw new ArgumentNullException (nameof (doc));
			if (versions == null)
				throw new ArgumentNullException (nameof (versions));

			return new AndroidAppManifest (versions, doc);
		}

		public void Write (XmlWriter writer)
		{
			doc.Save (writer);
		}

		public void WriteToFile (string fileName)
		{
			var xmlSettings = new XmlWriterSettings () {
				Encoding = Encoding.UTF8,
				CloseOutput = false,
				Indent = true,
				IndentChars = "\t",
				NewLineChars = "\n",
			};

			var tempFile = FileUtil.GetTempFilenameForWrite (fileName);
			bool success = false;
			try {
				using (var writer = XmlTextWriter.Create (tempFile, xmlSettings)) {
					Write (writer);
				}
				FileUtil.SystemRename (tempFile, fileName);
				success = true;
			} finally {
				if (!success) {
					try {
						File.Delete (tempFile);
					} catch {
						//the original exception is more important than this one
					}
				}
			}
		}

		static string? NullIfEmpty (string? value)
		{
			return string.IsNullOrEmpty (value) ? null : value;
		}

		public string? PackageName {
			get { return (string) manifest.Attribute ("package");  }
			set { manifest.SetAttributeValue ("package", NullIfEmpty (value)); }
		}

		public string? ApplicationLabel {
			get { return (string) application.Attribute (aNS + "label");  }
			set { application.SetAttributeValue (aNS + "label", NullIfEmpty (value)); }
		}

		public string? ApplicationIcon {
			get { return (string) application.Attribute (aNS + "icon");  }
			set { application.SetAttributeValue (aNS + "icon", NullIfEmpty (value)); }
		}

		public string? ApplicationTheme {
			get { return (string) application.Attribute (aNS + "theme"); }
			set { application.SetAttributeValue (aNS + "theme", NullIfEmpty (value)); }
		}

		public string? VersionName {
			get { return (string) manifest.Attribute (aNS + "versionName");  }
			set { manifest.SetAttributeValue (aNS + "versionName", NullIfEmpty (value)); }
		}

		public string? VersionCode {
			get { return (string) manifest.Attribute (aNS + "versionCode");  }
			set { manifest.SetAttributeValue (aNS + "versionCode", NullIfEmpty (value)); }
		}

		public string? InstallLocation {
			get { return (string) manifest.Attribute (aNS + "installLocation"); }
			set { manifest.SetAttributeValue (aNS + "installLocation", NullIfEmpty (value)); }
		}

		public int? MinSdkVersion {
			get { return ParseSdkVersion (usesSdk.Attribute (aNS + "minSdkVersion")); }
			set { usesSdk.SetAttributeValue (aNS + "minSdkVersion", value == null ? null : value.ToString ()); }
		}

		public int? TargetSdkVersion {
			get { return ParseSdkVersion (usesSdk.Attribute (aNS + "targetSdkVersion")); }
			set { usesSdk.SetAttributeValue (aNS + "targetSdkVersion", value == null ? null : value.ToString ()); }
		}

		int? ParseSdkVersion (XAttribute attribute)
		{
			var version = (string)attribute;
			if (string.IsNullOrEmpty (version))
				return null;
			int vn;
			if (!int.TryParse (version, out vn)) {
				int? apiLevel = versions.GetApiLevelFromId (version);
				if (apiLevel.HasValue)
					return apiLevel.Value;
				return versions.MaxStableVersion?.ApiLevel;
			}
			return vn;
		}

		public IEnumerable<string> AndroidPermissions {
			get {
				foreach (var el in manifest.Elements ("uses-permission")) {
					var name = (string) el.Attribute (aName);
					if (name == null)
						continue;
					var lastDot = name.LastIndexOf ('.');
					if (lastDot >= 0)
						yield return name.Substring (lastDot + 1);
				}
			}
		}

		public IEnumerable<string> AndroidPermissionsQualified {
			get {
				foreach (var el in manifest.Elements ("uses-permission")) {
					var name = (string) el.Attribute (aName);
					if (name != null)
						yield return name;
				}
			}
		}

		public bool? Debuggable {
			get { return (bool?) application.Attribute (aNS + "debuggable"); }
			set { application.SetAttributeValue (aNS + "debuggable", value); }
		}

		public void SetAndroidPermissions (IEnumerable<string> permissions)
		{
			var newPerms = new HashSet<string> (permissions.Select (FullyQualifyPermission));
			var current = new HashSet<string> (AndroidPermissionsQualified);
			AddAndroidPermissions (newPerms.Except (current));
			RemoveAndroidPermissions (current.Except (newPerms));
		}

		void AddAndroidPermissions (IEnumerable<string> permissions)
		{
			var newElements = permissions.Select (p => new XElement ("uses-permission", new XAttribute (aName, p)));

			var lastPerm = manifest.Elements ("uses-permission").LastOrDefault ();
			if (lastPerm != null) {
				foreach (var el in newElements) {
					lastPerm.AddAfterSelf (el);
					lastPerm = el;
				}
			} else {
				var parentNode = (XNode) manifest.Element ("application") ?? manifest.LastNode;
				foreach (var el in newElements)
					parentNode.AddBeforeSelf (el);
			}
		}

		string FullyQualifyPermission (string permission)
		{
			//if already qualified, don't mess with it
			if (permission.IndexOf ('.') > -1)
				return permission;

			switch (permission) {
			case "READ_HISTORY_BOOKMARKS":
			case "WRITE_HISTORY_BOOKMARKS":
				return string.Format ("com.android.browser.permission.{0}", permission);
			default:
				return string.Format ("android.permission.{0}", permission);
			}
		}

		void RemoveAndroidPermissions (IEnumerable<string> permissions)
		{
			var perms = new HashSet<string> (permissions);
			var list = manifest.Elements ("uses-permission")
				.Where (el => perms.Contains ((string)el.Attribute (aName))).ToList ();
			foreach (var el in list)
				el.Remove ();
		}

		[Obsolete ("Use GetLaunchableFastdevActivityName or GetLaunchableUserActivityName")]
		public string? GetLaunchableActivityName ()
		{
			return GetLaunchableFastDevActivityName ();
		}

		/// <summary>Gets an activity that can be used to initialize the override directory for fastdev.</summary>
		[Obsolete ("This should not be needed anymore; Activity execution is not part of installation.")]
		public string? GetLaunchableFastDevActivityName ()
		{
			string? first = null;
			foreach (var a in GetLaunchableActivities ()) {
				var name = (string) a.Attribute (aName);
				//prefer the fastdev launcher, it's quicker
				if (name == "mono.android.__FastDevLauncher") {
					return name;
				}
				//else just use the first other launchable activity
				if (first == null) {
					first = name;
				}
			}

			return string.IsNullOrEmpty (first)? null : first;
		}

		// We add a fake launchable activity for FastDev, but we don't want
		// to launch that one when the user does Run or Debug
		public string? GetLaunchableUserActivityName ()
		{
			return GetLaunchableActivities ()
				.Select (a => (string) a.Attribute (aName))
				.FirstOrDefault (name => !string.IsNullOrEmpty (name) && name != "mono.android.__FastDevLauncher");
		}

		IEnumerable<XElement> GetLaunchableActivities ()
		{
			foreach (var activity in application.Elements ("activity")) {
				var filter = activity.Element ("intent-filter");
				if (filter != null) {
					foreach (var category in filter.Elements ("category"))
						if (category != null && (string)category.Attribute (aName) == "android.intent.category.LAUNCHER")
							yield return activity;
				}
			}
		}

		public IEnumerable<string> GetAllActivityNames ()
		{
			foreach (var activity in application.Elements ("activity")) {
				var activityName = (string) activity.Attribute (aName);
				if (activityName != "mono.android.__FastDevLauncher")
					yield return activityName;
			}
		}

		public IEnumerable<string> GetLaunchableActivityNames ()
		{
			return GetLaunchableActivities ()
				.Select (a => (string) a.Attribute (aName))
				.Where (name => !string.IsNullOrEmpty (name) && name != "mono.android.__FastDevLauncher");
		}
	}
}

