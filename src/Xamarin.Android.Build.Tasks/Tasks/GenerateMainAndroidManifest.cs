using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class GenerateMainAndroidManifest : AndroidTask
{
	public override string TaskPrefix => "GMM";

	[Output]
	public string []? AdditionalProviderSources { get; set; }
	[Required]
	public string AndroidRuntime { get; set; } = "";
	public string? AndroidSdkDir { get; set; }
	public string? AndroidSdkPlatform { get; set; }
	public string? ApplicationJavaClass { get; set; }
	public string? ApplicationLabel { get; set; }
	public string? BundledWearApplicationName { get; set; }
	public string? CheckedBuild { get; set; }
	public bool Debug { get; set; }
	public bool EmbedAssemblies { get; set; }
	public bool EnableNativeRuntimeLinking { get; set; }
	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";
	public string []? ManifestPlaceholders { get; set; }
	public string? ManifestTemplate { get; set; }
	public string? MergedAndroidManifestOutput { get; set; }
	public string []? MergedManifestDocuments { get; set; }
	public bool MultiDex { get; set; }
	public bool NeedsInternet { get; set; }
	public string? PackageName { get; set; }
	[Required]
	public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];
	[Required]
	public string [] SupportedAbis { get; set; } = [];
	public string? SupportedOSPlatformVersion { get; set; }
	public string? VersionCode { get; set; }
	public string? VersionName { get; set; }

	AndroidRuntime androidRuntime;

	public override bool RunTask ()
	{
		// Retrieve the stored NativeCodeGenState (and remove it from the cache)
		var nativeCodeGenStates = BuildEngine4.UnregisterTaskObjectAssemblyLocal<ConcurrentDictionary<AndroidTargetArch, NativeCodeGenState>> (
			MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
			RegisteredTaskObjectLifetime.Build
		);

		// We only need the first architecture, since this task is architecture-agnostic
		var templateCodeGenState = nativeCodeGenStates.First ().Value;

		var userAssembliesPerArch = MonoAndroidHelper.GetPerArchAssemblies (ResolvedUserAssemblies, SupportedAbis, validate: true);

		androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);

		// Generate the merged manifest
		var additionalProviders = MergeManifest (templateCodeGenState, GenerateJavaStubs.MaybeGetArchAssemblies (userAssembliesPerArch, templateCodeGenState.TargetArch));

		AdditionalProviderSources = additionalProviders.ToArray ();

		// We still need the NativeCodeGenState for later tasks, but we're going to transfer
		// it to a new object that doesn't require holding open Cecil AssemblyDefinitions.
		var nativeCodeGenStateObject = MarshalMethodCecilAdapter.GetNativeCodeGenStateCollection (Log, nativeCodeGenStates);

		Log.LogDebugMessage ($"Saving {nameof (NativeCodeGenStateObject)} to {nameof (GenerateJavaStubs.NativeCodeGenStateObjectRegisterTaskKey)}");
		BuildEngine4.RegisterTaskObjectAssemblyLocal (MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateObjectRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory), nativeCodeGenStateObject, RegisteredTaskObjectLifetime.Build);

		// Dispose the Cecil resolvers so the assemblies are closed.
		Log.LogDebugMessage ($"Disposing all {nameof (NativeCodeGenState)}.{nameof (NativeCodeGenState.Resolver)}");

		foreach (var state in nativeCodeGenStates.Values) {
			state.Resolver.Dispose ();
		}

		if (Log.HasLoggedErrors) {
			// Ensure that on a rebuild, we don't *skip* the `_GenerateJavaStubs` target,
			// by ensuring that the target outputs have been deleted.
			Files.DeleteFile (MergedAndroidManifestOutput, Log);
		}

		return !Log.HasLoggedErrors;
	}

	IList<string> MergeManifest (NativeCodeGenState codeGenState, Dictionary<string, ITaskItem> userAssemblies)
	{
		var manifest = new ManifestDocument (ManifestTemplate) {
			PackageName = PackageName,
			VersionName = VersionName,
			ApplicationLabel = ApplicationLabel ?? PackageName,
			Placeholders = ManifestPlaceholders,
			Resolver = codeGenState.Resolver,
			SdkDir = AndroidSdkDir,
			TargetSdkVersion = AndroidSdkPlatform,
			MinSdkVersion = MonoAndroidHelper.ConvertSupportedOSPlatformVersionToApiLevel (SupportedOSPlatformVersion).ToString (),
			Debug = Debug,
			MultiDex = MultiDex,
			NeedsInternet = NeedsInternet,
			AndroidRuntime = androidRuntime,
		};
		// Only set manifest.VersionCode if there is no existing value in AndroidManifest.xml.
		if (manifest.HasVersionCode) {
			Log.LogDebugMessage ($"Using existing versionCode in: {ManifestTemplate}");
		} else if (!string.IsNullOrEmpty (VersionCode)) {
			manifest.VersionCode = VersionCode;
		}
		manifest.Assemblies.AddRange (userAssemblies.Values.Select (item => item.ItemSpec));

		if (!String.IsNullOrWhiteSpace (CheckedBuild)) {
			// We don't validate CheckedBuild value here, this will be done in BuildApk. We just know that if it's
			// on then we need android:debuggable=true and android:extractNativeLibs=true
			manifest.ForceDebuggable = true;
			manifest.ForceExtractNativeLibs = true;
		}

		IList<string> additionalProviders = manifest.Merge (Log, codeGenState.TypeCache, codeGenState.AllJavaTypes, ApplicationJavaClass, EmbedAssemblies, BundledWearApplicationName, MergedManifestDocuments);

		// Only write the new manifest if it actually changed
		if (manifest.SaveIfChanged (Log, MergedAndroidManifestOutput)) {
			Log.LogDebugMessage ($"Saving: {MergedAndroidManifestOutput}");
		}

		return additionalProviders;
	}
}
