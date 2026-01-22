#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public class GenerateAdditionalProviderSources : AndroidTask
{
	public override string TaskPrefix => "GPS";

	[Required]
	public string [] AdditionalProviderSources { get; set; } = [];

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

		foreach (var provider in AdditionalProviderSources) {
			var contents = providerTemplate.Replace (isMonoVM ? "MonoRuntimeProvider" : "NativeAotRuntimeProvider", provider);
			var real_provider = isMonoVM ?
				Path.Combine (OutputDirectory, "src", "mono", provider + ".java") :
				Path.Combine (OutputDirectory, "src", "net", "dot", "jni", "nativeaot", provider + ".java");
			Files.CopyIfStringChanged (contents, real_provider);
		}

		// For NativeAOT, generate JavaInteropRuntime.java and NativeAotEnvironmentVars.java
		if (androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.NativeAOT) {
			const string fileName = "JavaInteropRuntime.java";
			GenerateJavaSource (
				"JavaInteropRuntime.java",
				new Dictionary<string, string> (StringComparer.Ordinal) {
					{ "@MAIN_ASSEMBLY_NAME@", TargetName },
				}
			);

			// TODO: actually put envvars here
			GenerateJavaSource (
				"NativeAotEnvironmentVars.java",
				new Dictionary<string, string> (StringComparer.Ordinal) {
				}
			);
		}

		// Create additional application java sources.
		StringWriter regCallsWriter = new StringWriter ();
		regCallsWriter.WriteLine ("// Application and Instrumentation ACWs must be registered first.");

		foreach ((string jniName, string assemblyQualifiedName) in codeGenState.ApplicationsAndInstrumentationsToRegister) {
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

		void GenerateJavaSource (string fileName, Dictionary<string, string> replacements)
		{
			var template = new StringBuilder (GetResource (fileName));

			foreach (var kvp in replacements) {
				template.Replace (kvp.Key, kvp.Value);
			}

			var path = Path.Combine (OutputDirectory, "src", "net", "dot", "jni", "nativeaot", fileName);
			Log.LogDebugMessage ($"Writing: {path}");
			Files.CopyIfStringChanged (template.ToString (), path);
		}
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
