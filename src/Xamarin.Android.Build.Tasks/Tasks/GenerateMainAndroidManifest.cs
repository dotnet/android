using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class GenerateMainAndroidManifest : AndroidTask
{
	public override string TaskPrefix => "GMM";

	[Required]
	public string AndroidRuntime { get; set; } = "";
	public string? AndroidSdkDir { get; set; }
	public string? AndroidSdkPlatform { get; set; }
	public string? ApplicationJavaClass { get; set; }
	public string? ApplicationLabel { get; set; }
	public string? BundledWearApplicationName { get; set; }
	public string? CheckedBuild { get; set; }
	public string CodeGenerationTarget { get; set; } = "";
	public bool Debug { get; set; }
	public bool EmbedAssemblies { get; set; }
	public bool EnableMarshalMethods { get; set; }
	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";
	public string []? ManifestPlaceholders { get; set; }
	public string? ManifestTemplate { get; set; }
	public string? MergedAndroidManifestOutput { get; set; }
	public string []? MergedManifestDocuments { get; set; }
	public bool MultiDex { get; set; }
	public bool NeedsInternet { get; set; }
	public string? OutputDirectory { get; set; }
	public string? PackageName { get; set; }
	[Required]
	public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];
	[Required]
	public string [] SupportedAbis { get; set; } = [];
	public string? SupportedOSPlatformVersion { get; set; }
	[Required]
	public string TargetName { get; set; } = "";
	public string? VersionCode { get; set; }
	public string? VersionName { get; set; }

	AndroidRuntime androidRuntime;
	JavaPeerStyle codeGenerationTarget;

	bool UseMarshalMethods => !Debug && EnableMarshalMethods;

	public override bool RunTask ()
	{
		// Retrieve the stored NativeCodeGenState
		var nativeCodeGenStates = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<ConcurrentDictionary<AndroidTargetArch, NativeCodeGenState>> (
			MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
			RegisteredTaskObjectLifetime.Build
		);

		// We only need the first architecture, since this task is architecture-agnostic
		var templateCodeGenState = nativeCodeGenStates.First ().Value;

		var userAssembliesPerArch = MonoAndroidHelper.GetPerArchAssemblies (ResolvedUserAssemblies, SupportedAbis, validate: true);

		androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);
		codeGenerationTarget = MonoAndroidHelper.ParseCodeGenerationTarget (CodeGenerationTarget);

		// Generate the merged manifest
		var additionalProviders = MergeManifest (templateCodeGenState, GenerateJavaStubs.MaybeGetArchAssemblies (userAssembliesPerArch, templateCodeGenState.TargetArch));
		GenerateAdditionalProviderSources (templateCodeGenState, additionalProviders);

		// Marshal methods needs this data in the <GeneratePackageManagerJava/> later,
		// but if we're not using marshal methods we need to dispose of the resolver.
		if (!UseMarshalMethods) {
			Log.LogDebugMessage ($"Disposing all {nameof (NativeCodeGenState)}.{nameof (NativeCodeGenState.Resolver)}");

			foreach (var state in nativeCodeGenStates.Values) {
				state.Resolver.Dispose ();
			}
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

	void GenerateAdditionalProviderSources (NativeCodeGenState codeGenState, IList<string> additionalProviders)
	{
		if (androidRuntime != Xamarin.Android.Tasks.AndroidRuntime.CoreCLR) {
			// Create additional runtime provider java sources.
			bool isMonoVM = androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.MonoVM;
			string providerTemplateFile = isMonoVM ?
				"MonoRuntimeProvider.Bundled.java" :
				"NativeAotRuntimeProvider.java";
			string providerTemplate = GetResource (providerTemplateFile);

			foreach (var provider in additionalProviders) {
				var contents = providerTemplate.Replace (isMonoVM ? "MonoRuntimeProvider" : "NativeAotRuntimeProvider", provider);
				var real_provider = isMonoVM ?
					Path.Combine (OutputDirectory, "src", "mono", provider + ".java") :
					Path.Combine (OutputDirectory, "src", "net", "dot", "jni", "nativeaot", provider + ".java");
				Files.CopyIfStringChanged (contents, real_provider);
			}
		} else {
			Log.LogDebugMessage ($"Skipping android.content.ContentProvider generation for: {androidRuntime}");
		}

		// For NativeAOT, generate JavaInteropRuntime.java
		if (androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.NativeAOT) {
			const string fileName = "JavaInteropRuntime.java";
			string template = GetResource (fileName);
			var contents = template.Replace ("@MAIN_ASSEMBLY_NAME@", TargetName);
			var path = Path.Combine (OutputDirectory, "src", "net", "dot", "jni", "nativeaot", fileName);
			Log.LogDebugMessage ($"Writing: {path}");
			Files.CopyIfStringChanged (contents, path);
		}

		// Create additional application java sources.
		StringWriter regCallsWriter = new StringWriter ();
		regCallsWriter.WriteLine ("// Application and Instrumentation ACWs must be registered first.");
		foreach (TypeDefinition type in codeGenState.JavaTypesForJCW) {
			if (JavaNativeTypeManager.IsApplication (type, codeGenState.TypeCache) || JavaNativeTypeManager.IsInstrumentation (type, codeGenState.TypeCache)) {
				if (codeGenState.Classifier != null && !codeGenState.Classifier.FoundDynamicallyRegisteredMethods (type)) {
					continue;
				}

				string javaKey = JavaNativeTypeManager.ToJniName (type, codeGenState.TypeCache).Replace ('/', '.');
				regCallsWriter.WriteLine (
					codeGenerationTarget == JavaPeerStyle.XAJavaInterop1 ?
						"\t\tmono.android.Runtime.register (\"{0}\", {1}.class, {1}.__md_methods);" :
						"\t\tnet.dot.jni.ManagedPeer.registerNativeMembers ({1}.class, {1}.__md_methods);",
					type.GetAssemblyQualifiedName (codeGenState.TypeCache),
					javaKey
				);
			}
		}
		regCallsWriter.Close ();

		var real_app_dir = Path.Combine (OutputDirectory, "src", "net", "dot", "android");
		string applicationTemplateFile = "ApplicationRegistration.java";
		SaveResource (
			applicationTemplateFile,
			applicationTemplateFile,
			real_app_dir,
			template => template.Replace ("// REGISTER_APPLICATION_AND_INSTRUMENTATION_CLASSES_HERE", regCallsWriter.ToString ())
		);
	}

	string GetResource (string resource)
	{
		using (var stream = GetType ().Assembly.GetManifestResourceStream (resource))
		using (var reader = new StreamReader (stream))
			return reader.ReadToEnd ();
	}

	void SaveResource (string resource, string filename, string destDir, Func<string, string> applyTemplate)
	{
		string template = GetResource (resource);
		template = applyTemplate (template);
		Files.CopyIfStringChanged (template, Path.Combine (destDir, filename));
	}
}
