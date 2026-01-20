#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Links NativeAOT-compiled object files into a shared library (.so) for Android.
/// Uses android-native-tools (our custom LLVM build) instead of the Android NDK.
/// </summary>
public class LinkNativeAotLibrary : AndroidTask
{
	public override string TaskPrefix => "LNA";

	[Required]
	public string AndroidBinUtilsDirectory { get; set; } = "";

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	/// <summary>
	/// The main object file produced by ILC ($(NativeObject)).
	/// </summary>
	[Required]
	public string NativeObject { get; set; } = "";

	/// <summary>
	/// Additional object files (e.g., generated assembler sources).
	/// </summary>
	public ITaskItem[] NativeObjectFiles { get; set; } = [];

	/// <summary>
	/// Static archives from ILC SDK and runtime packs.
	/// </summary>
	[Required]
	public ITaskItem[] NativeArchives { get; set; } = [];

	[Required]
	public string OutputLibrary { get; set; } = "";

	[Required]
	public string RuntimeIdentifier { get; set; } = "";

	[Required]
	public ITaskItem[] RuntimePackLibraryDirectories { get; set; } = [];

	public bool StripDebugSymbols { get; set; } = true;
	public bool SaveDebugSymbols { get; set; } = true;

	public override bool RunTask ()
	{
		string abi = GetAbiFromRuntimeIdentifier (RuntimeIdentifier);
		string clangArch = GetClangArchFromRuntimeIdentifier (RuntimeIdentifier);

		// Compute soname - Android requires a proper soname or it will refuse to load the library
		string soname = Path.GetFileNameWithoutExtension (OutputLibrary);
		if (soname.StartsWith ("lib", StringComparison.OrdinalIgnoreCase)) {
			soname = soname.Substring (3);
		}

		// Find the sysroot directory from runtime pack library directories
		string? sysrootDir = FindSysrootDirectory ();
		if (sysrootDir == null) {
			Log.LogError ("Could not find sysroot directory containing C++ runtime libraries in runtime pack");
			return false;
		}

		var linker = new NativeLinker (Log, abi, soname, AndroidBinUtilsDirectory, IntermediateOutputPath, RuntimePackLibraryDirectories) {
			StripDebugSymbols = StripDebugSymbols,
			SaveDebugSymbols = SaveDebugSymbols,
			AllowUndefinedSymbols = false,
			UseNdkLibraries = false,
			TargetsCLR = false, // NativeAOT uses its own runtime, not CoreCLR
			UseSymbolic = true,
			IsNativeAOT = true, // Enable NativeAOT-specific linker flags
		};

		List<ITaskItem> linkItems = OrganizeCommandLineItems (abi, sysrootDir, clangArch);
		List<ITaskItem> linkStartFiles = GetCrtStartFiles (abi, sysrootDir);
		List<ITaskItem> linkEndFiles = GetCrtEndFiles (abi, sysrootDir);

		bool success = linker.Link (
			CreateItemWithAbi (OutputLibrary, abi),
			linkItems,
			linkStartFiles,
			linkEndFiles,
			exportDynamicSymbols: null
		);

		if (!success) {
			Log.LogError ($"Failed to link NativeAOT library: {OutputLibrary}");
		}

		return success;
	}

	string GetAbiFromRuntimeIdentifier (string rid)
	{
		return rid switch {
			"android-arm64" => "arm64-v8a",
			"android-x64" => "x86_64",
			_ => throw new NotSupportedException ($"Unsupported RuntimeIdentifier for NativeAOT: {rid}")
		};
	}

	string GetClangArchFromRuntimeIdentifier (string rid)
	{
		return rid switch {
			"android-arm64" => "aarch64",
			"android-x64" => "x86_64",
			_ => throw new NotSupportedException ($"Unsupported RuntimeIdentifier for NativeAOT: {rid}")
		};
	}

	/// <summary>
	/// Finds the sysroot directory containing C++ runtime libraries.
	/// </summary>
	string? FindSysrootDirectory ()
	{
		foreach (var dir in RuntimePackLibraryDirectories) {
			string libcppPath = Path.Combine (dir.ItemSpec, "libc++_static.a");
			if (File.Exists (libcppPath)) {
				return dir.ItemSpec;
			}
		}
		return null;
	}

	/// <summary>
	/// Get CRT start files (crtbegin_so.o).
	/// </summary>
	List<ITaskItem> GetCrtStartFiles (string abi, string sysrootDir)
	{
		var items = new List<ITaskItem> ();
		string crtbegin = Path.Combine (sysrootDir, "crtbegin_so.o");
		if (File.Exists (crtbegin)) {
			items.Add (CreateItemWithAbi (crtbegin, abi));
		} else {
			Log.LogError ($"Required CRT file 'crtbegin_so.o' not found in {sysrootDir}. The NativeAOT runtime pack may be incomplete.");
		}
		return items;
	}

	/// <summary>
	/// Get CRT end files (crtend_so.o).
	/// </summary>
	List<ITaskItem> GetCrtEndFiles (string abi, string sysrootDir)
	{
		var items = new List<ITaskItem> ();
		string crtend = Path.Combine (sysrootDir, "crtend_so.o");
		if (File.Exists (crtend)) {
			items.Add (CreateItemWithAbi (crtend, abi));
		} else {
			Log.LogError ($"Required CRT file 'crtend_so.o' not found in {sysrootDir}. The NativeAOT runtime pack may be incomplete.");
		}
		return items;
	}

	/// <summary>
	/// Organizes link items in the correct order for the native linker.
	/// Order matters for static linking!
	/// </summary>
	List<ITaskItem> OrganizeCommandLineItems (string abi, string sysrootDir, string clangArch)
	{
		var items = new List<ITaskItem> ();

		// First: ILC's main object file
		items.Add (CreateItemWithAbi (NativeObject, abi));

		// Then: additional object files (generated assembler sources)
		foreach (ITaskItem objFile in NativeObjectFiles) {
			items.Add (CreateItemWithAbi (objFile.ItemSpec, abi));
		}

		// Then: static archives from ILC SDK and runtime packs
		foreach (ITaskItem archive in NativeArchives) {
			var item = CreateItemWithAbi (archive.ItemSpec, abi);
			// Check if this archive should be included with --whole-archive
			string? wholeArchive = archive.GetMetadata (KnownMetadata.NativeLinkWholeArchive);
			if (!wholeArchive.IsNullOrEmpty () && Boolean.Parse (wholeArchive)) {
				item.SetMetadata (KnownMetadata.NativeLinkWholeArchive, "true");
			}
			items.Add (item);
		}

		// C++ standard library (required by NativeAOT runtime for std::nothrow, operator new/delete, etc.)
		string libcppStatic = Path.Combine (sysrootDir, "libc++_static.a");
		if (File.Exists (libcppStatic)) {
			items.Add (CreateItemWithAbi (libcppStatic, abi));
		} else {
			Log.LogError ($"Required library 'libc++_static.a' not found in {sysrootDir}. The NativeAOT runtime pack may be incomplete.");
		}

		string libcppabi = Path.Combine (sysrootDir, "libc++abi.a");
		if (File.Exists (libcppabi)) {
			items.Add (CreateItemWithAbi (libcppabi, abi));
		} else {
			Log.LogError ($"Required library 'libc++abi.a' not found in {sysrootDir}. The NativeAOT runtime pack may be incomplete.");
		}

		// Unwinding support
		string libunwind = Path.Combine (sysrootDir, "libunwind.a");
		if (File.Exists (libunwind)) {
			items.Add (CreateItemWithAbi (libunwind, abi));
		} else {
			Log.LogError ($"Required library 'libunwind.a' not found in {sysrootDir}. The NativeAOT runtime pack may be incomplete.");
		}

		// Compiler runtime builtins (required for atomic intrinsics and TLS emulation)
		string libclangBuiltins = Path.Combine (sysrootDir, $"libclang_rt.builtins-{clangArch}-android.a");
		if (File.Exists (libclangBuiltins)) {
			items.Add (CreateItemWithAbi (libclangBuiltins, abi));
		} else {
			Log.LogError ($"Required library 'libclang_rt.builtins-{clangArch}-android.a' not found in {sysrootDir}. The NativeAOT runtime pack may be incomplete.");
		}

		// Add required system libraries (linked dynamically)
		items.Add (NativeLinker.MakeLibraryItem ("log", abi));  // Android logging
		items.Add (NativeLinker.MakeLibraryItem ("z", abi));    // zlib compression
		items.Add (NativeLinker.MakeLibraryItem ("m", abi));    // math library
		items.Add (NativeLinker.MakeLibraryItem ("dl", abi));   // dynamic linking
		items.Add (NativeLinker.MakeLibraryItem ("c", abi));    // C library (must be last)

		return items;
	}

	ITaskItem CreateItemWithAbi (string path, string abi)
	{
		var item = new TaskItem (path);
		item.SetMetadata (KnownMetadata.Abi, abi);
		return item;
	}
}
