using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Properties = Xamarin.Android.Tasks.Properties;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Android.Tasks;

public class LinkNativeAotSharedLibrary : AsyncTask
{
	static readonly string [] ArmEhabiPersonalitySymbols = [
		"__aeabi_unwind_cpp_pr0",
		"__aeabi_unwind_cpp_pr1",
		"__aeabi_unwind_cpp_pr2",
	];

	public override string TaskPrefix => "LNAS";

	[Required]
	public string LinkerToolPath { get; set; } = "";

	[Required]
	public string ObjcopyToolPath { get; set; } = "";

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	[Required]
	public string NativeObject { get; set; } = "";

	[Required]
	public string OutputSharedLibrary { get; set; } = "";

	[Required]
	public ITaskItem [] NativeLibraries { get; set; } = [];

	[Required]
	public string Abi { get; set; } = "";

	public ITaskItem []? AdditionalObjectFiles { get; set; }
	public ITaskItem []? CrtStartFiles { get; set; }
	public ITaskItem []? CrtEndFiles { get; set; }
	public ITaskItem []? CompilerRuntimeLibraries { get; set; }
	public ITaskItem []? SystemLibraries { get; set; }
	public ITaskItem []? LibrarySearchPaths { get; set; }
	public string? ExportsFile { get; set; }
	public string? LinkerScript { get; set; }
	public string? LinkerScriptContent { get; set; }
	public string? ExtraLinkerArgs { get; set; }
	public bool DebugBuild { get; set; }

	string ArmEhabiArchive => Path.Combine (IntermediateOutputPath, "libRuntime.WorkstationGC.arm-ehabi.a");

	public override async Task RunTaskAsync ()
	{
		bool success = false;
		try {
			success = await LinkAsync ().ConfigureAwait (false);
		} finally {
			// A failed link or debug-info operation must not leave outputs that make
			// the next incremental build skip the link target.
			if (!success) {
				File.Delete (OutputSharedLibrary);
				File.Delete (Path.ChangeExtension (OutputSharedLibrary, ".dbg.so"));
				if (Abi == "armeabi-v7a") {
					File.Delete (ArmEhabiArchive);
				}
			}
		}
	}

	async Task<bool> LinkAsync ()
	{
		Directory.CreateDirectory (IntermediateOutputPath);
		Directory.CreateDirectory (Path.GetDirectoryName (Path.GetFullPath (OutputSharedLibrary)) ?? throw new InvalidOperationException ("Missing output directory."));

		if (!string.IsNullOrEmpty (LinkerScriptContent) && !string.IsNullOrEmpty (LinkerScript)) {
			Directory.CreateDirectory (Path.GetDirectoryName (Path.GetFullPath (LinkerScript)) ?? throw new InvalidOperationException ("Missing linker script directory."));
			File.WriteAllText (LinkerScript, LinkerScriptContent);
		}

		var libraries = new List<ITaskItem> ();
		foreach (var library in NativeLibraries) {
			if (Abi != "armeabi-v7a" || Path.GetFileName (library.ItemSpec) != "libRuntime.WorkstationGC.a") {
				libraries.Add (library);
				continue;
			}

			// NativeAOT's private libunwind implements these as local symbols. Promote
			// them in a copy instead of linking a second libunwind from the NDK.
			var args = new List<string> ();
			foreach (string symbol in ArmEhabiPersonalitySymbols) {
				args.Add ($"--globalize-symbol={symbol}");
				args.Add ($"--weaken-symbol={symbol}");
			}
			args.Add (library.ItemSpec);
			args.Add (ArmEhabiArchive);
			if (!await RunToolAsync (ObjcopyToolPath, "XA3007", Properties.Resources.XA3007, args.ToArray ()).ConfigureAwait (false)) {
				return false;
			}
			libraries.Add (new TaskItem (library) { ItemSpec = ArmEhabiArchive });
		}

		string responseFile = Path.Combine (IntermediateOutputPath, $"ld.{Path.GetFileNameWithoutExtension (OutputSharedLibrary)}.{Abi}.rsp");
		WriteResponseFile (responseFile, libraries);
		if (!await RunToolAsync (LinkerToolPath, "XA3007", Properties.Resources.XA3007,
			"@" + responseFile, "-o", OutputSharedLibrary).ConfigureAwait (false)) {
			return false;
		}
		if (DebugBuild) {
			return true;
		}

		string debugFile = Path.ChangeExtension (OutputSharedLibrary, ".dbg.so");
		if (!await RunToolAsync (ObjcopyToolPath, "XA3008", Properties.Resources.XA3008,
			"--only-keep-debug", OutputSharedLibrary, debugFile).ConfigureAwait (false)) {
			return false;
		}
		if (!await RunToolAsync (ObjcopyToolPath, "XA3008", Properties.Resources.XA3008,
			"--strip-debug", "--strip-unneeded", OutputSharedLibrary).ConfigureAwait (false)) {
			return false;
		}
		return await RunToolAsync (ObjcopyToolPath, "XA3008", Properties.Resources.XA3008,
			"--add-gnu-debuglink=" + debugFile, OutputSharedLibrary).ConfigureAwait (false);
	}

	internal void WriteResponseFile (string path, IEnumerable<ITaskItem> libraries)
	{
		using var writer = new StreamWriter (path, append: false, new UTF8Encoding (false));
		foreach (string arg in new [] {
			"--shared", "--gc-sections", "-z relro", "-z noexecstack", "-z now",
			"--enable-new-dtags", "--build-id=sha1", "--warn-shared-textrel",
			"--fatal-warnings", "--no-rosegment", "--export-dynamic", "--no-undefined",
			"--eh-frame-hdr", "-EL", "--hash-style=both", "--discard-all", "--as-needed", "-e 0x0",
		}) {
			writer.WriteLine (arg);
		}
		writer.WriteLine ("-soname " + QuoteFileName (Path.GetFileName (OutputSharedLibrary)));
		writer.WriteLine ("-m " + (Abi switch {
			"armeabi-v7a" => "armelf_linux_eabi",
			"arm64-v8a" => "aarch64linux",
			"x86_64" => "elf_x86_64",
			_ => throw new NotSupportedException ($"Unsupported Android target architecture ABI: {Abi}"),
		}));
		writer.WriteLine (Abi == "armeabi-v7a" ? "-z max-page-size=4096" : "-z max-page-size=16384");
		if (Abi == "armeabi-v7a") {
			writer.WriteLine ("-X");
		} else if (Abi == "arm64-v8a") {
			writer.WriteLine ("--fix-cortex-a53-843419");
		}
		if (!string.IsNullOrEmpty (ExportsFile)) {
			writer.WriteLine ("--version-script=" + QuoteFileName (ExportsFile));
		}
		if (!string.IsNullOrEmpty (LinkerScript)) {
			writer.WriteLine ("-T " + QuoteFileName (LinkerScript));
		}
		foreach (var directory in LibrarySearchPaths ?? []) {
			writer.WriteLine ("-L " + QuoteFileName (directory.ItemSpec));
		}
		foreach (string arg in ExtraLinkerArgs?.Split (';', StringSplitOptions.RemoveEmptyEntries) ?? []) {
			writer.WriteLine (arg.Trim ());
		}

		var excludedExports = new List<string> ();
		WriteLibraries (CrtStartFiles);
		writer.WriteLine (QuoteFileName (NativeObject));
		WriteLibraries (libraries);
		foreach (var library in SystemLibraries ?? []) {
			writer.WriteLine ("-l" + QuoteFileName (library.ItemSpec));
		}
		WriteLibraries (AdditionalObjectFiles);
		WriteLibraries (CompilerRuntimeLibraries);
		if (excludedExports.Count > 0) {
			writer.WriteLine ("--exclude-libs=" + QuoteFileName (string.Join (",", excludedExports)));
		}
		WriteLibraries (CrtEndFiles);

		void WriteLibraries (IEnumerable<ITaskItem>? items)
		{
			foreach (var item in items ?? []) {
				bool wholeArchive = item.GetMetadataOrDefault (KnownMetadata.NativeLinkWholeArchive, false);
				if (item.GetMetadataOrDefault (KnownMetadata.NativeDontExportSymbols, false)) {
					excludedExports.Add (Path.GetFileName (item.ItemSpec));
				}
				if (wholeArchive) {
					writer.WriteLine ("--whole-archive");
				}
				writer.WriteLine (QuoteFileName (item.ItemSpec));
				if (wholeArchive) {
					writer.WriteLine ("--no-whole-archive");
				}
			}
		}
	}

	async Task<bool> RunToolAsync (string tool, string errorCode, string errorMessage, params string [] arguments)
	{
		using var stdout = new StringWriter (CultureInfo.InvariantCulture);
		using var stderr = new StringWriter (CultureInfo.InvariantCulture);
		LogDebugMessage ("{0} {1}", tool, string.Join (" ", arguments));
		int exitCode;
		try {
			exitCode = await ExecuteToolAsync (tool, arguments, stdout, stderr).ConfigureAwait (false);
		} catch (Win32Exception e) {
			LogCodedError (errorCode, errorMessage, Path.GetFileName (OutputSharedLibrary), Environment.NewLine + e.Message);
			return false;
		}
		if (exitCode != 0) {
			LogCodedError (errorCode, errorMessage, Path.GetFileName (OutputSharedLibrary),
				$"{Environment.NewLine}{Path.GetFileName (tool)} exited with code {exitCode}.{Environment.NewLine}{stdout}{stderr}");
			return false;
		}
		LogDebugMessage (stdout.ToString ());
		LogDebugMessage (stderr.ToString ());
		return true;
	}

	protected virtual Task<int> ExecuteToolAsync (string tool, string [] arguments, TextWriter stdout, TextWriter stderr)
		=> ProcessUtils.StartProcess (ProcessUtils.CreateProcessStartInfo (tool, arguments), stdout, stderr, CancellationToken);

	static string QuoteFileName (string name)
	{
		var command = new CommandLineBuilder ();
		command.AppendFileNameIfNotNull (name);
		return command.ToString ();
	}
}
