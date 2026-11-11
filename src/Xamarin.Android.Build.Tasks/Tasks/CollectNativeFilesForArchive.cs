// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

using ArchiveFileList = System.Collections.Generic.List<(string filePath, string archivePath)>;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Collects native libraries to be added to the final archive.
/// </summary>
public class CollectNativeFilesForArchive : AndroidTask
{
	const string ArchiveLibPath = "lib";

	public override string TaskPrefix => "CNF";

	[Required]
	public string ApkOutputPath { get; set; } = "";

	public ITaskItem[] AdditionalNativeLibraryReferences { get; set; } = [];

	[Required]
	public ITaskItem[] FrameworkNativeLibraries { get; set; } = [];

	[Required]
	public ITaskItem[] NativeLibraries { get; set; } = [];

	public ITaskItem[] BundleNativeLibraries { get; set; } = [];

	public bool StripNativeLibraries { get; set; }

	public string? StripToolPath { get; set; }

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	public bool IncludeWrapSh { get; set; }

	public int ZipAlignmentPages { get; set; } = 16;

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	public string AndroidRuntime { get; set; } = "";

	[Output]
	public ITaskItem[] OutputFiles { get; set; } = [];

	[Output]
	public ITaskItem[] FilesToAddToArchive { get; set; } = [];

	[Output]
	public ITaskItem [] StrippedLibraries { get; set; } = [];

	readonly List<ITaskItem> strippedLibraries = new ();

	public override bool RunTask ()
	{
		strippedLibraries.Clear ();
		StrippedLibraries = [];
		if (StripNativeLibraries && !File.Exists (StripToolPath)) {
			Log.LogCodedError ("XA5105", Properties.Resources.XA5105, "llvm-strip", string.Join (";", SupportedAbis), StripToolPath ?? "");
			return false;
		}

		bool success = false;
		try {
			var apk = new PackageFileListBuilder ();
			var outputFiles = new List<string> {
				ApkOutputPath
			};

			var files = new ArchiveFileList ();
			AddNativeLibraries (files, SupportedAbis);
			AddAdditionalNativeLibraries (files, SupportedAbis);

			foreach (var file in files) {
				var item = Path.Combine (file.archivePath.Replace (Path.DirectorySeparatorChar, '/'));
				Log.LogDebugMessage ("\tAdding {0}", file.filePath);
				apk.AddItem (file.filePath, item);
			}

			// Task output parameters
			FilesToAddToArchive = apk.ToArray ();
			OutputFiles = outputFiles.Select (a => new TaskItem (a)).ToArray ();
			StrippedLibraries = strippedLibraries.ToArray ();
			success = !Log.HasLoggedErrors;
			return success;
		} finally {
			if (!success) {
				foreach (var library in strippedLibraries) {
					File.Delete (library.ItemSpec);
				}
				StrippedLibraries = [];
				FilesToAddToArchive = [];
			}
		}
	}

	string? StripNativeLibIfNecessary (string filesystemPath, string abi, string archiveFileName)
	{
		if (!StripNativeLibraries) {
			return filesystemPath;
		}

		if (filesystemPath.EndsWith (".dll.so", StringComparison.OrdinalIgnoreCase) || IsWrapperScript (filesystemPath, archiveFileName)) {
			// Wrapped assemblies and wrapper scripts have no native debug info.
			return filesystemPath;
		}

		if (StripToolPath.IsNullOrEmpty ()) {
			throw new InvalidOperationException ("The Android NDK strip tool must be resolved before collecting native libraries.");
		}

		ELFInfo? info = ELFHelper.GetInfo (Log, filesystemPath);
		if (info != null && !info.HasDebugInfo) {
			return filesystemPath;
		}

		string outputDir = Path.Combine (IntermediateOutputPath, MonoAndroidHelper.AbiToRid (abi), "stripped");
		Directory.CreateDirectory (outputDir);

		string outputFilePath = Path.Combine (outputDir, Path.GetFileName (archiveFileName));
		var args = new List<string> {
			"-o",
			MonoAndroidHelper.QuoteFileNameArgument (outputFilePath),
			MonoAndroidHelper.QuoteFileNameArgument (filesystemPath),
		};

		Log.LogDebugMessage ($"Stripping native library: '{filesystemPath}' to '{outputFilePath}'");
		bool success = false;
		try {
			if (MonoAndroidHelper.RunProcess ("llvm-strip", StripToolPath, String.Join (" ", args), Log) != 0) {
				return null; // RunProcess logged XA0142; never package an unstripped fallback.
			}
			strippedLibraries.Add (new TaskItem (outputFilePath));
			success = true;
			return outputFilePath;
		} finally {
			if (!success) {
				File.Delete (outputFilePath);
			}
		}
	}

	bool IsWrapperScript (string path, string? link)
	{
		if (Path.DirectorySeparatorChar == '/') {
			path = path.Replace ('\\', '/');
		}

		if (string.Equals (Path.GetFileName (path), "wrap.sh", StringComparison.Ordinal)) {
			return true;
		}

		if (link.IsNullOrEmpty ()) {
			return false;
		}

		if (Path.DirectorySeparatorChar == '/') {
			link = link!.Replace ('\\', '/');
		}

		return string.Equals (Path.GetFileName (link), "wrap.sh", StringComparison.Ordinal);
	}

	bool IncludeNativeLibrary (ITaskItem item)
	{
		if (IncludeWrapSh)
			return true;

		return !IsWrapperScript (item.ItemSpec, item.GetMetadata ("Link"));
	}

	string? GetArchiveFileName (ITaskItem item)
	{
		string archiveFileName = item.GetMetadata ("ArchiveFileName");
		if (!archiveFileName.IsNullOrEmpty ())
			return archiveFileName;

		if (!IsWrapperScript (item.ItemSpec, item.GetMetadata ("Link"))) {
			return null;
		}

		return "wrap.sh";
	}

	void AddNativeLibraries (ArchiveFileList files, string [] supportedAbis)
	{
		var frameworkLibs = FrameworkNativeLibraries.Select (v => new LibInfo (
			path: v.ItemSpec,
			link: v.GetMetadata ("Link"),
			abi: GetNativeLibraryAbi (v),
			archiveFileName: GetArchiveFileName (v),
			item: v
		));

		AddNativeLibraries (files, supportedAbis, frameworkLibs);

		var libs = NativeLibraries.Concat (BundleNativeLibraries ?? Enumerable.Empty<ITaskItem> ())
			.Where (v => IncludeNativeLibrary (v))
			.Select (v => new LibInfo (
					path: v.ItemSpec,
					link: v.GetMetadata ("Link"),
					abi: GetNativeLibraryAbi (v),
					archiveFileName: GetArchiveFileName (v),
					item: v
				)
			);

		AddNativeLibraries (files, supportedAbis, libs);
	}

	string? GetNativeLibraryAbi (ITaskItem lib)
	{
		// If Abi is explicitly specified, simply return it.
		var lib_abi = AndroidRidAbiHelper.GetNativeLibraryAbi (lib);

		if (lib_abi.IsNullOrWhiteSpace ()) {
			Log.LogCodedError ("XA4301", lib.ItemSpec, 0, Properties.Resources.XA4301_ABI, lib.ItemSpec);
			return null;
		}

		return lib_abi;
	}

	void AddNativeLibraries (ArchiveFileList files, string [] supportedAbis, System.Collections.Generic.IEnumerable<LibInfo> libs)
	{
		if (libs.Any (lib => lib.Abi == null))
			Log.LogCodedWarning (
					"XA4301",
					Properties.Resources.XA4301_ABI_Ignoring,
					string.Join (", ", libs.Where (lib => lib.Abi == null).Select (lib => lib.Path)));
		libs = libs.Where (lib => lib.Abi != null);
		libs = libs.Where (lib => supportedAbis.Contains (lib.Abi));
		foreach (var info in libs) {
			AddNativeLibrary (files, info.Path, info.Abi!, info.ArchiveFileName, info.Item);
		}
	}

	void AddAdditionalNativeLibraries (ArchiveFileList files, string [] supportedAbis)
	{
		if (AdditionalNativeLibraryReferences == null || !AdditionalNativeLibraryReferences.Any ())
			return;

		var libs = AdditionalNativeLibraryReferences
			.Select (l => new LibInfo (
				path: l.ItemSpec,
				link: null,
				abi: AndroidRidAbiHelper.GetNativeLibraryAbi (l),
				archiveFileName: l.GetMetadata ("ArchiveFileName"),
				item: l
			));

		AddNativeLibraries (files, supportedAbis, libs);
	}

	void AddNativeLibrary (ArchiveFileList files, string path, string abi, string? archiveFileName, ITaskItem? taskItem = null)
	{
		string fileName = archiveFileName.IsNullOrEmpty () ? Path.GetFileName (path) : archiveFileName!;
		bool needsSonameCheck = AndroidRuntime == "CoreCLR" &&
			!string.Equals (Path.GetFileName (fileName), "libbinary_blobs.so", StringComparison.Ordinal);
		bool hasReservedSoname = ELFHelper.AssertValidLibraryAlignment (
			Log, ZipAlignmentPages, path, taskItem, needsSonameCheck ? "libbinary_blobs.so" : null);
		if (hasReservedSoname) {
			Log.LogCodedError ("XA4330", Properties.Resources.XA4330, path,
				MonoAndroidHelper.MakeZipArchivePath (ArchiveLibPath, abi, fileName));
			return;
		}
		var item = (filePath: path, archivePath: MakeArchiveLibPath (abi, fileName));
		if (files.Any (x => x.archivePath == item.archivePath)) {
			Log.LogCodedWarning ("XA4301", path, 0, Properties.Resources.XA4301, item.archivePath);
			return;
		}

		string? strippedPath = StripNativeLibIfNecessary (item.filePath, abi, fileName);
		if (strippedPath != null) {
			item.filePath = strippedPath;
			files.Add (item);
		}
	}

	static string MakeArchiveLibPath (string abi, string fileName) => MonoAndroidHelper.MakeZipArchivePath (ArchiveLibPath, abi, fileName);

	sealed class LibInfo
	{
		public string Path;
		public string? Link;
		public string? Abi;
		public string? ArchiveFileName;
		public ITaskItem Item;

		public LibInfo (string path, string? link, string? abi, string? archiveFileName, ITaskItem item)
		{
			Path = path;
			Link = link;
			Abi = abi;
			ArchiveFileName = archiveFileName;
			Item = item;
		}
	}
}
