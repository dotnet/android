using System;
using System.Collections.Generic;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Build.Framework;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class AssemblyPipeline : IDisposable
{
	bool disposed_value;

	public List<IAssemblyModifierPipelineStep> Steps { get; } = [];
	public DirectoryAssemblyResolver Resolver { get; }

	public AssemblyPipeline (DirectoryAssemblyResolver resolver)
	{
		Resolver = resolver;
	}

	public void Run (AssemblyDefinition assembly, StepContext context)
	{
		foreach (var step in Steps)
			step.ProcessAssembly (assembly, context);
	}

	protected virtual void Dispose (bool disposing)
	{
		if (!disposed_value) {
			if (disposing) {
				Resolver?.Dispose ();
			}

			disposed_value = true;
		}
	}

	public void Dispose ()
	{
		// Do not change this code. Put cleanup code in 'Dispose (bool disposing)' method
		Dispose (disposing: true);
		GC.SuppressFinalize (this);
	}
}

public interface IAssemblyModifierPipelineStep
{
	void ProcessAssembly (AssemblyDefinition assembly, StepContext context);
}

public class StepContext
{
	public AndroidTargetArch Architecture { get; set; }
	public string AndroidSdkPlatform { get; set; }
	public JavaPeerStyle CodeGenerationTarget { get; set; }
	public ITaskItem Destination { get; }
	public bool EnableMarshalMethods { get; set; }
	public bool EnableManagedMarshalMethodsLookup { get; set; }
	public ITaskItem [] Environments { get; set; }
	public bool IsAndroidAssembly { get; set; }
	public bool IsAssemblyModified { get; set; }
	public bool IsDebug { get; set; }
	public bool IsFrameworkAssembly { get; set; }
	public bool IsMainAssembly { get; set; }
	public bool IsUserAssembly { get; set; }
	// This only contains the resolved assemblies for *this* architecture
	public ITaskItem [] ResolvedAssemblies { get; set; }
	public ITaskItem Source { get; }

	public bool IsAndroidUserAssembly => IsAndroidAssembly && IsUserAssembly;

	public StepContext (ITaskItem source, ITaskItem destination, string androidSdkPlatform, ITaskItem [] environments, ITaskItem [] resolvedAssemblies)
	{
		AndroidSdkPlatform = androidSdkPlatform;
		Destination = destination;
		Environments = environments;
		ResolvedAssemblies = resolvedAssemblies;
		Source = source;
	}
}
