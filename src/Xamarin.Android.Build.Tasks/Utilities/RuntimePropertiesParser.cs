using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Xamarin.Android.Tasks;

class RuntimePropertiesParser
{
	/// <summary>
	/// Reads the `configProperties` from `*.runtimeconfig.json`, layering the ones from the
	/// companion `*.runtimeconfig.dev.json` on top when it exists.
	///
	/// `hostfxr` performs this merge when the app starts, but .NET for Android does not use
	/// `hostfxr`: the properties are baked into the app at build time and handed to
	/// `coreclr_initialize()`, so the merge has to happen here instead. The .NET SDK uses the
	/// dev file to turn on Hot Reload switches for `Debug` builds.
	/// </summary>
	public static Dictionary<string, string>? ParseConfig (string projectRuntimeConfigFilePath, string? projectRuntimeConfigDevFilePath = null)
	{
		if (String.IsNullOrEmpty (projectRuntimeConfigFilePath) || !File.Exists (projectRuntimeConfigFilePath)) {
			return null;
		}

		var ret = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		AddConfigProperties (projectRuntimeConfigFilePath, ret);

		if (!projectRuntimeConfigDevFilePath.IsNullOrEmpty () && File.Exists (projectRuntimeConfigDevFilePath)) {
			// Values from the dev file win, matching `hostfxr`
			AddConfigProperties (projectRuntimeConfigDevFilePath, ret);
		}

		return ret;
	}

	static void AddConfigProperties (string path, Dictionary<string, string> properties)
	{
		using var fs = File.OpenRead (path);

		var jsonOptions = new JsonDocumentOptions {
			AllowTrailingCommas = true, // yes, please!
			CommentHandling = JsonCommentHandling.Skip,
		};
		using JsonDocument config = JsonDocument.Parse (fs, jsonOptions);

		// Either file may legitimately omit both keys: the dev file often only carries
		// `additionalProbingPaths`, and a project that sets no feature switches produces a
		// `*.runtimeconfig.json` without `configProperties`. Treat both as "no properties".
		if (!config.RootElement.TryGetProperty ("runtimeOptions", out JsonElement runtimeOptions) ||
				!runtimeOptions.TryGetProperty ("configProperties", out JsonElement configProperties)) {
			return;
		}

		foreach (JsonProperty prop in configProperties.EnumerateObject ()) {
			string? value = GetJsonValueAsString (prop.Value);
			if (value is not null) {
				properties[prop.Name] = value;
			}
		}
	}

	static string? GetJsonValueAsString (JsonElement element) =>
		element.ValueKind switch {
			JsonValueKind.String => element.GetString (),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			JsonValueKind.Number or JsonValueKind.Object or JsonValueKind.Array => element.GetRawText (),
			_ => null, // Null or Undefined
		};
}
