#nullable enable
using System;
using System.IO;
using System.Linq;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public sealed class GenerateAdditionalProviderSources : AndroidTask
{
	public override string TaskPrefix => "GPS";

	[Required]
	public string [] AdditionalProviderSources { get; set; } = [];

	[Required]
	public string AndroidRuntime { get; set; } = "";

	public string CodeGenerationTarget { get; set; } = "";

	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public string TargetName { get; set; } = "";

	public ITaskItem[]? Environments { get; set; }

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

		if (nativeCodeGenStates is null)
			throw new InvalidOperationException ($"Internal error: {nameof (NativeCodeGenStateCollection)} not found");

		// We only need the first architecture, since this task is architecture-agnostic
		var templateCodeGenState = nativeCodeGenStates.States.First ().Value;

		Generate (templateCodeGenState);

		return !Log.HasLoggedErrors;
	}

	void Generate (NativeCodeGenStateObject codeGenState)
	{
		// Create additional runtime provider java sources.
		bool isCoreCLR = androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.CoreCLR;

		RuntimeProviderSourceGenerator.WriteAdditionalRuntimeProviderSources (OutputDirectory, isCoreCLR, AdditionalProviderSources);

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

	}

	void SaveResource (string resource, string filename, string destDir, Func<string, string> applyTemplate)
	{
		string template = RuntimeProviderSourceGenerator.ReadResource (resource);
		template = applyTemplate (template);
		Files.CopyIfStringChanged (template, Path.Combine (destDir, filename));
	}

}
