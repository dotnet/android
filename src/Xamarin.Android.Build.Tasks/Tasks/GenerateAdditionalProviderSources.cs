using System;
using System.IO;
using System.Linq;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public class GenerateAdditionalProviderSources : AndroidTask
{
	public override string TaskPrefix => "GPS";

	[Required]
	public ITaskItem [] AdditionalProviderSources { get; set; } = [];

	[Required]
	public string AndroidRuntime { get; set; } = "";

	public string CodeGenerationTarget { get; set; } = "";

	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";

	public string? OutputDirectory { get; set; }

	[Required]
	public string TargetName { get; set; } = "";

	AndroidRuntime androidRuntime;
	JavaPeerStyle codeGenerationTarget;

	public override bool RunTask ()
	{
		androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);
		codeGenerationTarget = MonoAndroidHelper.ParseCodeGenerationTarget (CodeGenerationTarget);

		// Retrieve the stored NativeCodeGenStateObject
		var nativeCodeGenStates = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<NativeCodeGenStateCollection> (
			MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateObjectRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
			RegisteredTaskObjectLifetime.Build
		);

		// We only need the first architecture, since this task is architecture-agnostic
		var templateCodeGenState = nativeCodeGenStates.States.First ().Value;

		Generate (templateCodeGenState);

		return !Log.HasLoggedErrors;
	}

	void Generate (NativeCodeGenStateObject codeGenState)
	{
		var additionalProviders = AdditionalProviderSources.Select (p => p.ItemSpec).ToList ();

		// Create additional runtime provider java sources.
		bool isMonoVM = androidRuntime switch {
			Xamarin.Android.Tasks.AndroidRuntime.MonoVM => true,
			Xamarin.Android.Tasks.AndroidRuntime.CoreCLR => true,
			_ => false,
		};
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

		foreach ((string jniName, string assemblyQualifiedName) in codeGenState.ApplicationsAndInstrumentationsToReigster) {
			regCallsWriter.WriteLine (
				codeGenerationTarget == JavaPeerStyle.XAJavaInterop1 ?
					"\t\tmono.android.Runtime.register (\"{0}\", {1}.class, {1}.__md_methods);" :
					"\t\tnet.dot.jni.ManagedPeer.registerNativeMembers ({1}.class, {1}.__md_methods);",
				assemblyQualifiedName,
				jniName
			);
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
