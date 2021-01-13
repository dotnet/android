using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

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

			var assemblies = new SortedDictionary<string, CompressedAssemblyInfo> (StringComparer.Ordinal);
			foreach (ITaskItem assembly in ResolvedAssemblies) {
				if (bool.TryParse (assembly.GetMetadata ("AndroidSkipAddToPackage"), out bool value) && value) {
					continue;
				}

				if (assemblies.ContainsKey (assembly.ItemSpec)) {
					continue;
				}

				var fi = new FileInfo (assembly.ItemSpec);
				if (!fi.Exists) {
					Log.LogError ($"Assembly {assembly.ItemSpec} does not exist");
					continue;
				}

				assemblies.Add (CompressedAssemblyInfo.GetDictionaryKey (assembly),
					new CompressedAssemblyInfo (checked((uint)fi.Length)));
			}

			uint index = 0;
			foreach (var kvp in assemblies) {
				kvp.Value.DescriptorIndex = index++;
			}

			string key = CompressedAssemblyInfo.GetKey (ProjectFullPath);
			Log.LogDebugMessage ($"Storing compression assemblies info with key '{key}'");
			BuildEngine4.RegisterTaskObjectAssemblyLocal (key, assemblies, RegisteredTaskObjectLifetime.Build);
			Generate (assemblies);

			void Generate (IDictionary<string, CompressedAssemblyInfo> dict)
			{
				foreach (string abi in SupportedAbis) {
					NativeAssemblerTargetProvider asmTargetProvider = GeneratePackageManagerJava.GetAssemblyTargetProvider (abi);
					string baseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"compressed_assemblies.{abi.ToLowerInvariant ()}");
					string asmFilePath = $"{baseAsmFilePath}.s";
					var asmgen = new CompressedAssembliesNativeAssemblyGenerator (dict, asmTargetProvider, baseAsmFilePath);

					using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
						asmgen.Write (sw);
						sw.Flush ();
						if (MonoAndroidHelper.CopyIfStreamChanged (sw.BaseStream, asmFilePath)) {
							Log.LogDebugMessage ($"File {asmFilePath} was regenerated");
						}
					}
				}
			}
		}
	}
}
