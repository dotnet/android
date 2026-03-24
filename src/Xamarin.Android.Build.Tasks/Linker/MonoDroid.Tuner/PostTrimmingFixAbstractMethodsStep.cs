using System;
using System.Collections.Generic;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner;

/// <summary>
/// Post-trimming version of FixAbstractMethods that calls FixAbstractMethodsHelper directly.
/// Runs in <see cref="PostTrimmingPipeline"/> after ILLink in the per-RID inner build.
/// The helper has its own type-level guards (MightNeedFix checks IsSubclassOf Java.Lang.Object).
/// </summary>
class PostTrimmingFixAbstractMethodsStep : IAssemblyModifierPipelineStep
{
	readonly IMetadataResolver cache;
	readonly Func<AssemblyDefinition?> getMonoAndroidAssembly;
	readonly Action<string> logMessage;
	readonly Action<string> warn;
	readonly HashSet<string> warnedAssemblies = new (StringComparer.Ordinal);
	MethodDefinition? abstractMethodErrorCtor;

	public PostTrimmingFixAbstractMethodsStep (IMetadataResolver cache, Func<AssemblyDefinition?> getMonoAndroidAssembly, Action<string> logMessage, Action<string> warn)
	{
		this.cache = cache;
		this.getMonoAndroidAssembly = getMonoAndroidAssembly;
		this.logMessage = logMessage;
		this.warn = warn;
	}

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		if (MonoAndroidHelper.IsFrameworkAssembly (assembly))
			return;

		FixAbstractMethodsHelper.CheckAppDomainUsage (assembly, warn, warnedAssemblies);

		if (!assembly.MainModule.HasTypeReference ("Java.Lang.Object"))
			return;

		context.IsAssemblyModified |= FixAbstractMethodsHelper.FixAbstractMethods (assembly, cache, ref abstractMethodErrorCtor, getMonoAndroidAssembly, logMessage);
	}
}
