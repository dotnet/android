#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class LinkApplicationSharedLibraries : AsyncTask
	{
		public override string TaskPrefix => "LAS";

		sealed class Config
		{
			public NativeLinker Linker;
			public List<ITaskItem> LinkItems;
			public ITaskItem OutputSharedLibrary;
		}

		[Required]
		public ITaskItem[] ObjectFiles { get; set; }

		[Required]
		public ITaskItem[] ApplicationSharedLibraries { get; set; }

		[Required]
		public ITaskItem IntermediateOutputPath { get; set; }

		[Required]
		public bool DebugBuild { get; set; }

		[Required]
		public string AndroidBinUtilsDirectory { get; set; }

		[Required]
		public ITaskItem[] RuntimePackLibraryDirectories { get; set; } = Array.Empty<ITaskItem> ();

		[Required]
		public bool TargetsCLR { get; set; }

		public int ZipAlignmentPages { get; set; } = AndroidZipAlign.DefaultZipAlignment64Bit;

		public override System.Threading.Tasks.Task RunTaskAsync ()
		{
			return this.WhenAll (GetLinkerConfigs (), RunLinker);
		}

		void RunLinker (Config config)
		{
			config.Linker.Link (config.OutputSharedLibrary, config.LinkItems);
		}

		IEnumerable<Config> GetLinkerConfigs ()
		{
			string soname = TargetsCLR ? "libxamarin-app-clr.so" : "libxamarin-app.so";
			foreach (ITaskItem item in ApplicationSharedLibraries) {
				string abi = item.GetMetadata ("abi");
				var linker = new NativeLinker (
					Log,
					abi,
					soname,
					AndroidBinUtilsDirectory,
					IntermediateOutputPath.ItemSpec,
					RuntimePackLibraryDirectories,
					CancellationToken
				) {
					AllowUndefinedSymbols = true,
					StripDebugSymbols = !DebugBuild,
					SaveDebugSymbols = !DebugBuild,
					ZipAlignmentPages = ZipAlignmentPages,
				};

				yield return new Config {
					Linker = linker,
					LinkItems = GatherFilesForABI (item, abi, ObjectFiles),
					OutputSharedLibrary = item,
				};
			}
		}

		List<ITaskItem> GatherFilesForABI (ITaskItem runtimeSharedLibrary, string abi, ITaskItem[] objectFiles)
		{
			List<ITaskItem> inputs = GetItemsForABI (abi, objectFiles);
			inputs.Add (NativeLinker.MakeLibraryItem ("c", abi));

			return inputs;
		}

		List<ITaskItem> GetItemsForABI (string abi, ITaskItem[] items)
		{
			var ret = new List <ITaskItem> ();
			foreach (ITaskItem item in items) {
				if (String.Compare (abi, item.GetMetadata (KnownMetadata.Abi), StringComparison.Ordinal) != 0) {
					continue;
				}
				ret.Add (item);
			}

			return ret;
		}
	}
}
