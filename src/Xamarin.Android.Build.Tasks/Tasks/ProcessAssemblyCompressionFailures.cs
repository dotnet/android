#nullable enable

using System;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Remove "CompressedAssembly" metadata from assemblies that failed to compress.
/// </summary>
public class ProcessAssemblyCompressionFailures : AndroidTask
{
	public override string TaskPrefix => "PAC";

	[Required]
	public ITaskItem [] FailedToCompressAssembliesOutput { get; set; } = [];

	[Required]
	public ITaskItem [] ResolvedFrameworkAssemblies { get; set; } = [];

	[Required]
	public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];

	[Output]
	public ITaskItem [] ResolvedFrameworkAssembliesOutput { get; set; } = [];

	[Output]
	public ITaskItem [] ResolvedUserAssembliesOutput { get; set; } = [];

	public override bool RunTask ()
	{
		// We always need to set the output properties so that future tasks can use them
		ResolvedFrameworkAssembliesOutput = ResolvedFrameworkAssemblies;
		ResolvedUserAssembliesOutput = ResolvedUserAssemblies;

		foreach (var failure in FailedToCompressAssembliesOutput) {
			var assembly = ResolvedFrameworkAssemblies.Concat (ResolvedUserAssemblies).Single (a => a.ItemSpec == failure.ItemSpec);
			assembly.RemoveMetadata ("CompressedAssembly");

			Log.LogDebugMessage ($"Removed 'CompressedAssembly' metadata from '{assembly.ItemSpec}'.");
		}

		return !Log.HasLoggedErrors;
	}
}
