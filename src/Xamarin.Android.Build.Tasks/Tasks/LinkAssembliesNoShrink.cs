using Java.Interop.Tools.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System;
using System.IO;

using MTProfile = Mono.Tuner.Profile;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task is for Debug builds where LinkMode=None, LinkAssemblies is for Release builds
	/// </summary>
	public class LinkAssembliesNoShrink : AndroidTask
	{
		public override string TaskPrefix => "LNS";

		/// <summary>
		/// These are used so we have the full list of SearchDirectories
		/// </summary>
		[Required]
		public ITaskItem [] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem [] SourceFiles { get; set; }

		[Required]
		public ITaskItem [] DestinationFiles { get; set; }

		public bool Deterministic { get; set; }

		public override bool RunTask ()
		{
			if (SourceFiles.Length != DestinationFiles.Length)
				throw new ArgumentException ("source and destination count mismatch");

			var readerParameters = new ReaderParameters {
				ReadSymbols = true,
			};
			var writerParameters = new WriterParameters {
				DeterministicMvid = Deterministic,
			};

			using (var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: true, loadReaderParameters: readerParameters)) {
				// Add SearchDirectories with ResolvedAssemblies
				foreach (var assembly in ResolvedAssemblies) {
					var path = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec));
					if (!resolver.SearchDirectories.Contains (path))
						resolver.SearchDirectories.Add (path);
				}

				// Run the FixAbstractMethodsStep
				var step = new FixAbstractMethodsStep (resolver, new TypeDefinitionCache (), Log);
				for (int i = 0; i < SourceFiles.Length; i++) {
					var source = SourceFiles [i];
					var destination = DestinationFiles [i];
					AssemblyDefinition assemblyDefinition = null;

					if (!MTProfile.IsSdkAssembly (source.ItemSpec) && !MTProfile.IsProductAssembly (source.ItemSpec)) {
						assemblyDefinition = resolver.GetAssembly (source.ItemSpec);
						step.CheckAppDomainUsage (assemblyDefinition, (string msg) => Log.LogMessageFromText (msg, MessageImportance.High));
					}

					// Only run the step on "MonoAndroid" assemblies
					if (MonoAndroidHelper.IsMonoAndroidAssembly (source) && !MonoAndroidHelper.IsSharedRuntimeAssembly (source.ItemSpec)) {
						if (assemblyDefinition == null)
							assemblyDefinition = resolver.GetAssembly (source.ItemSpec);

						if (step.FixAbstractMethods (assemblyDefinition)) {
							Log.LogDebugMessage ($"Saving modified assembly: {destination.ItemSpec}");
							writerParameters.WriteSymbols = assemblyDefinition.MainModule.HasSymbols;
							assemblyDefinition.Write (destination.ItemSpec, writerParameters);
							continue;
						}
					}

					if (MonoAndroidHelper.CopyAssemblyAndSymbols (source.ItemSpec, destination.ItemSpec)) {
						Log.LogDebugMessage ($"Copied: {destination.ItemSpec}");
					} else {
						Log.LogDebugMessage ($"Skipped unchanged file: {destination.ItemSpec}");

						// NOTE: We still need to update the timestamp on this file, or this target would run again
						File.SetLastWriteTimeUtc (destination.ItemSpec, DateTime.UtcNow);
					}
				}
			}

			return !Log.HasLoggedErrors;
		}

		class FixAbstractMethodsStep : MonoDroid.Tuner.FixAbstractMethodsStep
		{
			readonly DirectoryAssemblyResolver resolver;
			readonly TaskLoggingHelper logger;

			public FixAbstractMethodsStep (DirectoryAssemblyResolver resolver, TypeDefinitionCache cache, TaskLoggingHelper logger)
				: base (cache)
			{
				this.resolver = resolver;
				this.logger = logger;
			}

			protected override AssemblyDefinition GetMonoAndroidAssembly ()
			{
				return resolver.GetAssembly ("Mono.Android.dll");
			}

			public override void LogMessage (string message)
			{
				logger.LogDebugMessage ("{0}", message);
			}
		}
	}
}
