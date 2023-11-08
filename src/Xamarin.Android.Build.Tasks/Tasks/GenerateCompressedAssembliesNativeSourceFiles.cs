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
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		[Required]
		public string EnvironmentOutputDirectory { get; set; }

		[Required]
		public bool Debug { get; set; }

		[Required]
		public bool EnableCompression { get; set; }

		[Required]
		public string ProjectFullPath { get; set; }

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

			var assemblies = new Dictionary<string, CompressedAssemblyInfo> (StringComparer.Ordinal);
			var counters = new Dictionary<AndroidTargetArch, uint> ();
			foreach (ITaskItem assembly in ResolvedAssemblies) {
				if (bool.TryParse (assembly.GetMetadata ("AndroidSkipAddToPackage"), out bool value) && value) {
					continue;
				}

				var assemblyKey = CompressedAssemblyInfo.GetDictionaryKey (assembly);
				if (assemblies.ContainsKey (assemblyKey)) {
					Log.LogDebugMessage ($"Skipping duplicate assembly: {assembly.ItemSpec}");
					continue;
				}

				var fi = new FileInfo (assembly.ItemSpec);
				if (!fi.Exists) {
					Log.LogError ($"Assembly {assembly.ItemSpec} does not exist");
					continue;
				}

				AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (assembly);
				if (!counters.TryGetValue (arch, out uint counter)) {
					counter = 0;
				}
				assemblies.Add (assemblyKey, new CompressedAssemblyInfo (checked((uint)fi.Length), counter++, arch, Path.GetFileNameWithoutExtension (assembly.ItemSpec)));
				counters[arch] = counter;
			}

			string key = CompressedAssemblyInfo.GetKey (ProjectFullPath);
			Log.LogDebugMessage ($"Storing compression assemblies info with key '{key}'");
			BuildEngine4.RegisterTaskObjectAssemblyLocal (key, assemblies, RegisteredTaskObjectLifetime.Build);
			Generate (assemblies);

			void Generate (IDictionary<string, CompressedAssemblyInfo> dict)
			{
				var composer = new CompressedAssembliesNativeAssemblyGenerator (dict);
				LLVMIR.LlvmIrModule compressedAssemblies = composer.Construct ();

				foreach (string abi in SupportedAbis) {
					string baseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"compressed_assemblies.{abi.ToLowerInvariant ()}");
					string llvmIrFilePath = $"{baseAsmFilePath}.ll";

					using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
						try {
							composer.Generate (compressedAssemblies, GeneratePackageManagerJava.GetAndroidTargetArchForAbi (abi), sw, llvmIrFilePath);
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
	}
}
