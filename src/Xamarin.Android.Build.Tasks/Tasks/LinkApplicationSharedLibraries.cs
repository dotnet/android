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
					StripDebugSymbols = !DebugBuild,
					SaveDebugSymbols = !DebugBuild,
					ZipAlignmentPages = ZipAlignmentPages,
				};

			string stripSymbolsArg = DebugBuild ? String.Empty : " -s";

			string ld = Path.Combine (AndroidBinUtilsDirectory, MonoAndroidHelper.GetExecutablePath (AndroidBinUtilsDirectory, "ld"));
			var targetLinkerArgs = new List<string> ();
			foreach (var kvp in abis) {
				string abi = kvp.Key;
				InputFiles inputs = kvp.Value;

				targetLinkerArgs.Clear ();
				string elf_arch;
				uint maxPageSize;
				switch (abi) {
					case "armeabi-v7a":
						targetLinkerArgs.Add ("-X");
						elf_arch = "armelf_linux_eabi";
						maxPageSize = MonoAndroidHelper.ZipAlignmentToPageSize (AndroidZipAlign.ZipAlignment32Bit);
						break;

					case "arm64":
					case "arm64-v8a":
					case "aarch64":
						targetLinkerArgs.Add ("--fix-cortex-a53-843419");
						elf_arch = "aarch64linux";
						maxPageSize = MonoAndroidHelper.ZipAlignmentToPageSize (ZipAlignmentPages);
						break;

					case "x86":
						elf_arch = "elf_i386";
						maxPageSize = MonoAndroidHelper.ZipAlignmentToPageSize (AndroidZipAlign.ZipAlignment32Bit);
						break;

					case "x86_64":
						elf_arch = "elf_x86_64";
						maxPageSize = MonoAndroidHelper.ZipAlignmentToPageSize (ZipAlignmentPages);
						break;

					default:
						throw new NotSupportedException ($"Unsupported Android target architecture ABI: {abi}");
				}

				targetLinkerArgs.Add ("-m");
				targetLinkerArgs.Add (elf_arch);

				foreach (string file in inputs.ObjectFiles) {
					targetLinkerArgs.Add (MonoAndroidHelper.QuoteFileNameArgument (file));
				}

				targetLinkerArgs.Add ("-o");
				targetLinkerArgs.Add (MonoAndroidHelper.QuoteFileNameArgument (inputs.OutputSharedLibrary));

				if (inputs.ExtraLibraries != null) {
					foreach (string lib in inputs.ExtraLibraries) {
						targetLinkerArgs.Add (lib);
					}
				}

				targetLinkerArgs.Add ("-z");
				targetLinkerArgs.Add ($"max-page-size={maxPageSize}");

				string targetArgs = String.Join (" ", targetLinkerArgs);
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

		void OnOutputData (string linkerName, object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				LogMessage ($"[{linkerName} stdout] {e.Data}");
		}

		void OnErrorData (string linkerName, object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				LogMessage ($"[{linkerName} stderr] {e.Data}");
		}
	}
}
