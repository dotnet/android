#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using MonoDroid.Tuner;
using Xamarin.Android.Tools;
using PackageNamingPolicyEnum = Java.Interop.Tools.TypeNameMappings.PackageNamingPolicy;

namespace Xamarin.Android.Tasks;

/// <summary>
/// This task runs additional "linker steps" that are not part of ILLink. These steps
/// are run *after* the linker has run. Additionally, this task is run by
/// LinkAssembliesNoShrink to modify assemblies when ILLink is not used.
/// </summary>
public class AssemblyModifierPipeline : AndroidTask
{
	public override string TaskPrefix => "AMP";

	public string ApplicationJavaClass { get; set; } = "";

	public string CodeGenerationTarget { get; set; } = "";

	public bool Debug { get; set; }

	[Required]
	public ITaskItem [] DestinationFiles { get; set; } = [];

	public bool Deterministic { get; set; }

	public bool EnableMarshalMethods { get; set; }

	public bool ErrorOnCustomJavaObject { get; set; }

	public string? PackageNamingPolicy { get; set; }

	/// <summary>
	/// Defaults to false, enables Mono.Cecil to load symbols
	/// </summary>
	public bool ReadSymbols { get; set; }

	/// <summary>
	/// These are used so we have the full list of SearchDirectories
	/// </summary>
	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	[Required]
	public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];

	[Required]
	public ITaskItem [] SourceFiles { get; set; } = [];

	/// <summary>
	/// $(TargetName) would be "AndroidApp1" with no extension
	/// </summary>
	[Required]
	public string TargetName { get; set; } = "";

	protected JavaPeerStyle codeGenerationTarget;

	public override bool RunTask ()
	{
		codeGenerationTarget = MonoAndroidHelper.ParseCodeGenerationTarget (CodeGenerationTarget);
		JavaNativeTypeManager.PackageNamingPolicy = Enum.TryParse (PackageNamingPolicy, out PackageNamingPolicyEnum pnp) ? pnp : PackageNamingPolicyEnum.LowercaseCrc64;

		if (SourceFiles.Length != DestinationFiles.Length)
			throw new ArgumentException ("source and destination count mismatch");

		var readerParameters = new ReaderParameters {
			ReadSymbols = ReadSymbols,
		};

		Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> perArchAssemblies = MonoAndroidHelper.GetPerArchAssemblies (ResolvedAssemblies, [], validate: false);

		AssemblyPipeline? pipeline = null;
		var currentArch = AndroidTargetArch.None;

		for (int i = 0; i < SourceFiles.Length; i++) {
			ITaskItem source = SourceFiles [i];
			AndroidTargetArch sourceArch = MonoAndroidHelper.GetRequiredValidArchitecture (source);
			ITaskItem destination = DestinationFiles [i];
			AndroidTargetArch destinationArch = MonoAndroidHelper.GetRequiredValidArchitecture (destination);

			if (sourceArch != destinationArch) {
				throw new InvalidOperationException ($"Internal error: assembly '{sourceArch}' targets architecture '{sourceArch}', while destination assembly '{destination}' targets '{destinationArch}' instead");
			}

			// Each architecture must have a different set of context classes, or otherwise only the first instance of the assembly may be rewritten.
			if (currentArch != sourceArch) {
				currentArch = sourceArch;
				pipeline?.Dispose ();

				var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: ReadSymbols, loadReaderParameters: readerParameters);

				// Add SearchDirectories for the current architecture's ResolvedAssemblies
				foreach (var kvp in perArchAssemblies [sourceArch]) {
					ITaskItem assembly = kvp.Value;
					var path = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec));
					if (!resolver.SearchDirectories.Contains (path)) {
						resolver.SearchDirectories.Add (path);
					}
				}

				// Set up the FixAbstractMethodsStep and AddKeepAlivesStep
				var context = new MSBuildLinkContext (resolver, Log);
				pipeline = new AssemblyPipeline (resolver);

				BuildPipeline (pipeline, context);
			}

			Directory.CreateDirectory (Path.GetDirectoryName (destination.ItemSpec));

			RunPipeline (pipeline!, source, destination);
		}

		pipeline?.Dispose ();

		return !Log.HasLoggedErrors;
	}

	protected virtual void BuildPipeline (AssemblyPipeline pipeline, MSBuildLinkContext context)
	{
		// FindJavaObjectsStep
		var findJavaObjectsStep = new FindJavaObjectsStep (Log) {
			ApplicationJavaClass = ApplicationJavaClass,
			ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
			UseMarshalMethods = EnableMarshalMethods,
		};

		findJavaObjectsStep.Initialize (context);
		pipeline.Steps.Add (findJavaObjectsStep);

		// StripEmbeddedLibrariesStep
		var stripEmbeddedLibrariesStep = new StripEmbeddedLibrariesStep (Log);
		pipeline.Steps.Add (stripEmbeddedLibrariesStep);

		// SaveChangedAssemblyStep
		var writerParameters = new WriterParameters {
			DeterministicMvid = Deterministic,
		};

		var saveChangedAssemblyStep = new SaveChangedAssemblyStep (Log, writerParameters);
		pipeline.Steps.Add (saveChangedAssemblyStep);

		// FindTypeMapObjectsStep - this must be run after the assembly has been saved, as saving changes the MVID
		var findTypeMapObjectsStep = new FindTypeMapObjectsStep (Log) {
			ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
			Debug = Debug,
		};

		findTypeMapObjectsStep.Initialize (context);
		pipeline.Steps.Add (findTypeMapObjectsStep);
	}

	void RunPipeline (AssemblyPipeline pipeline, ITaskItem source, ITaskItem destination)
	{
		var assembly = pipeline.Resolver.GetAssembly (source.ItemSpec);

		var context = new StepContext (source, destination) {
			CodeGenerationTarget = codeGenerationTarget,
			EnableMarshalMethods = EnableMarshalMethods,
			IsAndroidAssembly = MonoAndroidHelper.IsAndroidAssembly (source),
			IsDebug = Debug,
			IsFrameworkAssembly = MonoAndroidHelper.IsFrameworkAssembly (source),
			IsMainAssembly = Path.GetFileNameWithoutExtension (source.ItemSpec) == TargetName,
			IsUserAssembly = ResolvedUserAssemblies.Any (a => a.ItemSpec == source.ItemSpec),
		};

		pipeline.Run (assembly, context);
	}
}

class SaveChangedAssemblyStep : IAssemblyModifierPipelineStep
{
	public TaskLoggingHelper Log { get; set; }

	public WriterParameters WriterParameters { get; set; }

	public SaveChangedAssemblyStep (TaskLoggingHelper log, WriterParameters writerParameters)
	{
		Log = log;
		WriterParameters = writerParameters;
	}

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		if (context.IsAssemblyModified) {
			Log.LogDebugMessage ($"Saving modified assembly: {context.Destination.ItemSpec}");
			Directory.CreateDirectory (Path.GetDirectoryName (context.Destination.ItemSpec));

			// Write back pure IL even for crossgen-ed (R2R) assemblies, matching ILLink's OutputStep behavior.
			// Mono.Cecil cannot write mixed-mode assemblies, so we strip the R2R metadata before writing.
			// The native R2R code is discarded since the assembly has been modified and would need to be
			// re-crossgen'd anyway.
			foreach (var module in assembly.Modules) {
				if (IsCrossgened (module)) {
					module.Attributes |= ModuleAttributes.ILOnly;
					module.Attributes ^= ModuleAttributes.ILLibrary;
					module.Architecture = TargetArchitecture.I386; // I386+ILOnly translates to AnyCPU
					module.Characteristics |= ModuleCharacteristics.NoSEH;
				}
			}

			WriterParameters.WriteSymbols = assembly.MainModule.HasSymbols;
			assembly.Write (context.Destination.ItemSpec, WriterParameters);
		} else {
			// If we didn't write a modified file, copy the original to the destination
			CopyIfChanged (context.Source, context.Destination);
		}

		// We just saved the assembly, so it is no longer modified
		context.IsAssemblyModified = false;
	}

	void CopyIfChanged (ITaskItem source, ITaskItem destination)
	{
		if (MonoAndroidHelper.CopyAssemblyAndSymbols (source.ItemSpec, destination.ItemSpec)) {
			Log.LogDebugMessage ($"Copied: {destination.ItemSpec}");
		} else {
			Log.LogDebugMessage ($"Skipped unchanged file: {destination.ItemSpec}");

			// NOTE: We still need to update the timestamp on this file, or this target would run again
			File.SetLastWriteTimeUtc (destination.ItemSpec, DateTime.UtcNow);
		}
	}

	/// <summary>
	/// Check if a module has been crossgen-ed (ReadyToRun compiled), matching
	/// ILLink's ModuleDefinitionExtensions.IsCrossgened() implementation.
	/// </summary>
	static bool IsCrossgened (ModuleDefinition module)
	{
		return (module.Attributes & ModuleAttributes.ILOnly) == 0 &&
			(module.Attributes & ModuleAttributes.ILLibrary) != 0;
	}
}
