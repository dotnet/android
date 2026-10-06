// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Creates the native assembly containing the remaining assembly-store runtime state.
	/// </summary>
	public class GenerateNativeApplicationConfigSources : AndroidTask
	{
		public override string TaskPrefix => "GCA";

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; } = [];

		public ITaskItem[]? AdditionalResolvedAssemblies { get; set; }

		public ITaskItem[]? SatelliteAssemblies { get; set; }

		[Required]
		public string EnvironmentOutputDirectory { get; set; } = "";

		[Required]
		public string [] SupportedAbis { get; set; } = [];

		/// <summary>
		/// When <c>true</c>, descriptive comments are written into the generated LLVM IR.  They make
		/// the <c>.ll</c> far easier to read, but have no effect on the object code produced from it.
		/// Set from the <c>$(_AndroidEmitLlvmIrComments)</c> MSBuild property.
		/// </summary>
		public bool EmitLlvmIrComments { get; set; }

		static internal AndroidTargetArch GetAndroidTargetArchForAbi (string abi) => MonoAndroidHelper.AbiToTargetArch (abi);

		public override bool RunTask ()
		{
			int assemblyCount = 0;
			HashSet<string>? archAssemblyNames = null;
			Action<ITaskItem> updateAssemblyCount = (ITaskItem assembly) => {
				string? culture = MonoAndroidHelper.GetAssemblyCulture (assembly);
				string fileName = Path.GetFileName (assembly.ItemSpec);
				string assemblyName;

				if (String.IsNullOrEmpty (culture)) {
					assemblyName = fileName;
				} else {
					assemblyName = $"{culture}/{fileName}";
				}

				archAssemblyNames ??= new HashSet<string> (StringComparer.OrdinalIgnoreCase);

				if (!archAssemblyNames.Contains (assemblyName)) {
					assemblyCount++;
					archAssemblyNames.Add (assemblyName);
				}
			};

			static bool ShouldSkipAssembly (ITaskItem assembly)
			{
				return assembly.GetMetadataOrDefault ("AndroidSkipAddToPackage", false);
			}

			if (SatelliteAssemblies != null) {
				foreach (ITaskItem assembly in SatelliteAssemblies) {
					if (ShouldSkipAssembly (assembly)) {
						continue;
					}

					updateAssemblyCount (assembly);
				}
			}

			foreach (var assembly in ResolvedAssemblies) {
				if (ShouldSkipAssembly (assembly)) {
					continue;
				}

				updateAssemblyCount (assembly);
			}

			if (AdditionalResolvedAssemblies != null) {
				foreach (ITaskItem assembly in AdditionalResolvedAssemblies) {
					updateAssemblyCount (assembly);
				}
			}

			LLVMIR.LlvmIrComposer appConfigAsmGen = new AssemblyStoreNativeAssemblyGenerator (Log) {
				NumberOfAssembliesInApk = assemblyCount,
			};
			LLVMIR.LlvmIrModule appConfigModule = appConfigAsmGen.Construct ();
			appConfigAsmGen.EmitComments = EmitLlvmIrComments;

			foreach (string abi in SupportedAbis) {
				string targetAbi = abi.ToLowerInvariant ();
				string environmentBaseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"environment.{targetAbi}");
				string environmentLlFilePath  = $"{environmentBaseAsmFilePath}.ll";
				AndroidTargetArch targetArch = GetAndroidTargetArchForAbi (abi);

				using var appConfigWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
				try {
					appConfigAsmGen.Generate (appConfigModule, targetArch, appConfigWriter, environmentLlFilePath);
				} catch {
					throw;
				} finally {
					appConfigWriter.Flush ();
					Files.CopyIfStreamChanged (appConfigWriter.BaseStream, environmentLlFilePath);
				}
			}

			return !Log.HasLoggedErrors;
		}
	}
}
