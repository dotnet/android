#nullable enable
using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Mono.Cecil;
using Mono.Linker.Steps;
using MonoDroid.Tuner;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task is called for builds that did not use ILLink. It runs "linker steps" that
	/// should be run on assemblies even in non-linked builds.
	/// </summary>
	public class LinkAssembliesNoShrink : AssemblyModifierPipeline
	{
		// Names of assemblies which don't have Mono.Android.dll references, or are framework assemblies, but which must
		public override string TaskPrefix => "LNS";

		/// <summary>
		/// $(TargetName) would be "AndroidApp1" with no extension
		/// </summary>
		[Required]
		public string TargetName { get; set; } = "";

		public bool AddKeepAlives { get; set; }

		public bool UseDesignerAssembly { get; set; }

		protected override void CreateRunState (RunState runState, MSBuildLinkContext context)
		{
			// Create the additional steps that we want to run since ILLink won't be run
			var fixAbstractMethodsStep = new FixAbstractMethodsStep ();
			fixAbstractMethodsStep.Initialize (context, new EmptyMarkContext ());
			runState.fixAbstractMethodsStep = fixAbstractMethodsStep;

			var addKeepAliveStep = new AddKeepAlivesStep ();
			addKeepAliveStep.Initialize (context);
			runState.addKeepAliveStep = addKeepAliveStep;

			var fixLegacyResourceDesignerStep = new FixLegacyResourceDesignerStep ();
			fixLegacyResourceDesignerStep.Initialize (context);
			runState.fixLegacyResourceDesignerStep = fixLegacyResourceDesignerStep;

			// base must be called to initialize AssemblyModifierPipeline steps
			base.CreateRunState (runState, context);
		}

		protected override void RunPipeline (ITaskItem source, ITaskItem destination, RunState runState, WriterParameters writerParameters)
		{
			if (!TryModifyAssembly (source, destination, runState, writerParameters)) {
				// If we didn't write a modified file, copy the original to the destination
				CopyIfChanged (source, destination);
			}

			// base must be called to run AssemblyModifierPipeline steps
			base.RunPipeline (source, destination, runState, writerParameters);
		}

		bool TryModifyAssembly (ITaskItem source, ITaskItem destination, RunState runState, WriterParameters writerParameters)
		{
			var assemblyName = Path.GetFileNameWithoutExtension (source.ItemSpec);

			// In .NET 6+, we can skip the main assembly
			if (!AddKeepAlives && assemblyName == TargetName)
				return false;

			if (MonoAndroidHelper.IsFrameworkAssembly (source))
				return false;

			// Only run steps on "MonoAndroid" assemblies
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
