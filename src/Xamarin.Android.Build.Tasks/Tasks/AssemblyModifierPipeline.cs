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
	// Names of assemblies which don't have Mono.Android.dll references, or are framework assemblies, but which must
	// be scanned for Java types.
	static readonly HashSet<string> SpecialAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
			"Java.Interop.dll",
			"Mono.Android.dll",
			"Mono.Android.Runtime.dll",
		};

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

		var writerParameters = new WriterParameters {
			DeterministicMvid = Deterministic,
		};

		Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> perArchAssemblies = MonoAndroidHelper.GetPerArchAssemblies (ResolvedAssemblies, Array.Empty<string> (), validate: false);

		RunState? runState = null;
		var currentArch = AndroidTargetArch.None;

		for (int i = 0; i < SourceFiles.Length; i++) {
			ITaskItem source = SourceFiles [i];
			AndroidTargetArch sourceArch = GetValidArchitecture (source);
			ITaskItem destination = DestinationFiles [i];
			AndroidTargetArch destinationArch = GetValidArchitecture (destination);

			if (sourceArch != destinationArch) {
				throw new InvalidOperationException ($"Internal error: assembly '{sourceArch}' targets architecture '{sourceArch}', while destination assembly '{destination}' targets '{destinationArch}' instead");
			}

			// Each architecture must have a different set of context classes, or otherwise only the first instance of the assembly may be rewritten.
			if (currentArch != sourceArch) {
				currentArch = sourceArch;
				runState?.Dispose ();

				var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: ReadSymbols, loadReaderParameters: readerParameters);
				runState = new RunState (resolver);

				// Add SearchDirectories for the current architecture's ResolvedAssemblies
				foreach (var kvp in perArchAssemblies [sourceArch]) {
					ITaskItem assembly = kvp.Value;
					var path = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec));
					if (!runState.resolver.SearchDirectories.Contains (path)) {
						runState.resolver.SearchDirectories.Add (path);
					}
				}

				// Set up the FixAbstractMethodsStep and AddKeepAlivesStep
				var context = new MSBuildLinkContext (runState.resolver, Log);

				CreateRunState (runState, context);
			}

			Directory.CreateDirectory (Path.GetDirectoryName (destination.ItemSpec));

			RunPipeline (source, destination, runState!, writerParameters);
		}

		runState?.Dispose ();

		return !Log.HasLoggedErrors;
	}

	protected virtual void CreateRunState (RunState runState, MSBuildLinkContext context)
	{
		var findJavaObjectsStep = new FindJavaObjectsStep (Log) {
			ApplicationJavaClass = ApplicationJavaClass,
			ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
			UseMarshalMethods = EnableMarshalMethods,
		};

		findJavaObjectsStep.Initialize (context);

		runState.findJavaObjectsStep = findJavaObjectsStep;
	}

	protected virtual void RunPipeline (ITaskItem source, ITaskItem destination, RunState runState, WriterParameters writerParameters)
	{
		var destinationJLOXml = Path.ChangeExtension (destination.ItemSpec, ".jlo.xml");

		if (!TryScanForJavaObjects (source, destination, runState, writerParameters)) {
			// Even if we didn't scan for Java objects, we still write an empty .xml file for later steps
			FindJavaObjectsStep.WriteEmptyXmlFile (destinationJLOXml);
		}
	}

	bool TryScanForJavaObjects (ITaskItem source, ITaskItem destination, RunState runState, WriterParameters writerParameters)
	{
		if (!ShouldScanAssembly (source))
			return false;

		var destinationJLOXml = Path.ChangeExtension (destination.ItemSpec, ".jlo.xml");
		var assemblyDefinition = runState.resolver!.GetAssembly (source.ItemSpec);

		var scanned = runState.findJavaObjectsStep!.ProcessAssembly (assemblyDefinition, destinationJLOXml);

		return scanned;
	}

	bool ShouldScanAssembly (ITaskItem source)
	{
		// Skip this assembly if it is not an Android assembly
		if (!IsAndroidAssembly (source)) {
			Log.LogDebugMessage ($"Skipping assembly '{source.ItemSpec}' because it is not an Android assembly");
			return false;
		}

		// When marshal methods or non-JavaPeerStyle.XAJavaInterop1 are in use we do not want to skip non-user assemblies (such as Mono.Android) - we need to generate JCWs for them during
		// application build, unlike in Debug configuration or when marshal methods are disabled, in which case we use JCWs generated during Xamarin.Android
		// build and stored in a jar file.
		var useMarshalMethods = !Debug && EnableMarshalMethods;
		var shouldSkipNonUserAssemblies = !useMarshalMethods && codeGenerationTarget == JavaPeerStyle.XAJavaInterop1;

		if (shouldSkipNonUserAssemblies && !ResolvedUserAssemblies.Any (a => a.ItemSpec == source.ItemSpec)) {
			Log.LogDebugMessage ($"Skipping assembly '{source.ItemSpec}' because it is not a user assembly and we don't need JLOs from non-user assemblies");
			return false;
		}

		return true;
	}

	bool IsAndroidAssembly (ITaskItem source)
	{
		string name = Path.GetFileName (source.ItemSpec);

		if (SpecialAssemblies.Contains (name))
			return true;

		return MonoAndroidHelper.IsMonoAndroidAssembly (source);
	}

	AndroidTargetArch GetValidArchitecture (ITaskItem item)
	{
		AndroidTargetArch ret = MonoAndroidHelper.GetTargetArch (item);
		if (ret == AndroidTargetArch.None) {
			throw new InvalidOperationException ($"Internal error: assembly '{item}' doesn't target any architecture.");
		}

		return ret;
	}

	protected sealed class RunState : IDisposable
	{
		public DirectoryAssemblyResolver resolver;
		public FixAbstractMethodsStep? fixAbstractMethodsStep = null;
		public AddKeepAlivesStep? addKeepAliveStep = null;
		public FixLegacyResourceDesignerStep? fixLegacyResourceDesignerStep = null;
		public FindJavaObjectsStep? findJavaObjectsStep = null;
		bool disposed_value;

		public RunState (DirectoryAssemblyResolver resolver)
		{
			this.resolver = resolver;
		}

		private void Dispose (bool disposing)
		{
			if (!disposed_value) {
				if (disposing) {
					resolver?.Dispose ();
					fixAbstractMethodsStep = null;
					fixLegacyResourceDesignerStep = null;
					addKeepAliveStep = null;
					findJavaObjectsStep = null;
				}
				disposed_value = true;
			}
		}

		public void Dispose ()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose (disposing: true);
			GC.SuppressFinalize (this);
		}
	}
}
