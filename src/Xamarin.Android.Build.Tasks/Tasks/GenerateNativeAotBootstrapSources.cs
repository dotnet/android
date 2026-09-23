#nullable enable
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Generates NativeAOT bootstrap Java sources: JavaInteropRuntime.java (loads the native
/// library and exposes the runtime init entry point) and NativeAotEnvironmentVars.java
/// (bakes in environment variable names/values for the native runtime).
///
/// These files are needed by NativeAotRuntimeProvider.java.
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

	public bool EnableSGenConcurrent { get; set; }

	// Names of the extra per-process runtime providers (e.g. NativeAotRuntimeProvider_1) that the
	// manifest declares for components with a non-default android:process; their Java sources must be
	// generated too.
	public string [] AdditionalProviderSources { get; set; } = [];

	public override bool RunTask ()
	{
		GenerateAdditionalProviderSources.GenerateNativeAotBootstrapFiles (
			Log, OutputDirectory, TargetName, Environments, EnableSGenConcurrent);

		GenerateAdditionalProviderSources.WriteAdditionalRuntimeProviderSources (OutputDirectory, isCoreCLR: false, AdditionalProviderSources);

		return !Log.HasLoggedErrors;
	}
}
