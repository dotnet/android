using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class CreateEmbeddedAssemblyStore : AndroidTask
{
	public override string TaskPrefix => "CEAS";

	[Required]
	public string AndroidBinUtilsDirectory { get; set; }

	[Required]
	public string AppSharedLibrariesDir { get; set; }

	[Required]
	public string AssemblySourcesDir { get; set; }

	[Required]
	public string CompressedAssembliesDir { get; set; }

	[Required]
	public bool Debug { get; set; }

	[Required]
	public bool EnableCompression { get; set; }

	[Required]
	public string ProjectFullPath { get; set; }

	[Required]
	public ITaskItem[] ResolvedUserAssemblies { get; set; }

	[Required]
	public ITaskItem[] ResolvedFrameworkAssemblies { get; set; }

	[Required]
	public string [] SupportedAbis { get; set; }

	[Output]
	public ITaskItem[] NativeAssemblySources { get; set; }

	[Output]
	public ITaskItem[] EmbeddedObjectFiles { get; set; }

	public override bool RunTask ()
	{
		bool compress = !Debug && EnableCompression;
		IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>? compressedAssembliesInfo = null;

		if (compress) {
			string key = CompressedAssemblyInfo.GetKey (ProjectFullPath);
			Log.LogDebugMessage ($"[{TaskPrefix}] Retrieving assembly compression info with key '{key}'");
			compressedAssembliesInfo = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>> (key, RegisteredTaskObjectLifetime.Build);
			if (compressedAssembliesInfo == null) {
				throw new InvalidOperationException ($"Assembly compression info not found for key '{key}'. Compression will not be performed.");
			}
		}

		var storeBuilder = new AssemblyStoreBuilder (Log);

		// Add user assemblies
		AssemblyPackagingHelper.AddAssembliesFromCollection (Log, SupportedAbis, ResolvedUserAssemblies, DoAddAssembliesFromArchCollection);

		// Add framework assemblies
		AssemblyPackagingHelper.AddAssembliesFromCollection (Log, SupportedAbis, ResolvedFrameworkAssemblies, DoAddAssembliesFromArchCollection);

		var objectFiles = new List<ITaskItem> ();
		var sourceFiles = new List<ITaskItem> ();
		Dictionary<AndroidTargetArch, string> assemblyStorePaths = storeBuilder.Generate (Path.Combine (AppSharedLibrariesDir, "embedded"));
		foreach (var kvp in assemblyStorePaths) {
			string abi = MonoAndroidHelper.ArchToAbi (kvp.Key);
			string inputFile = kvp.Value;

			List<ITaskItem> items = ELFEmbeddingHelper.EmbedBinary (
				Log,
				abi,
				AndroidBinUtilsDirectory,
				inputFile,
				ELFEmbeddingHelper.KnownEmbedItems.AssemblyStore,
				AssemblySourcesDir
			);

			if (items.Count == 0) {
				continue;
			}

			objectFiles.AddRange (items);
			foreach (ITaskItem objectItem in items) {
				var sourceItem = new TaskItem (
					Path.ChangeExtension (objectItem.ItemSpec, ".s"),
					objectItem.CloneCustomMetadata ()
				);
				sourceFiles.Add (sourceItem);
			}
		}

		NativeAssemblySources = sourceFiles.ToArray ();
		EmbeddedObjectFiles = objectFiles.ToArray ();

		return !Log.HasLoggedErrors;

		void DoAddAssembliesFromArchCollection (TaskLoggingHelper log, AndroidTargetArch arch, ITaskItem assembly)
		{
			string sourcePath = CompressAssembly (assembly);
			storeBuilder.AddAssembly (sourcePath, assembly, includeDebugSymbols: Debug);
			return;
		}

		string CompressAssembly (ITaskItem assembly)
		{
			if (!compress) {
				return assembly.ItemSpec;
			}

			return AssemblyCompression.Compress (Log, assembly, compressedAssembliesInfo, CompressedAssembliesDir);
		}
	}
}
