#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Linker.Steps;
using MonoDroid.Tuner;
using Xamarin.Android.Tasks.Utilities;
using Xamarin.Android.Tools;
using PackageNamingPolicyEnum = Java.Interop.Tools.TypeNameMappings.PackageNamingPolicy;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task is for Debug builds where LinkMode=None, LinkAssemblies is for Release builds
	/// </summary>
	public class LinkAssembliesNoShrink : AndroidTask
	{
		JavaPeerStyle codeGenerationTarget;

		sealed class RunState
		{
			public DirectoryAssemblyResolver? resolver = null;
			public FixAbstractMethodsStep? fixAbstractMethodsStep = null;
			public AddKeepAlivesStep? addKeepAliveStep = null;
			public FixLegacyResourceDesignerStep? fixLegacyResourceDesignerStep = null;
			public FindJavaObjectsStep? findJavaObjectsStep = null;
		}

		public override string TaskPrefix => "LNS";

		/// <summary>
		/// These are used so we have the full list of SearchDirectories
		/// </summary>
		[Required]
		public ITaskItem [] ResolvedAssemblies { get; set; } = Array.Empty<ITaskItem> ();

		[Required]
		public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];

		[Required]
		public ITaskItem [] SourceFiles { get; set; } = Array.Empty<ITaskItem> ();

		[Required]
		public ITaskItem [] DestinationFiles { get; set; } = Array.Empty<ITaskItem> ();

		/// <summary>
		/// $(TargetName) would be "AndroidApp1" with no extension
		/// </summary>
		[Required]
		public string TargetName { get; set; } = "";

		public bool AddKeepAlives { get; set; }

		public string ApplicationJavaClass { get; set; } = "";

		public string CodeGenerationTarget { get; set; } = "";

		public bool Debug { get; set; }

		public bool EnableMarshalMethods { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		public bool Deterministic { get; set; }

		public bool UseDesignerAssembly { get; set; }

		public string? PackageNamingPolicy { get; set; }

		/// <summary>
		/// Defaults to false, enables Mono.Cecil to load symbols
		/// </summary>
		public bool ReadSymbols { get; set; }

		public bool AlreadyLinked { get; set; }

		[Output]
		public ITaskItem [] ScannedFiles { get; set; } = Array.Empty<ITaskItem> ();


		public override bool RunTask ()
		{
			codeGenerationTarget = MonoAndroidHelper.ParseCodeGenerationTarget (CodeGenerationTarget);
			PackageNamingPolicy pnp;
			JavaNativeTypeManager.PackageNamingPolicy = Enum.TryParse (PackageNamingPolicy, out pnp) ? pnp : PackageNamingPolicyEnum.LowercaseCrc64;

			if (SourceFiles.Length != DestinationFiles.Length)
				throw new ArgumentException ("source and destination count mismatch");

			var readerParameters = new ReaderParameters {
				ReadSymbols = ReadSymbols,
			};
			var writerParameters = new WriterParameters {
				DeterministicMvid = Deterministic,
			};

			Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> perArchAssemblies = MonoAndroidHelper.GetPerArchAssemblies (ResolvedAssemblies, Array.Empty<string> (), validate: false);
			var runState = new RunState ();

			AndroidTargetArch currentArch = AndroidTargetArch.None;

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
					runState.resolver?.Dispose ();
					runState.resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: ReadSymbols, loadReaderParameters: readerParameters);

					// Add SearchDirectories for the current architecture's ResolvedAssemblies
					foreach (var kvp in perArchAssemblies[sourceArch]) {
						ITaskItem assembly = kvp.Value;
						var path = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec));
						if (!runState.resolver.SearchDirectories.Contains (path)) {
							runState.resolver.SearchDirectories.Add (path);
						}
					}

					// Set up the FixAbstractMethodsStep and AddKeepAlivesStep
					var context = new MSBuildLinkContext (runState.resolver, Log);

					var fixAbstractMethodsStep = new FixAbstractMethodsStep ();
					fixAbstractMethodsStep.Initialize (context, new EmptyMarkContext ());
					runState.fixAbstractMethodsStep = fixAbstractMethodsStep;

					var addKeepAliveStep = new AddKeepAlivesStep ();
					addKeepAliveStep.Initialize (context);
					runState.addKeepAliveStep = addKeepAliveStep;

					var fixLegacyResourceDesignerStep = new FixLegacyResourceDesignerStep ();
					fixLegacyResourceDesignerStep.Initialize (context);
					runState.fixLegacyResourceDesignerStep = fixLegacyResourceDesignerStep;

					var findJavaObjectsStep = new FindJavaObjectsStep {
						ApplicationJavaClass = ApplicationJavaClass,
						CodeGenerationTarget = codeGenerationTarget,
						ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
						UserAssemblies = ResolvedUserAssemblies.Select (a => Path.GetFileNameWithoutExtension (a.ItemSpec)).ToList (),
						UseMarshalMethods = EnableMarshalMethods,
						Debug = Debug,
						Log = Log,
					};

					findJavaObjectsStep.Initialize (context);
					runState.findJavaObjectsStep = findJavaObjectsStep;
				}

				DoRunTask (source, destination, runState, writerParameters);
			}
			runState.resolver?.Dispose ();

			Log.LogDebugMessage ($"LinkAssembliesNoShrink: total JavaObject time: {total_jlo_ms}ms");

			return !Log.HasLoggedErrors;

			AndroidTargetArch GetValidArchitecture (ITaskItem item)
			{
				AndroidTargetArch ret = MonoAndroidHelper.GetTargetArch (item);
				if (ret == AndroidTargetArch.None) {
					throw new InvalidOperationException ($"Internal error: assembly '{item}' doesn't target any architecture.");
				}

				return ret;
			}
		}

		long total_jlo_ms = 0;

		void DoRunTask (ITaskItem source, ITaskItem destination, RunState runState, WriterParameters writerParameters)
		{
			Directory.CreateDirectory (Path.GetDirectoryName (destination.ItemSpec));

			// Skip this step if the assembly has already gone though ILLink
			if (!AlreadyLinked) {
				if (!TryModifyAssembly (source, destination, runState, writerParameters)) {
					// If we didn't write a modified file, copy the original to the destination
					CopyIfChanged (source, destination);
				}
			}

			var destinationJLOXml = Path.ChangeExtension (destination.ItemSpec, ".jlo.xml");

			if (!TryScanForJavaObjects (source, destination, runState, writerParameters)) {
				// If we didn't scan for Java objects, write an empty .xml file to facilitate incremental builds
				FindJavaObjectsStep.WriteEmptyXmlFile (destinationJLOXml);
			}

			return;

			var assemblyName = Path.GetFileNameWithoutExtension (source.ItemSpec);
			// In .NET 6+, we can skip the main assembly
			//if (!AddKeepAlives && assemblyName == TargetName) {
			//	FindJavaObjectsStep.WriteEmptyXmlFile (destinationJLOXml);
			//	CopyIfChanged (source, destination);
			//	return;
			//}
			//if (MonoAndroidHelper.IsFrameworkAssembly (source)) {
			//	CopyIfChanged (source, destination);
			//	FindJavaObjectsStep.WriteEmptyXmlFile (destinationJLOXml);
			//	return;
			//}

			// Only run the step on "MonoAndroid" assemblies
			if (ShouldScanAssembly (source)) {
				AssemblyDefinition assemblyDefinition = runState.resolver!.GetAssembly (source.ItemSpec);
				var sw = Stopwatch.StartNew ();
				var resolve_ms = sw.ElapsedMilliseconds;
				long fixAbstractMethods_ms = 0;
				long fixLegacyResourceDesigner_ms = 0;
				long addKeepAlives_ms = 0;
				sw.Restart ();
				var save = false;

				if (!AlreadyLinked) {
					save = runState.fixAbstractMethodsStep!.FixAbstractMethods (assemblyDefinition);
					fixAbstractMethods_ms = sw.ElapsedMilliseconds;
					sw.Restart ();
					if (UseDesignerAssembly)
						save |= runState.fixLegacyResourceDesignerStep!.ProcessAssemblyDesigner (assemblyDefinition);
					fixLegacyResourceDesigner_ms = sw.ElapsedMilliseconds;
					sw.Restart ();
					if (AddKeepAlives)
						save |= runState.addKeepAliveStep!.AddKeepAlives (assemblyDefinition);
					addKeepAlives_ms = sw.ElapsedMilliseconds;
					sw.Restart ();
				}
				var scanned = runState.findJavaObjectsStep!.ProcessAssembly (assemblyDefinition, destinationJLOXml, MonoAndroidHelper.IsMonoAndroidAssembly (source));
				if (scanned)
					ScannedFiles = ScannedFiles.Append (new TaskItem (destination)).ToArray ();
				var findJavaObjects_ms = sw.ElapsedMilliseconds;
				total_jlo_ms += (int) findJavaObjects_ms;
				Log.LogDebugMessage ($"LinkAssembliesNoShrink: {assemblyName} -> {save} (Resolve: {resolve_ms}ms, FixAbstract: {fixAbstractMethods_ms}ms, Designer: {fixLegacyResourceDesigner_ms}, KeepAlives: {addKeepAlives_ms}ms, JavaObjects: {findJavaObjects_ms}ms)");
				if (save) {
					Log.LogDebugMessage ($"Saving modified assembly: {destination.ItemSpec}");
					Directory.CreateDirectory (Path.GetDirectoryName (destination.ItemSpec));
					writerParameters.WriteSymbols = assemblyDefinition.MainModule.HasSymbols;
					assemblyDefinition.Write (destination.ItemSpec, writerParameters);
					return;
				}
			} else {

				FindJavaObjectsStep.WriteEmptyXmlFile (destinationJLOXml);
			}

			CopyIfChanged (source, destination);
		}

		bool TryModifyAssembly (ITaskItem source, ITaskItem destination, RunState runState, WriterParameters writerParameters)
		{
			var assemblyName = Path.GetFileNameWithoutExtension (source.ItemSpec);

			// In .NET 6+, we can skip the main assembly
			if (!AddKeepAlives && assemblyName == TargetName) {
				return false;
			}
			if (MonoAndroidHelper.IsFrameworkAssembly (source)) {
				return false;
			}

			// Only run the step on "MonoAndroid" assemblies
			if (MonoAndroidHelper.IsMonoAndroidAssembly (source)) {
				AssemblyDefinition assemblyDefinition = runState.resolver!.GetAssembly (source.ItemSpec);

				bool save = runState.fixAbstractMethodsStep!.FixAbstractMethods (assemblyDefinition);
				if (UseDesignerAssembly)
					save |= runState.fixLegacyResourceDesignerStep!.ProcessAssemblyDesigner (assemblyDefinition);
				if (AddKeepAlives)
					save |= runState.addKeepAliveStep!.AddKeepAlives (assemblyDefinition);
				if (save) {
					Log.LogDebugMessage ($"Saving modified assembly: {destination.ItemSpec}");
					Directory.CreateDirectory (Path.GetDirectoryName (destination.ItemSpec));
					writerParameters.WriteSymbols = assemblyDefinition.MainModule.HasSymbols;
					assemblyDefinition.Write (destination.ItemSpec, writerParameters);
					return true;
				}
			}

			return false;
		}

		bool TryScanForJavaObjects (ITaskItem source, ITaskItem destination, RunState runState, WriterParameters writerParameters)
		{
			if (!ShouldScanAssembly (source))
				return false;

			var destinationJLOXml = Path.ChangeExtension (destination.ItemSpec, ".jlo.xml");
			var assemblyDefinition = runState.resolver!.GetAssembly (source.ItemSpec);

			var scanned = runState.findJavaObjectsStep!.ProcessAssembly (assemblyDefinition, destinationJLOXml, MonoAndroidHelper.IsMonoAndroidAssembly (source));

			if (scanned)
				ScannedFiles = ScannedFiles.Append (new TaskItem (destination)).ToArray ();

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
			bool useMarshalMethods = !Debug && EnableMarshalMethods;

			//var shouldSkipNonUserAssemblies = Debug && !EnableMarshalMethods && codeGenerationTarget == JavaPeerStyle.XAJavaInterop1;
			var shouldSkipNonUserAssemblies = !useMarshalMethods && codeGenerationTarget == JavaPeerStyle.XAJavaInterop1;

			if (shouldSkipNonUserAssemblies && !ResolvedUserAssemblies.Any (a => a.ItemSpec == source.ItemSpec)) {
				Log.LogDebugMessage ($"Skipping assembly '{source.ItemSpec}' because it is not a user assembly and we don't need JLOs from non-user assemblies");
				return false;
			}

			return true;
		}

		// Names of assemblies which don't have Mono.Android.dll references, or are framework assemblies, but which must
		// be scanned for Java types.
		static readonly HashSet<string> SpecialAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
			"Java.Interop.dll",
			"Mono.Android.dll",
			"Mono.Android.Runtime.dll",
		};


		bool IsAndroidAssembly (ITaskItem source)
		{
			string name = Path.GetFileName (source.ItemSpec);

			if (SpecialAssemblies.Contains (name))
				return true;

			return MonoAndroidHelper.IsMonoAndroidAssembly (source);
		}

		void CopyIfChanged (ITaskItem source, ITaskItem destination)
		{
			if (AlreadyLinked)
				return;

			if (MonoAndroidHelper.CopyAssemblyAndSymbols (source.ItemSpec, destination.ItemSpec)) {
				Log.LogDebugMessage ($"Copied: {destination.ItemSpec}");
			} else {
				Log.LogDebugMessage ($"Skipped unchanged file: {destination.ItemSpec}");

				// NOTE: We still need to update the timestamp on this file, or this target would run again
				File.SetLastWriteTimeUtc (destination.ItemSpec, DateTime.UtcNow);
			}
		}
	}
}
