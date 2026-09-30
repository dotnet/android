#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Tasks;

internal static class GenerateAdditionalProviderSources
{
	static string GetResource (string resource)
	{
		using var stream = typeof (GenerateAdditionalProviderSources).Assembly.GetManifestResourceStream (resource)
			?? throw new InvalidOperationException ($"Java bootstrap resource '{resource}' was not found.");
		using var reader = new StreamReader (stream);
		return reader.ReadToEnd ();
	}

	/// <summary>
	/// Writes the additional per-process runtime provider Java sources (e.g. NativeAotRuntimeProvider_1.java)
	/// by cloning the runtime provider template for each name.
	/// </summary>
	internal static void WriteAdditionalRuntimeProviderSources (string outputDirectory, bool isCoreCLR, string [] additionalProviderSources)
	{
		if (additionalProviderSources.Length == 0) {
			return;
		}
		string providerTemplateFile = isCoreCLR ?
			"MonoRuntimeProvider.Bundled.java" :
			"NativeAotRuntimeProvider.java";
		string providerTemplate = GetResource (providerTemplateFile);
		foreach (var provider in additionalProviderSources) {
			var contents = providerTemplate.Replace (isCoreCLR ? "MonoRuntimeProvider" : "NativeAotRuntimeProvider", provider);
			var realProvider = isCoreCLR ?
				Path.Combine (outputDirectory, "src", "mono", provider + ".java") :
				Path.Combine (outputDirectory, "src", "net", "dot", "jni", "nativeaot", provider + ".java");
			Files.CopyIfStringChanged (contents, realProvider);
		}
	}

	/// <summary>
	/// Generates JavaInteropRuntime.java and the shared AppBootstrapConfig.java for NativeAOT apps.
	/// </summary>
	internal static void GenerateNativeAotBootstrapFiles (
		Microsoft.Build.Utilities.TaskLoggingHelper log,
		string outputDirectory,
		string targetName,
		ITaskItem []? environments)
	{
		GenerateJavaSource (
			"JavaInteropRuntime.java",
			new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@MAIN_ASSEMBLY_NAME@", targetName },
			}
		);

		GenerateJavaApplicationConfig.WriteNativeAotSource (outputDirectory, environments);
		string obsoleteEnvironment = Path.Combine (outputDirectory, "src", "net", "dot", "jni", "nativeaot", "NativeAotEnvironmentVars.java");
		if (File.Exists (obsoleteEnvironment)) {
			File.Delete (obsoleteEnvironment);
		}

		void GenerateJavaSource (string fileName, Dictionary<string, string> replacements)
		{
			var template = new StringBuilder (GetResource (fileName));

			foreach (var kvp in replacements) {
				template.Replace (kvp.Key, kvp.Value);
			}

			var outputDir = Path.Combine (outputDirectory, "src", "net", "dot", "jni", "nativeaot");
			Directory.CreateDirectory (outputDir);
			var path = Path.Combine (outputDir, fileName);
			log.LogDebugMessage ($"Writing: {path}");
			Files.CopyIfStringChanged (template.ToString (), path);
		}
	}
}
