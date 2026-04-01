#nullable enable

using System;
using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Mono.Cecil;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner;

/// <summary>
/// Post-trimming version of FixAbstractMethodsStep that delegates to the core logic
/// in <see cref="FixAbstractMethodsStep"/>. Skips framework assemblies, checks for
/// AppDomain.CreateDomain usage, and fixes missing abstract method implementations
/// on Java.Lang.Object subclasses.
/// </summary>
class PostTrimmingFixAbstractMethodsStep : IAssemblyModifierPipelineStep
{
	readonly FixAbstractMethodsStep _step;
	readonly Action<string> _warn;

	public PostTrimmingFixAbstractMethodsStep (
		IMetadataResolver cache,
		Func<AssemblyDefinition?> getMonoAndroidAssembly,
		Action<string> logMessage,
		Action<string> warn)
	{
		_step = new FixAbstractMethodsStep (cache, getMonoAndroidAssembly, logMessage);
		_warn = warn;
	}

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		if (MonoAndroidHelper.IsFrameworkAssembly (assembly))
			return;

		_step.CheckAppDomainUsage (assembly, _warn);

		if (!assembly.MainModule.HasTypeReference ("Java.Lang.Object"))
			return;

		context.IsAssemblyModified |= _step.FixAbstractMethods (assembly);
	}
}
