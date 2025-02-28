#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Linker.Steps;
using MonoDroid.Tuner;
using Xamarin.Android.Tasks.Utilities;
using Xamarin.Android.Tools;

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

		public string CodeGenerationTarget { get; set; } = "";

		public bool Debug { get; set; }

		public bool EnableMarshalMethods { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		public bool Deterministic { get; set; }

		public bool UseDesignerAssembly { get; set; }

		/// <summary>
		/// Defaults to false, enables Mono.Cecil to load symbols
		/// </summary>
		public bool ReadSymbols { get; set; }

		public override bool RunTask ()
		{
			codeGenerationTarget = MonoAndroidHelper.ParseCodeGenerationTarget (CodeGenerationTarget);

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
						CodeGenerationTarget = codeGenerationTarget,
						ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
						UserAssemblies = ResolvedUserAssemblies.Select (a => Path.GetFileNameWithoutExtension (a.ItemSpec)).ToList (),
						UseMarshalMethods = EnableMarshalMethods,
						Debug = Debug,
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

			var assemblyName = Path.GetFileNameWithoutExtension (source.ItemSpec);
			var destinationJLOXml = Path.ChangeExtension (destination.ItemSpec, ".jlo.xml");

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

				sw.Restart ();
				bool save = runState.fixAbstractMethodsStep!.FixAbstractMethods (assemblyDefinition);
				var fixAbstractMethods_ms = sw.ElapsedMilliseconds;
				sw.Restart ();
				if (UseDesignerAssembly)
					save |= runState.fixLegacyResourceDesignerStep!.ProcessAssemblyDesigner (assemblyDefinition);
				var fixLegacyResourceDesigner_ms = sw.ElapsedMilliseconds;
				sw.Restart ();
				if (AddKeepAlives)
					save |= runState.addKeepAliveStep!.AddKeepAlives (assemblyDefinition);
				var addKeepAlives_ms = sw.ElapsedMilliseconds;
				sw.Restart ();
				runState.findJavaObjectsStep!.ProcessAssembly (assemblyDefinition, destinationJLOXml);
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

		bool ShouldScanAssembly (ITaskItem source)
		{
			if (!Debug || EnableMarshalMethods && Path.GetFileName (source.ItemSpec) == "Mono.Android.dll")
				return true;

			if (MonoAndroidHelper.IsFrameworkAssembly (source))
				return false;

			if (MonoAndroidHelper.IsMonoAndroidAssembly (source))
				return true;

			return false;
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
	}
}
