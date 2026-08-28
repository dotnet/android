#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GenerateCompressedAssembliesNativeSourceFiles : AndroidTask
	{
		public override string TaskPrefix => "GCANSF";

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; } = [];

		public ITaskItem []? SizeSourceAssemblies { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; } = [];

		[Required]
		public string EnvironmentOutputDirectory { get; set; } = "";

		[Required]
		public bool Debug { get; set; }

		[Required]
		public bool EnableCompression { get; set; }

		[Required]
		public string ProjectFullPath { get; set; } = "";

		/// <summary>
		/// When <c>true</c>, descriptive comments are written into the generated LLVM IR.  They make
		/// the <c>.ll</c> far easier to read, but have no effect on the object code produced from it.
		/// Set from the <c>$(_AndroidEmitLlvmIrComments)</c> MSBuild property.
		/// </summary>
		public bool EmitLlvmIrComments { get; set; }

		public override bool RunTask ()
		{
			GenerateCompressedAssemblySources ();
			return !Log.HasLoggedErrors;
		}

		void GenerateCompressedAssemblySources ()
		{
			if (Debug || !EnableCompression) {
				Generate (null);
				return;
			}

			if (SizeSourceAssemblies != null) {
				Generate (CreateUpdatedCompressionInfo (SizeSourceAssemblies));
				return;
			}

			Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> perArchAssemblies = MonoAndroidHelper.GetPerArchAssemblies (
				ResolvedAssemblies,
				SupportedAbis,
				validate: true,
				shouldSkip: (ITaskItem asm) => bool.TryParse (asm.GetMetadata ("AndroidSkipAddToPackage"), out bool value) && value
			);
			var archAssemblies = new Dictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> ();
			var counters = new Dictionary<AndroidTargetArch, uint> ();

			foreach (var kvpPerArch in perArchAssemblies) {
				AndroidTargetArch arch = kvpPerArch.Key;
				Dictionary<string, ITaskItem> resolvedArchAssemblies = kvpPerArch.Value;

				foreach (var kvp in resolvedArchAssemblies) {
					ITaskItem assembly = kvp.Value;

					if (!archAssemblies.TryGetValue (arch, out Dictionary<string, CompressedAssemblyInfo> assemblies)) {
						assemblies = new Dictionary<string, CompressedAssemblyInfo> (StringComparer.OrdinalIgnoreCase);
						archAssemblies.Add (arch, assemblies);
					}

					var assemblyKey = CompressedAssemblyInfo.GetDictionaryKey (assembly);
					if (assemblies.ContainsKey (assemblyKey)) {
						Log.LogDebugMessage ($"Skipping duplicate assembly: {assembly.ItemSpec} (arch {MonoAndroidHelper.GetAssemblyAbi(assembly)})");
						continue;
					}

					var fi = new FileInfo (assembly.ItemSpec);
					if (!fi.Exists) {
						Log.LogCodedError ("XA2025", Properties.Resources.XA2025, assembly.ItemSpec);
						continue;
					}


					if (!counters.TryGetValue (arch, out uint counter)) {
						counter = 0;
					}
					assemblies.Add (assemblyKey, new CompressedAssemblyInfo (checked((uint)fi.Length), counter++, arch, Path.GetFileNameWithoutExtension (assembly.ItemSpec)));
					counters[arch] = counter;
				}
			}

			string key = CompressedAssemblyInfo.GetKey (ProjectFullPath);
			Log.LogDebugMessage ($"Storing compression assemblies info with key '{key}'");
			BuildEngine4.RegisterTaskObjectAssemblyLocal (key, archAssemblies, RegisteredTaskObjectLifetime.Build);
			Generate (archAssemblies);

			void Generate (Dictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>? dict)
			{
				var composer = new CompressedAssembliesNativeAssemblyGenerator (Log, dict) {
					EmitComments = EmitLlvmIrComments,
				};
				LLVMIR.LlvmIrModule compressedAssemblies = composer.Construct ();

				foreach (string abi in SupportedAbis) {
					string baseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"compressed_assemblies.{abi.ToLowerInvariant ()}");
					string llvmIrFilePath = $"{baseAsmFilePath}.ll";

					using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
						try {
							composer.Generate (compressedAssemblies, GenerateNativeApplicationConfigSources.GetAndroidTargetArchForAbi (abi), sw, llvmIrFilePath);
						} catch {
							throw;
						} finally {
							sw.Flush ();
						}

						if (Files.CopyIfStreamChanged (sw.BaseStream, llvmIrFilePath)) {
							Log.LogDebugMessage ($"File {llvmIrFilePath} was regenerated");
						}
					}

				}
			}
		}

		Dictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> CreateUpdatedCompressionInfo (ITaskItem [] sizeSourceAssemblies)
		{
			string registrationKey = CompressedAssemblyInfo.GetKey (ProjectFullPath);
			var registered = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<Dictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>> (
				registrationKey,
				RegisteredTaskObjectLifetime.Build
			);
			if (registered == null) {
				throw new InvalidOperationException ($"Compression assembly information with key '{registrationKey}' has not been generated.");
			}

			var sources = new Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> ();
			foreach (ITaskItem source in sizeSourceAssemblies) {
				if (bool.TryParse (source.GetMetadata ("AndroidSkipAddToPackage"), out bool skip) && skip) {
					continue;
				}

				AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (source);
				if (!sources.TryGetValue (arch, out Dictionary<string, ITaskItem>? archSources)) {
					archSources = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
					sources.Add (arch, archSources);
				}

				string assemblyKey = CompressedAssemblyInfo.GetDictionaryKey (source);
				if (!archSources.TryGetValue (assemblyKey, out ITaskItem? existing)) {
					archSources.Add (assemblyKey, source);
					continue;
				}

				if (new FileInfo (existing.ItemSpec).Length != new FileInfo (source.ItemSpec).Length) {
					throw new InvalidOperationException ($"Size-source assemblies '{existing.ItemSpec}' and '{source.ItemSpec}' have the same package key but different sizes.");
				}
			}

			var updated = new Dictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> ();
			foreach (var archEntry in registered) {
				if (!sources.TryGetValue (archEntry.Key, out Dictionary<string, ITaskItem>? archSources)) {
					throw new InvalidOperationException ($"Could not find size-source assemblies for architecture '{archEntry.Key}'.");
				}

				var archAssemblies = new Dictionary<string, CompressedAssemblyInfo> (StringComparer.OrdinalIgnoreCase);
				foreach (var assemblyEntry in archEntry.Value) {
					if (!archSources.TryGetValue (assemblyEntry.Key, out ITaskItem? source)) {
						throw new InvalidOperationException ($"Could not find a size-source assembly matching package key '{assemblyEntry.Key}' for architecture '{archEntry.Key}'.");
					}

					var fi = new FileInfo (source.ItemSpec);
					if (!fi.Exists) {
						throw new FileNotFoundException ($"Size-source assembly '{source.ItemSpec}' does not exist.", source.ItemSpec);
					}

					CompressedAssemblyInfo info = assemblyEntry.Value;
					archAssemblies.Add (assemblyEntry.Key, new CompressedAssemblyInfo (checked((uint)fi.Length), info.DescriptorIndex, info.TargetArch, info.AssemblyName));
				}
				updated.Add (archEntry.Key, archAssemblies);
			}
			return updated;
		}
	}
}
