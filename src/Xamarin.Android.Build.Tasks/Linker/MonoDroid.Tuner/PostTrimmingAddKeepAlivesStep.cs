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

	public PostTrimmingAddKeepAlivesStep (IMetadataResolver cache, Func<AssemblyDefinition?> getCorlibAssembly, Action<string> logMessage)
	{
		this.cache = cache;
		this.getCorlibAssembly = getCorlibAssembly;
		this.logMessage = logMessage;
	}

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		context.IsAssemblyModified |= AddKeepAlivesHelper.AddKeepAlives (assembly, cache, getCorlibAssembly, logMessage);
	}
}
