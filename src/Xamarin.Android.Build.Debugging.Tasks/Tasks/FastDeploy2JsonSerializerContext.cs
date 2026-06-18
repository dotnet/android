using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Xamarin.Android.Tasks
{
	[JsonSourceGenerationOptions (WriteIndented = true)]
	[JsonSerializable (typeof (Dictionary<string, FastDeploy2.ManifestEntry>))]
	[JsonSerializable (typeof (FastDeploy2Base.DiagnosticData))]
	internal partial class FastDeploy2JsonSerializerContext : JsonSerializerContext
	{
	}
}
