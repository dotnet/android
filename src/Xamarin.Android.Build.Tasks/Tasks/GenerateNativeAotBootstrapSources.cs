#nullable enable
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Generates NativeAOT bootstrap Java sources: JavaInteropRuntime.java (loads the native
/// library and exposes the runtime init entry point) and NativeAotEnvironmentVars.java
/// (bakes in environment variable names/values for the native runtime).
///
/// These files are needed by NativeAotRuntimeProvider.java and must be generated regardless
/// of the typemap implementation (managed or trimmable).
///
/// Delegates to <see cref="GenerateAdditionalProviderSources.GenerateNativeAotBootstrapFiles"/>
/// for the actual generation logic.
/// </summary>
public sealed class GenerateNativeAotBootstrapSources : AndroidTask
{
	public override string TaskPrefix => "GNABS";

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public string TargetName { get; set; } = "";

	public ITaskItem []? Environments { get; set; }

	public string? HttpClientHandlerType { get; set; }

	public bool EnableSGenConcurrent { get; set; }

	public override bool RunTask ()
	{
		GenerateAdditionalProviderSources.GenerateNativeAotBootstrapFiles (
			Log, OutputDirectory, TargetName, Environments, HttpClientHandlerType, EnableSGenConcurrent);

		return !Log.HasLoggedErrors;
	}
}
