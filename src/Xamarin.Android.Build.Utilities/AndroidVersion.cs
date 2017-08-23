using System;

namespace Xamarin.Android.Build.Utilities
{
	public class AndroidVersion
	{
		public AndroidVersion (int apilevel, string osVersion)
		{
			this.ApiLevel = apilevel;
			this.OSVersion = osVersion;
		}

		internal AndroidVersion (int apilevel, string osVersion, string codeName, Version version, string frameworkVersion = null, bool stable = true)
		{
			this.ApiLevel = apilevel;
			this.Id = apilevel.ToString ();
			// TODO: remove osVersion from parameter list and generate from version
			this.OSVersion = osVersion;
			this.CodeName = codeName;
			this.Version = version;
			this.FrameworkVersion = frameworkVersion;
			this.Stable = stable;
		}

		public int ApiLevel { get; private set; }
		public string OSVersion { get; private set; }
		public string CodeName { get; private set; }
		public Version Version { get; private set; }
		public string FrameworkVersion { get; private set; }
		public string Id { get; internal set; }
		public bool Stable { get; private set; }

		internal string[] AlternateIds { get; set; }

		public override string ToString ()
		{
			return $"(AndroidVersion: ApiLevel={ApiLevel} OSVersion={OSVersion} CodeName='{CodeName}' Version={Version} FrameworkVersion={FrameworkVersion} Id={Id} Stable={Stable})";
		}
	}
}

