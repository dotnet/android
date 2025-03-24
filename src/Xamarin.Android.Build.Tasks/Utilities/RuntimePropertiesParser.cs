using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Xamarin.Android.Tasks;

class RuntimePropertiesParser
{
	public static Dictionary<string, string>? ParseConfig (string projectRuntimeConfigFilePath)
	{
		if (String.IsNullOrEmpty (projectRuntimeConfigFilePath) || !File.Exists (projectRuntimeConfigFilePath)) {
			return null;
		}

		using var fs = File.OpenRead (projectRuntimeConfigFilePath);

		var jsonOptions = new JsonDocumentOptions {
			AllowTrailingCommas = true, // yes, please!
			CommentHandling = JsonCommentHandling.Skip,
		};
		using JsonDocument config = JsonDocument.Parse (fs, jsonOptions);
		JsonElement runtimeOptions = config.RootElement.GetProperty ("runtimeOptions");
		JsonElement properties = runtimeOptions.GetProperty ("configProperties");
		var ret = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		foreach (JsonProperty prop in properties.EnumerateObject ()) {
			ret[prop.Name] = prop.Value.GetRawText ();
		}

		return ret;
	}
}
