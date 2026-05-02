#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Links a NativeAOT shared library (.so) from the ILC-compiled object file and runtime archives.
/// Uses ld.lld directly instead of clang, matching the approach used by NativeLinker for CoreCLR/Mono.
/// </summary>
public class LinkNativeAotSharedLibrary : AndroidTask
{
	public override string TaskPrefix => "LNAS";

	[Required]
	public string AndroidBinUtilsDirectory { get; set; } = "";

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	[Required]
	public ITaskItem [] RuntimePackLibraryDirectories { get; set; } = [];

	/// <summary>
	/// The ILC-compiled object file (e.g., TestApp.o)
	/// </summary>
	[Required]
	public ITaskItem NativeObject { get; set; } = null!; // NRT - guarded by [Required]

	/// <summary>
	/// The output shared library path (e.g., libTestApp.so)
	/// </summary>
	[Required]
	public ITaskItem OutputSharedLibrary { get; set; } = null!; // NRT - guarded by [Required]

	/// <summary>
	/// Runtime and BCL static archives to link (e.g., libSystem.Native.a, libRuntime.WorkstationGC.a)
	/// </summary>
	[Required]
	public ITaskItem [] NativeLibraries { get; set; } = [];

	/// <summary>
	/// Additional object files to link (e.g., jni_init_funcs.o, environment.o, libbootstrapperdll.o)
	/// </summary>
	public ITaskItem []? AdditionalObjectFiles { get; set; }

	/// <summary>
	/// CRT start files (e.g., crtbegin_so.o) — linked first
	/// </summary>
	public ITaskItem []? CrtStartFiles { get; set; }

	/// <summary>
	/// CRT end files (e.g., crtend_so.o) — linked last
	/// </summary>
	public ITaskItem []? CrtEndFiles { get; set; }

	/// <summary>
	/// Compiler-rt and unwinder libraries to link after user libraries (explicit file paths)
	/// </summary>
	public ITaskItem []? CompilerRuntimeLibraries { get; set; }

	/// <summary>
	/// System libraries to link with -l (e.g., "dl", "c", "m", "z", "log")
	/// </summary>
	public ITaskItem []? SystemLibraries { get; set; }

	/// <summary>
	/// Additional library search paths (e.g., NDK sysroot paths)
	/// </summary>
	public ITaskItem []? LibrarySearchPaths { get; set; }

	/// <summary>
	/// Version script for symbol visibility (e.g., TestApp.exports)
	/// </summary>
	public string? ExportsFile { get; set; }

	/// <summary>
	/// Linker script (e.g., sections.ld for __modules retention)
	/// </summary>
	public string? LinkerScript { get; set; }

	/// <summary>
	/// Linker script content to write before linking
	/// </summary>
	public string? LinkerScriptContent { get; set; }

	[Required]
	public string SupportedAbis { get; set; } = "";

	public bool DebugBuild { get; set; }

	public override bool RunTask ()
	{
		foreach (string abi in SupportedAbis.Split (new [] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
			if (!LinkForAbi (abi)) {
				return false;
			}
		}
		return true;
	}

	bool LinkForAbi (string abi)
	{
		var linker = new NativeLinker (
			Log,
			abi,
			Path.GetFileName (OutputSharedLibrary.ItemSpec),
			AndroidBinUtilsDirectory,
			IntermediateOutputPath,
			RuntimePackLibraryDirectories
		) {
			StripDebugSymbols = !DebugBuild,
			SaveDebugSymbols = !DebugBuild,
			AllowUndefinedSymbols = false,

			// NativeAOT-specific options
			ExportDynamic = true,
			UseEhFrameHdr = true,
			DiscardAll = true,
			AsNeeded = true,
			HashStyleBoth = true,
			LittleEndian = true,
			EntryPoint = "0x0",
			CompressDebugSections = "zlib",
		};

		if (!ExportsFile.IsNullOrEmpty ()) {
			linker.VersionScript = ExportsFile;
		}

		// Write linker script if content is provided
		if (!LinkerScriptContent.IsNullOrEmpty () && !LinkerScript.IsNullOrEmpty ()) {
			Directory.CreateDirectory (Path.GetDirectoryName (LinkerScript)!);
			File.WriteAllText (LinkerScript, LinkerScriptContent);
		}

		if (!LinkerScript.IsNullOrEmpty ()) {
			linker.LinkerScript = LinkerScript;
		}

		if (LibrarySearchPaths != null) {
			linker.AdditionalSearchPaths = new List<string> ();
			foreach (var path in LibrarySearchPaths) {
				linker.AdditionalSearchPaths.Add (path.ItemSpec);
			}
		}

		// Build the link items in order:
		// 1. ILC object file
		// 2. Native libraries (.a archives from ILC runtime pack)
		// 3. System libraries (-ldl, -lz, -llog, -lm, -lc)
		// 4. Additional object files (jni_init, environment, etc.)
		// 5. Compiler-rt and unwinder libraries
		var linkItems = new List<ITaskItem> ();

		linkItems.Add (CopyItemWithAbi (NativeObject, abi));

		foreach (var lib in NativeLibraries) {
			linkItems.Add (CopyItemWithAbi (lib, abi));
		}

		if (SystemLibraries != null) {
			foreach (var lib in SystemLibraries) {
				linkItems.Add (NativeLinker.MakeLibraryItem (lib.ItemSpec, abi));
			}
		}

		if (AdditionalObjectFiles != null) {
			foreach (var obj in AdditionalObjectFiles) {
				linkItems.Add (CopyItemWithAbi (obj, abi));
			}
		}

		if (CompilerRuntimeLibraries != null) {
			foreach (var lib in CompilerRuntimeLibraries) {
				linkItems.Add (CopyItemWithAbi (lib, abi));
			}
		}

		// CRT start/end files
		List<ITaskItem>? startFiles = null;
		if (CrtStartFiles != null && CrtStartFiles.Length > 0) {
			startFiles = new List<ITaskItem> ();
			foreach (var crt in CrtStartFiles) {
				startFiles.Add (CopyItemWithAbi (crt, abi));
			}
		}

		List<ITaskItem>? endFiles = null;
		if (CrtEndFiles != null && CrtEndFiles.Length > 0) {
			endFiles = new List<ITaskItem> ();
			foreach (var crt in CrtEndFiles) {
				endFiles.Add (CopyItemWithAbi (crt, abi));
			}
		}

		var output = CopyItemWithAbi (OutputSharedLibrary, abi);

		return linker.Link (output, linkItems, startFiles, endFiles);
	}

	/// <summary>
	/// Copy a task item preserving all metadata, then set or override the Abi metadata.
	/// </summary>
	static ITaskItem CopyItemWithAbi (ITaskItem source, string abi)
	{
		var item = new TaskItem (source);
		item.SetMetadata (KnownMetadata.Abi, abi);
		return item;
	}
}
