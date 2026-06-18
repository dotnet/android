using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Xamarin.Android.Tasks
{
	[JsonSourceGenerationOptions (WriteIndented = true)]
	[JsonSerializable (typeof (Dictionary<string, FastDeploy2.ManifestEntry>))]
	internal partial class FastDeploy2JsonSerializerContext : JsonSerializerContext
	{
	}
}
