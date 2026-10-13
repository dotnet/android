#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

public class CollectCompressedAssemblyInfo : AndroidTask
{
	public override string TaskPrefix => "CCAI";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	public ITaskItem [] PackagedAssemblies { get; set; } = [];

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	[Required]
	public bool Debug { get; set; }

	[Required]
	public bool EnableCompression { get; set; }

	[Required]
	public string ProjectFullPath { get; set; } = "";

	public override bool RunTask ()
	{
		if (Debug || !EnableCompression) {
			return true;
		}

		var perArchAssemblies = MonoAndroidHelper.GetPerArchAssemblies (
			ResolvedAssemblies,
			SupportedAbis,
			validate: true,
			shouldSkip: assembly => bool.TryParse (assembly.GetMetadata ("AndroidSkipAddToPackage"), out bool skip) && skip
		);
		var packagedAssemblies = MonoAndroidHelper.GetPerArchAssemblies (
			PackagedAssemblies,
			SupportedAbis,
			validate: true,
			shouldSkip: assembly => bool.TryParse (assembly.GetMetadata ("AndroidSkipAddToPackage"), out bool skip) && skip
		);
		// Crossgen2 can add a composite image which has no pre-trimming input assembly.
		// Append these generated identities without changing any pre-trimming indices.
		foreach (var arch in packagedAssemblies) {
			if (!perArchAssemblies.TryGetValue (arch.Key, out var assemblies)) {
				assemblies = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
				perArchAssemblies.Add (arch.Key, assemblies);
			}
			foreach (var assembly in arch.Value) {
				assemblies.TryAdd (assembly.Key, assembly.Value);
			}
		}
		var archAssemblies = new Dictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> ();

		foreach (var kvpPerArch in perArchAssemblies) {
			var assemblies = new Dictionary<string, CompressedAssemblyInfo> (StringComparer.OrdinalIgnoreCase);
			archAssemblies.Add (kvpPerArch.Key, assemblies);
			uint counter = 0;

			foreach (var assembly in kvpPerArch.Value.Values) {
				string assemblyKey = CompressedAssemblyInfo.GetDictionaryKey (assembly);
				if (assemblies.ContainsKey (assemblyKey)) {
					Log.LogDebugMessage ($"Skipping duplicate assembly: {assembly.ItemSpec} (arch {MonoAndroidHelper.GetAssemblyAbi (assembly)})");
					continue;
				}

				if (!File.Exists (assembly.ItemSpec)) {
					Log.LogCodedError ("XA2025", Properties.Resources.XA2025, assembly.ItemSpec);
					continue;
				}

				assemblies.Add (assemblyKey, new CompressedAssemblyInfo (counter));
				counter = checked (counter + 1);
			}
		}

		string key = CompressedAssemblyInfo.GetKey (ProjectFullPath);
		Log.LogDebugMessage ($"Storing compression assemblies info with key '{key}'");
		BuildEngine4.RegisterTaskObjectAssemblyLocal (key, archAssemblies, RegisteredTaskObjectLifetime.Build);
		return !Log.HasLoggedErrors;
	}
}
