#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Tasks;

[MSBuildMultiThreadableTask]
public sealed class GenerateCoreClrRuntimeProviderSources : MultithreadedAndroidTask
{
	public override string TaskPrefix => "GCRP";

	public string [] AdditionalProviderSources { get; set; } = [];

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Output]
	public string [] DeletedSources { get; set; } = [];

	public override bool RunTask ()
	{
		RuntimeProviderSourceGenerator.WriteAdditionalRuntimeProviderSources (
			TaskEnvironment, OutputDirectory, isCoreCLR: true, AdditionalProviderSources);
		var selected = new HashSet<string> (AdditionalProviderSources, StringComparer.Ordinal);
		var deleted = new List<string> ();
		string directory = TaskEnvironment.GetAbsolutePath (Path.Combine (OutputDirectory, "src", "mono"));
		if (Directory.Exists (directory)) {
			foreach (string path in Directory.EnumerateFiles (directory, "MonoRuntimeProvider_*.java")) {
				string name = Path.GetFileNameWithoutExtension (path);
				if (Regex.IsMatch (name, @"^MonoRuntimeProvider_[0-9]+$") && !selected.Contains (name)) {
					File.Delete (TaskEnvironment.GetAbsolutePath (path));
					deleted.Add (path);
				}
			}
		}
		DeletedSources = deleted.ToArray ();
		return !Log.HasLoggedErrors;
	}
}
