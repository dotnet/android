#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks;

public sealed class GenerateNativeAotBootstrapSources : AndroidTask
{
	public override string TaskPrefix => "GNABS";

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public string TargetName { get; set; } = "";

	public ITaskItem []? Environments { get; set; }

	public string [] AdditionalProviderSources { get; set; } = [];

	[Output]
	public string [] GeneratedSources { get; set; } = [];

	public override bool RunTask ()
	{
		var environment = new EnvironmentBuilder (escapeValues: false);
		environment.Read (Environments);

		var names = new StringBuilder ();
		var values = new StringBuilder ();
		foreach (var variable in environment.EnvironmentVariables) {
			AppendString (names, variable.Key);
			AppendString (values, variable.Value);
		}
		var properties = new StringBuilder ();
		foreach (var property in environment.SystemProperties) {
			AppendString (properties, property.Key);
			AppendString (properties, property.Value);
		}

		string outputDirectory = Path.Combine (OutputDirectory, "src", "net", "dot", "jni", "nativeaot");
		Directory.CreateDirectory (outputDirectory);
		var sources = new List<string> {
			WriteSource ("JavaInteropRuntime.java", ("@MAIN_ASSEMBLY_NAME@", EscapeJavaString (TargetName))),
			WriteSource ("NativeAotEnvironmentVars.java",
				("@ENVIRONMENT_VAR_NAMES@", names.ToString ()),
				("@ENVIRONMENT_VAR_VALUES@", values.ToString ()),
				("@SYSTEM_PROPERTIES@", properties.ToString ())),
		};
		sources.AddRange (RuntimeProviderSourceGenerator.WriteAdditionalRuntimeProviderSources (
			OutputDirectory, isCoreCLR: false, AdditionalProviderSources, preserveTimestamp: false));
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
				"@(MAIN_ASSEMBLY_NAME|ENVIRONMENT_VAR_NAMES|ENVIRONMENT_VAR_VALUES|SYSTEM_PROPERTIES)@",
				match => replacementValues [match.Value]);
			string path = Path.Combine (outputDirectory, name);
			Log.LogDebugMessage ($"Writing: {path}");
			File.WriteAllText (path, source, new UTF8Encoding (false));
			return path;
		}
	}

	static void AppendString (StringBuilder builder, string value)
	{
		builder.Append ("\t\t\"");
		builder.Append (EscapeJavaString (value));
		builder.Append ("\",\n");
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
