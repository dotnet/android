#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task adapter for <see cref="TrimmableTypeMapGenerator"/>.
/// Opens files and maps ITaskItem to/from strings, then delegates to the core class.
/// </summary>
public class GenerateTrimmableTypeMap : AndroidTask
{
	public override string TaskPrefix => "GTT";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public string JavaSourceOutputDirectory { get; set; } = "";

	[Required]
	public string AcwMapDirectory { get; set; } = "";

	/// <summary>
	/// Output path for the merged acw-map.txt consumed by _ConvertCustomView and _UpdateAndroidResgen.
	/// </summary>
	public string? AcwMapOutputFile { get; set; }

	/// <summary>
	/// The .NET target framework version (e.g., "v11.0"). Used to set the System.Runtime
	/// assembly reference version in generated typemap assemblies.
	/// </summary>
	[Required]
	public string TargetFrameworkVersion { get; set; } = "";

	/// <summary>
	/// User's AndroidManifest.xml template. May be null if no template exists.
	/// </summary>
	public string? ManifestTemplate { get; set; }

	/// <summary>
	/// Output path for the merged AndroidManifest.xml.
	/// </summary>
	public string? MergedAndroidManifestOutput { get; set; }

	/// <summary>
	/// Android package name (e.g., "com.example.myapp").
	/// </summary>
	public string? PackageName { get; set; }

	/// <summary>
	/// Application label for the manifest.
	/// </summary>
	public string? ApplicationLabel { get; set; }

	public string? VersionCode { get; set; }

	public string? VersionName { get; set; }

	/// <summary>
	/// Target Android API level (e.g., "36").
	/// </summary>
	public string? AndroidApiLevel { get; set; }

	/// <summary>
	/// Supported OS platform version (e.g., "21.0").
	/// </summary>
	public string? SupportedOSPlatformVersion { get; set; }

	/// <summary>
	/// Android runtime type ("mono", "coreclr", "nativeaot").
	/// </summary>
	public string? AndroidRuntime { get; set; }

	public bool Debug { get; set; }

	public bool NeedsInternet { get; set; }

	public bool EmbedAssemblies { get; set; }

	/// <summary>
	/// Manifest placeholder values (e.g., "applicationId=com.example.app;key=value").
	/// </summary>
	public string? ManifestPlaceholders { get; set; }

	/// <summary>
	/// When set, forces android:debuggable="true" and android:extractNativeLibs="true".
	/// </summary>
	public string? CheckedBuild { get; set; }

	/// <summary>
	/// Optional custom Application Java class name.
	/// </summary>
	public string? ApplicationJavaClass { get; set; }

	[Output]
	public ITaskItem []? GeneratedAssemblies { get; set; }

	[Output]
	public ITaskItem []? GeneratedJavaFiles { get; set; }

	/// <summary>
	/// Content provider names for ApplicationRegistration.java.
	/// </summary>
	[Output]
	public string []? AdditionalProviderSources { get; set; }

	public override bool RunTask ()
	{
		var systemRuntimeVersion = TrimmableTypeMapGenerator.ParseTargetFrameworkVersion (TargetFrameworkVersion);
		// Don't filter by HasMonoAndroidReference — ReferencePath items from the compiler
		// don't carry this metadata. The scanner handles non-Java assemblies gracefully.
		var assemblyPaths = ResolvedAssemblies.Select (i => i.ItemSpec).Distinct ().ToList ();

		// Framework binding types (Activity, View, etc.) already exist in java_runtime.dex and don't
		// need JCW .java files. Framework Implementor types (mono/ prefix, e.g. OnClickListenerImplementor)
		// DO need JCWs — they're included via the mono/ filter below.
		// User NuGet libraries also need JCWs, so we only filter by FrameworkReferenceName.
		// Note: Pre-generating SDK-compatible JCWs (mono.android-trimmable.jar) is tracked by #10792.
		var frameworkAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		foreach (var item in ResolvedAssemblies) {
			if (!item.GetMetadata ("FrameworkReferenceName").IsNullOrEmpty ()) {
				frameworkAssemblyNames.Add (Path.GetFileNameWithoutExtension (item.ItemSpec));
			}
		}

		Directory.CreateDirectory (AcwMapDirectory);

		ManifestConfig? manifestConfig = null;
		if (!MergedAndroidManifestOutput.IsNullOrEmpty () && !PackageName.IsNullOrEmpty ()) {
			manifestConfig = new ManifestConfig (
				PackageName: PackageName,
				ApplicationLabel: ApplicationLabel,
				VersionCode: VersionCode,
				VersionName: VersionName,
				AndroidApiLevel: AndroidApiLevel,
				SupportedOSPlatformVersion: SupportedOSPlatformVersion,
				AndroidRuntime: AndroidRuntime,
				Debug: Debug,
				NeedsInternet: NeedsInternet,
				EmbedAssemblies: EmbedAssemblies,
				ManifestPlaceholders: ManifestPlaceholders,
				CheckedBuild: CheckedBuild,
				ApplicationJavaClass: ApplicationJavaClass);
		}

		var generator = new TrimmableTypeMapGenerator (Log);
		var result = generator.Execute (
			assemblyPaths,
			OutputDirectory,
			JavaSourceOutputDirectory,
			systemRuntimeVersion,
			frameworkAssemblyNames,
			manifestConfig,
			ManifestTemplate,
			MergedAndroidManifestOutput,
			AcwMapOutputFile);

		GeneratedAssemblies = result.GeneratedAssemblies.Select (p => (ITaskItem) new TaskItem (p)).ToArray ();
		GeneratedJavaFiles = result.GeneratedJavaFiles.Select (p => (ITaskItem) new TaskItem (p)).ToArray ();
		AdditionalProviderSources = result.AdditionalProviderSources;

		return !Log.HasLoggedErrors;
	}
}
