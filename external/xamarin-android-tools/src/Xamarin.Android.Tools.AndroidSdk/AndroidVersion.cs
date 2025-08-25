using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Xamarin.Android.Tools
{
	public class AndroidVersion
	{
		// Android API Level. *Usually* corresponds to $(AndroidSdkPath)/platforms/android-$(ApiLevel)/android.jar
		public  int             ApiLevel                { get; private set; }

		// Android API Level; includes "minor" version bumps, e.g. Android 16 QPR2 is "36.1" while ApiLevel=36
		public  Version         VersionCodeFull         { get; private set; }

		// Android API Level ID. == ApiLevel on stable versions, will be e.g. `N` for previews: $(AndroidSdkPath)/platforms/android-N/android.jar
		public  string          Id                      { get; private set; }

		// Name of an Android release, e.g. "Oreo"
		public  string?         CodeName                { get; private set; }

		// Android version number, e.g. 8.0
		public string           OSVersion               { get; private set; }

		// Xamarin.Android $(TargetFrameworkVersion) value, e.g. 8.0
		public Version          TargetFrameworkVersion  { get; private set; }

		// TargetFrameworkVersion *with* a leading `v`, e.g. "v8.0"
		public string           FrameworkVersion        { get; private set; }

		// Is this API level stable? Should be False for non-numeric Id values.
		public bool             Stable                  { get; private set; }

		internal    HashSet<string> Ids                 { get; } = new ();

		// Alternate Ids for a given API level. Allows for historical mapping, e.g. API-11 has alternate ID 'H'.
		internal    string[]?   AlternateIds {
			set => Ids.UnionWith (value);
		}

		public AndroidVersion (int apiLevel, string osVersion, string? codeName = null, string? id = null, bool stable = true)
			: this (new Version (apiLevel, 0), osVersion, codeName, id, stable)
		{
		}

		public AndroidVersion (Version versionCodeFull, string osVersion, string? codeName = null, string? id = null, bool stable = true)
		{
			if (versionCodeFull == null)
				throw new ArgumentNullException (nameof (versionCodeFull));
			if (osVersion == null)
				throw new ArgumentNullException (nameof (osVersion));

			ApiLevel                = versionCodeFull.Major;
			VersionCodeFull         = versionCodeFull;
			Id                      = id ?? (versionCodeFull.Minor != 0 ? versionCodeFull.ToString () : ApiLevel.ToString ());
			CodeName                = codeName;
			OSVersion               = osVersion;
			TargetFrameworkVersion  = Version.Parse (osVersion);
			FrameworkVersion        = "v" + osVersion;
			Stable                  = stable;

			Ids.Add (ApiLevel.ToString ());
			Ids.Add (VersionCodeFull.ToString ());
			Ids.Add (Id);
		}

		public override string ToString ()
		{
			return $"(AndroidVersion: ApiLevel={ApiLevel} VersionCodeFull={VersionCodeFull} Id={Id} OSVersion={OSVersion} CodeName='{CodeName}' TargetFrameworkVersion={TargetFrameworkVersion} Stable={Stable})";
		}

		public static AndroidVersion Load (Stream stream)
		{
			var doc = XDocument.Load (stream);
			return Load (doc);
		}

		public static AndroidVersion Load (string uri)
		{
			var doc = XDocument.Load (uri);
			return Load (doc);
		}

		// Example:
		// <AndroidApiInfo>
		//   <Id>26</Id>
		//   <Level>26</Level>
		//   <Name>Oreo</Name>
		//   <Version>v8.0</Version>
		//   <Stable>True</Stable>
		// </AndroidApiInfo>
		static AndroidVersion Load (XDocument doc)
		{
			var id      = (string?) doc.Root?.Element ("Id") ?? throw new InvalidOperationException ("Missing Id element");
			var level   = (int?) doc.Root?.Element ("Level") ?? throw new InvalidOperationException ("Missing Level element");
			var name    = (string?) doc.Root?.Element ("Name") ?? throw new InvalidOperationException ("Missing Name element");
			var version = (string?) doc.Root?.Element ("Version") ?? throw new InvalidOperationException ("Missing Version element");
			var stable  = (bool?) doc.Root?.Element ("Stable") ?? throw new InvalidOperationException ("Missing Stable element");
			var versionCodeFull = (string?) doc.Root?.Element ("VersionCodeFull");

			var fullLevel = string.IsNullOrWhiteSpace (versionCodeFull)
				? new Version (level, 0)
				: Version.Parse (versionCodeFull);

			return new AndroidVersion (fullLevel, version.TrimStart ('v'), name, id, stable);
		}
	}
}

