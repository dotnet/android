#nullable enable
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Tasks;

[MSBuildMultiThreadableTask]
public sealed class GenerateCoreClrRuntimeProviderSources : MultithreadedAndroidTask
{
	public override string TaskPrefix => "GCRP";

	[Required]
	public string [] AdditionalProviderSources { get; set; } = [];

	[Required]
	public string OutputDirectory { get; set; } = "";

	public override bool RunTask ()
	{
		RuntimeProviderSourceGenerator.WriteAdditionalRuntimeProviderSources (
			TaskEnvironment, OutputDirectory, isCoreCLR: true, AdditionalProviderSources);
		return !Log.HasLoggedErrors;
	}
}
