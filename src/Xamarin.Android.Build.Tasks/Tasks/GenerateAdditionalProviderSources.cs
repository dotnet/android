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

	public ITaskItem[]? Environments { get; set; }

	// We need to pass these two to the environment builder, otherwise not used
	// by this task. See also GenerateNativeApplicationSources.cs
	public string? HttpClientHandlerType { get; set; }
	public bool EnableSGenConcurrent { get; set; }

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
			GenerateJavaSource (
				"JavaInteropRuntime.java",
				new Dictionary<string, string> (StringComparer.Ordinal) {
					{ "@MAIN_ASSEMBLY_NAME@", TargetName },
				}
			);

			// We care only about environment variables here
			var envBuilder = new EnvironmentBuilder (Log);
			envBuilder.Read (Environments);
			GenerateNativeApplicationConfigSources.AddDefaultEnvironmentVariables (envBuilder, HttpClientHandlerType, EnableSGenConcurrent);

			var envVarNames = new StringBuilder ();
			var envVarValues = new StringBuilder ();
			foreach (var kvp in envBuilder.EnvironmentVariables) {
				// All the strings already have double-quotes properly quoted, EnvironmentBuilder took care of that
				AppendEnvVarEntry (envVarNames, kvp.Key);
				AppendEnvVarEntry (envVarValues, kvp.Value);
			}

			var envVars = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@ENVIRONMENT_VAR_NAMES@", envVarNames.ToString () },
				{ "@ENVIRONMENT_VAR_VALUES@", envVarValues.ToString () },
			};

			GenerateJavaSource (
				"NativeAotEnvironmentVars.java",
				envVars
			);
		}

		// Create additional application java sources.
		StringWriter regCallsWriter = new StringWriter ();

		// With TypeMap v3, native methods are registered via RegisterNatives in native code,
		// so we don't need the Java-side registerNativeMembers calls.
		// For older XAJavaInterop1 style, we still need to register via Runtime.register.
		if (codeGenerationTarget == JavaPeerStyle.XAJavaInterop1) {
			regCallsWriter.WriteLine ("// Application and Instrumentation ACWs must be registered first.");
			foreach ((string jniName, string assemblyQualifiedName) in codeGenState.ApplicationsAndInstrumentationsToRegister) {
				regCallsWriter.WriteLine (
					"\t\tmono.android.Runtime.register (\"{0}\", {1}.class, {1}.__md_methods);",
					assemblyQualifiedName,
					jniName
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

		void AppendEnvVarEntry (StringBuilder sb, string value)
		{
			sb.Append ("\t\t\"");
			sb.Append (value);
			sb.Append ("\",\n");
		}

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
