#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Sdk.TrimmableTypeMap;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Emits empty stub assemblies for per-assembly typemaps (<c>_X.TypeMap.dll</c>) that the root
/// <c>_Microsoft.Android.TypeMaps</c> assembly references via
/// <c>[assembly: TypeMapAssemblyTarget&lt;T&gt;("_X.TypeMap")]</c> but that ILLink trimmed away because
/// their target Java binding was unused.
///
/// Those attributes carry an opaque assembly-name string rather than a metadata reference, so ILLink
/// cannot follow (or prune) them, and at startup CoreCLR's <c>TypeMapping</c> enumerates every
/// attribute and calls <c>Assembly.Load</c> on the named assembly, throwing
/// <c>FileNotFoundException</c> for the trimmed ones. Emitting an empty, entry-free stub for each keeps
/// <c>Assembly.Load</c> succeeding (the stub contributes no type map entries) without editing the
/// linked root assembly, so ILLink's reconciled assembly references are preserved.
/// </summary>
public class GenerateMissingTypeMapStubs : AndroidTask
{
	public override string TaskPrefix => "GMTS";

	/// <summary>Directory holding every per-assembly typemap generated before trimming; defines the full referenced set.</summary>
	[Required]
	public string TypeMapDirectory { get; set; } = "";

	/// <summary>Directory holding the surviving (post-trim) assemblies, e.g. the <c>linked/</c> output. Stubs are written here.</summary>
	[Required]
	public string LinkedAssembliesDirectory { get; set; } = "";

	/// <summary>The root typemap assembly name to skip, e.g. <c>_Microsoft.Android.TypeMaps</c>.</summary>
	[Required]
	public string RootTypeMapAssemblyName { get; set; } = "";

	/// <summary>Used to derive the emitted assembly's System.Runtime reference version.</summary>
	[Required]
	public string TargetFrameworkVersion { get; set; } = "";

	[Output]
	public ITaskItem [] GeneratedStubs { get; set; } = [];

	public override bool RunTask ()
	{
		var stubs = new List<ITaskItem> ();
		if (!Directory.Exists (TypeMapDirectory) || !Directory.Exists (LinkedAssembliesDirectory)) {
			Log.LogDebugMessage ($"TypeMap directory '{TypeMapDirectory}' or linked directory '{LinkedAssembliesDirectory}' not found; skipping stub generation.");
			GeneratedStubs = stubs.ToArray ();
			return true;
		}

		var systemRuntimeVersion = ParseTargetFrameworkVersion (TargetFrameworkVersion);
		var generator = new TypeMapAssemblyGenerator (systemRuntimeVersion);

		foreach (var file in Directory.EnumerateFiles (TypeMapDirectory, "_*.TypeMap.dll")) {
			var name = Path.GetFileNameWithoutExtension (file);
			if (string.IsNullOrEmpty (name) || string.Equals (name, RootTypeMapAssemblyName, StringComparison.Ordinal))
				continue;
			var linkedPath = Path.Combine (LinkedAssembliesDirectory, name + ".dll");
			if (File.Exists (linkedPath))
				continue; // survived trimming; a real typemap already ships

			using var stream = new MemoryStream ();
			generator.GenerateEmpty (stream, name);
			Files.CopyIfStreamChanged (stream, linkedPath);
			Log.LogDebugMessage ($"Generated empty typemap stub for trimmed assembly '{name}'.");
			stubs.Add (new TaskItem (linkedPath));
		}

		if (stubs.Count > 0)
			Log.LogDebugMessage ($"Generated {stubs.Count} empty typemap stub(s) for trimmed per-assembly typemaps.");
		GeneratedStubs = stubs.ToArray ();
		return !Log.HasLoggedErrors;
	}

	static Version ParseTargetFrameworkVersion (string tfv)
	{
		if (tfv.Length > 0 && (tfv [0] == 'v' || tfv [0] == 'V')) {
			tfv = tfv.Substring (1);
		}
		if (Version.TryParse (tfv, out var version)) {
			return version;
		}
		throw new ArgumentException ($"Cannot parse TargetFrameworkVersion '{tfv}' as a Version.");
	}
}
