#nullable enable
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public sealed class GenerateCoreClrRuntimeProviderSources : AndroidTask
{
	public override string TaskPrefix => "GCRP";

	[Required]
	public string [] AdditionalProviderSources { get; set; } = [];

	[Required]
	public string OutputDirectory { get; set; } = "";

	public override bool RunTask ()
	{
		GenerateAdditionalProviderSources.WriteAdditionalRuntimeProviderSources (OutputDirectory, isCoreCLR: true, AdditionalProviderSources);
		return !Log.HasLoggedErrors;
	}
}
