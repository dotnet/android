using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Xamarin.Android.Tasks
{
	[JsonSourceGenerationOptions (WriteIndented = true)]
	[JsonSerializable (typeof (FastDeploy2.ManifestData))]
	internal partial class FastDeploy2JsonSerializerContext : JsonSerializerContext
	{
	}
}
