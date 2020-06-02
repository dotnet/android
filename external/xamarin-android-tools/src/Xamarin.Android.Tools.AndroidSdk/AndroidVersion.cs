using System;
using System.IO;
using System.Xml.Linq;

namespace Xamarin.Android.Tools
{
	public class AndroidVersion
	{
		// Android API Level. *Usually* corresponds to $(AndroidSdkPath)/platforms/android-$(ApiLevel)/android.jar
		public  int             ApiLevel                { get; private set; }

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

		// Alternate Ids for a given API level. Allows for historical mapping, e.g. API-11 has alternate ID 'H'.
		internal    string[]?   AlternateIds            { get; set; }

		public AndroidVersion (int apiLevel, string osVersion, string? codeName = null, string? id = null, bool stable = true)
		{
			if (osVersion == null)
				throw new ArgumentNullException (nameof (osVersion));

			ApiLevel                = apiLevel;
			Id                      = id ?? ApiLevel.ToString ();
			CodeName                = codeName;
			OSVersion               = osVersion;
			TargetFrameworkVersion  = Version.Parse (osVersion);
			FrameworkVersion        = "v" + osVersion;
			Stable                  = stable;
		}

		public override string ToString ()
		{
			return $"(AndroidVersion: ApiLevel={ApiLevel} Id={Id} OSVersion={OSVersion} CodeName='{CodeName}' TargetFrameworkVersion={TargetFrameworkVersion} Stable={Stable})";
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
			var id      = (string) doc.Root.Element ("Id");
			var level   = (int) doc.Root.Element ("Level");
			var name    = (string) doc.Root.Element ("Name");
			var version = (string) doc.Root.Element ("Version");
			var stable  = (bool) doc.Root.Element ("Stable");

			return new AndroidVersion (level, version.TrimStart ('v'), name, id, stable);
		}
	}
}

