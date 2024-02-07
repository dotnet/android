using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	class AndroidToolchainComponent : AppObject, IBuildInventoryItem
	{
		public string Name                { get; }
		public string DestDir             { get; }
		public Uri? RelativeUrl           { get; }
		public bool IsMultiVersion        { get; }
		public bool NoSubdirectory        { get; }
		public string? PkgRevision        { get; }
		public AndroidToolchainComponentType DependencyType { get; }
		public string BuildToolName       { get; }
		public string BuildToolVersion    { get; }

		public AndroidToolchainComponent (string name, string destDir, Uri? relativeUrl = null, bool isMultiVersion = false, bool noSubdirectory = false, string? pkgRevision = null,
			AndroidToolchainComponentType dependencyType = AndroidToolchainComponentType.CoreDependency, string buildToolName = "", string buildToolVersion = "")
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("must not be null or empty", nameof (name));
			if (String.IsNullOrEmpty (destDir))
				throw new ArgumentException ("must not be null or empty", nameof (destDir));

			Name = name;
			DestDir = destDir;
			RelativeUrl = relativeUrl;
			IsMultiVersion = isMultiVersion;
			NoSubdirectory = noSubdirectory;
			PkgRevision = pkgRevision;
			DependencyType = dependencyType;
			BuildToolName = string.IsNullOrEmpty (buildToolName) ? $"android-sdk-{name}" : buildToolName;
			BuildToolVersion = buildToolVersion;
		}

		public void AddToInventory ()
		{
			if (!string.IsNullOrEmpty (BuildToolName) && !string.IsNullOrEmpty (BuildToolVersion) && !Context.Instance.BuildToolsInventory.ContainsKey (BuildToolName)) {
				Context.Instance.BuildToolsInventory.Add (BuildToolName, BuildToolVersion);
			}
		}
	}

	class AndroidPlatformComponent : AndroidToolchainComponent
	{
		public string ApiLevel { get; }
		public bool IsLatestStable { get; }
		public bool IsPreview { get; }

		public AndroidPlatformComponent (string name, string apiLevel, string pkgRevision, bool isLatestStable = false, bool isPreview = false)
			: base (name, Path.Combine ("platforms", $"android-{apiLevel}"), pkgRevision: pkgRevision, buildToolName: $"android-sdk-{name}", buildToolVersion: $"{apiLevel}.{pkgRevision}")
		{
			ApiLevel = apiLevel;
			IsLatestStable = isLatestStable;
			IsPreview = isPreview;
		}
	}

	[Flags]
	enum AndroidToolchainComponentType
	{
		CoreDependency          = 0,
		BuildDependency         = 1 << 0,
		EmulatorDependency      = 1 << 1,
		All                     = CoreDependency | BuildDependency | EmulatorDependency,
	}

}
