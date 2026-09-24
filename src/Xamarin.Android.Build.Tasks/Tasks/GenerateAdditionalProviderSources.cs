#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

internal static class GenerateAdditionalProviderSources
{
	static string GetResource (string resource)
	{
		using (var stream = typeof (GenerateAdditionalProviderSources).Assembly.GetManifestResourceStream (resource))
		using (var reader = new StreamReader (stream))
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
	/// Generates JavaInteropRuntime.java and NativeAotEnvironmentVars.java for NativeAOT apps.
	/// </summary>
	internal static void GenerateNativeAotBootstrapFiles (
		Microsoft.Build.Utilities.TaskLoggingHelper log,
		string outputDirectory,
		string targetName,
		ITaskItem []? environments,
		bool enableSGenConcurrent)
	{
		GenerateJavaSource (
			"JavaInteropRuntime.java",
			new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@MAIN_ASSEMBLY_NAME@", targetName },
			}
		);

		// We care only about environment variables here
		var envBuilder = new EnvironmentBuilder (log);
		envBuilder.Read (environments);
		GenerateNativeApplicationConfigSources.AddDefaultEnvironmentVariables (envBuilder, enableSGenConcurrent);

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

			var outputDir = Path.Combine (outputDirectory, "src", "net", "dot", "jni", "nativeaot");
			Directory.CreateDirectory (outputDir);
			var path = Path.Combine (outputDir, fileName);
			log.LogDebugMessage ($"Writing: {path}");
			Files.CopyIfStringChanged (template.ToString (), path);
		}
	}
}
