#nullable enable
using System.Collections.Generic;

using Java.Interop.Tools.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Linker.Steps;
using MonoDroid.Tuner;
using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task is for Debug builds where LinkMode=None, LinkAssemblies is for Release builds
	/// </summary>
	public class LinkAssembliesNoShrink : AndroidTask
	{
		sealed class RunState
		{
			public DirectoryAssemblyResolver? resolver = null;
			public FixAbstractMethodsStep? fixAbstractMethodsStep = null;
			public AddKeepAlivesStep? addKeepAliveStep = null;
			public FixLegacyResourceDesignerStep? fixLegacyResourceDesignerStep = null;
		}

		public override string TaskPrefix => "LNS";

		/// <summary>
		/// These are used so we have the full list of SearchDirectories
		/// </summary>
		[Required]
		public ITaskItem [] ResolvedAssemblies { get; set; } = Array.Empty<ITaskItem> ();

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

		public bool UseDesignerAssembly { get; set; }

		public bool Deterministic { get; set; }

		/// <summary>
		/// Defaults to false, enables Mono.Cecil to load symbols
		/// </summary>
		public bool ReadSymbols { get; set; }

		public override bool RunTask ()
		{
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
				}

				DoRunTask (source, destination, runState, writerParameters);
			}
			runState.resolver?.Dispose ();
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

		void DoRunTask (ITaskItem source, ITaskItem destination, RunState runState, WriterParameters writerParameters)
		{
			var assemblyName = Path.GetFileNameWithoutExtension (source.ItemSpec);

			// In .NET 6+, we can skip the main assembly
			if (!AddKeepAlives && assemblyName == TargetName) {
				CopyIfChanged (source, destination);
				return;
			}
			if (MonoAndroidHelper.IsFrameworkAssembly (source)) {
				CopyIfChanged (source, destination);
				return;
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
					return;
				}
			}

			CopyIfChanged (source, destination);
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
