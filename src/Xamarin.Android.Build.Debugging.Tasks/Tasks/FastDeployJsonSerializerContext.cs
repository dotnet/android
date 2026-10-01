using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Xamarin.Android.Tasks
{
	[JsonSourceGenerationOptions (WriteIndented = true)]
	[JsonSerializable (typeof (FastDeploy.ManifestData))]
	internal partial class FastDeployJsonSerializerContext : JsonSerializerContext
	{
	}
}
