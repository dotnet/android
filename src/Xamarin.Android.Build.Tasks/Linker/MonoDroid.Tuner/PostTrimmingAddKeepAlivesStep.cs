using System;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner;

/// <summary>
/// Post-trimming version of AddKeepAlives that calls AddKeepAlivesHelper directly,
/// matching the original ILLink behavior (no IsAndroidUserAssembly pre-filter).
/// The helper has its own assembly-level guards (HasTypeReference, IsDotNetAndroidAssembly).
/// </summary>
class PostTrimmingAddKeepAlivesStep : IAssemblyModifierPipelineStep
{
	readonly IMetadataResolver cache;
	readonly Func<AssemblyDefinition?> getCorlibAssembly;
	readonly Action<string> logMessage;
	readonly KeepAliveFixupReporter reporter;

	public PostTrimmingAddKeepAlivesStep (
		IMetadataResolver cache,
		Func<AssemblyDefinition?> getCorlibAssembly,
		Action<string> logMessage,
		KeepAliveFixupReporter reporter)
	{
		this.cache = cache;
		this.getCorlibAssembly = getCorlibAssembly;
		this.logMessage = logMessage;
		this.reporter = reporter;
	}

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		bool changed = AddKeepAlivesHelper.AddKeepAlives (assembly, cache, getCorlibAssembly, logMessage);
		context.IsAssemblyModified |= changed;

		if (changed) {
			reporter.Report (assembly, context.Source.ItemSpec);
		}
	}
}
