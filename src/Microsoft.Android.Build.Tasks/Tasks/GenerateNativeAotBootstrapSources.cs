#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Tasks;

[MSBuildMultiThreadableTask]
public sealed class GenerateNativeAotBootstrapSources : MultithreadedAndroidTask
{
	public override string TaskPrefix => "GNABS";

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public string TargetName { get; set; } = "";

	public string [] AdditionalProviderSources { get; set; } = [];

	[Output]
	public string [] GeneratedSources { get; set; } = [];

	public override bool RunTask ()
	{
		string outputDirectory = Path.Combine (OutputDirectory, "src", "net", "dot", "jni", "nativeaot");
		string absoluteOutputDirectory = TaskEnvironment.GetAbsolutePath (outputDirectory);
		Directory.CreateDirectory (absoluteOutputDirectory);
		File.Delete (Path.Combine (absoluteOutputDirectory, "NativeAotEnvironmentVars.java"));
		var sources = new List<string> {
			WriteSource ("JavaInteropRuntime.java", ("@MAIN_ASSEMBLY_NAME@", EscapeJavaString (TargetName))),
		};
		sources.AddRange (RuntimeProviderSourceGenerator.WriteAdditionalRuntimeProviderSources (
			TaskEnvironment, OutputDirectory, isCoreCLR: false, AdditionalProviderSources, preserveTimestamp: false));
		var selected = new HashSet<string> (sources.ConvertAll (path => TaskEnvironment.GetAbsolutePath (path).ToString ()), StringComparer.Ordinal);
		foreach (string oldProvider in Directory.GetFiles (absoluteOutputDirectory, "NativeAotRuntimeProvider_*.java")) {
			if (Regex.IsMatch (Path.GetFileName (oldProvider), @"^NativeAotRuntimeProvider_[0-9]+\.java$") && !selected.Contains (oldProvider)) {
				File.Delete (TaskEnvironment.GetAbsolutePath (oldProvider));
			}
		}
		GeneratedSources = sources.ToArray ();
		return !Log.HasLoggedErrors;

		string WriteSource (string name, params (string Token, string Value) [] replacements)
		{
			var replacementValues = new Dictionary<string, string> (StringComparer.Ordinal);
			foreach (var replacement in replacements) {
				replacementValues.Add (replacement.Token, replacement.Value);
			}
			string source = Regex.Replace (
				RuntimeProviderSourceGenerator.ReadResource (name),
				"@MAIN_ASSEMBLY_NAME@",
				match => replacementValues [match.Value]);
			string path = Path.Combine (outputDirectory, name);
			Log.LogDebugMessage ($"Writing: {path}");
			File.WriteAllText (TaskEnvironment.GetAbsolutePath (path), source, new UTF8Encoding (false));
			return path;
		}
	}

	internal static string EscapeJavaString (string value)
	{
		var result = new StringBuilder (value.Length);
		foreach (char character in value) {
			switch (character) {
				case '\\': result.Append ("\\\\"); break;
				case '"': result.Append ("\\\""); break;
				case '\b': result.Append ("\\b"); break;
				case '\t': result.Append ("\\t"); break;
				case '\n': result.Append ("\\n"); break;
				case '\f': result.Append ("\\f"); break;
				case '\r': result.Append ("\\r"); break;
				default:
					if (character < ' ' || character > '~') {
						result.Append ("\\u");
						result.Append (((int) character).ToString ("x4", CultureInfo.InvariantCulture));
					} else {
						result.Append (character);
					}
					break;
			}
		}
		return result.ToString ();
	}
}
