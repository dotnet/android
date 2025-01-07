#nullable enable
// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

using ArchiveFileList = System.Collections.Generic.List<(string filePath, string archivePath)>;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Collects native libraries to be added to the final archive.
/// </summary>
public class CollectNativeFilesForArchive : AndroidTask
{
	const string ArchiveLibPath = "lib";

	public override string TaskPrefix => "CNF";

	public string AndroidNdkDirectory { get; set; } = "";

	[Required]
	public string ApkOutputPath { get; set; } = "";

	public ITaskItem[] AdditionalNativeLibraryReferences { get; set; } = [];

	[Required]
	public ITaskItem[] FrameworkNativeLibraries { get; set; } = [];

	[Required]
	public ITaskItem[] NativeLibraries { get; set; } = [];

	[Required]
	public ITaskItem[] ApplicationSharedLibraries { get; set; } = [];

	public ITaskItem[] BundleNativeLibraries { get; set; } = [];

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	public bool IncludeWrapSh { get; set; }

	public string CheckedBuild { get; set; } = "";

	public int ZipAlignmentPages { get; set; } = AndroidZipAlign.DefaultZipAlignment64Bit;

	[Required]
	public string AndroidBinUtilsDirectory { get; set; } = "";

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	[Output]
	public ITaskItem[] OutputFiles { get; set; } = [];

	[Output]
	public ITaskItem[] FilesToAddToArchive { get; set; } = [];

	[Output]
	public ITaskItem [] DSODirectoriesToDelete { get; set; } = [];

	public override bool RunTask ()
	{
		var apk = new PackageFileListBuilder ();
		var dsoWrapperConfig = DSOWrapperGenerator.GetConfig (Log, AndroidBinUtilsDirectory, IntermediateOutputPath);

		var outputFiles = new List<string> {
			ApkOutputPath
		};

		var files = new ArchiveFileList ();

		AddRuntimeLibraries (apk, SupportedAbis);
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
		DSODirectoriesToDelete = DSOWrapperGenerator.GetDirectoriesToCleanUp (dsoWrapperConfig).Select (d => new TaskItem (d)).ToArray ();

		return !Log.HasLoggedErrors;
	}

	void AddNativeLibraryToArchive (PackageFileListBuilder apk, string abi, string filesystemPath, string inArchiveFileName, ITaskItem taskItem)
	{
		string archivePath = MakeArchiveLibPath (abi, inArchiveFileName);
		Log.LogDebugMessage ($"Adding native library: {filesystemPath} (APK path: {archivePath})");
		ELFHelper.AssertValidLibraryAlignment (Log, ZipAlignmentPages, filesystemPath, taskItem);
		apk.AddItem (filesystemPath, archivePath);
	}

	void AddRuntimeLibraries (PackageFileListBuilder apk, string [] supportedAbis)
	{
		foreach (var abi in supportedAbis) {
			foreach (ITaskItem item in ApplicationSharedLibraries) {
				if (string.Compare (abi, item.GetMetadata ("abi"), StringComparison.Ordinal) != 0)
					continue;
				AddNativeLibraryToArchive (apk, abi, item.ItemSpec, Path.GetFileName (item.ItemSpec), item);
			}
		}
	}

	bool IsWrapperScript (string path, string? link)
	{
		if (Path.DirectorySeparatorChar == '/') {
			path = path.Replace ('\\', '/');
		}

		if (string.Compare (Path.GetFileName (path), "wrap.sh", StringComparison.Ordinal) == 0) {
			return true;
		}

		if (string.IsNullOrEmpty (link)) {
			return false;
		}

		if (Path.DirectorySeparatorChar == '/') {
			link = link!.Replace ('\\', '/');
		}

		return string.Compare (Path.GetFileName (link), "wrap.sh", StringComparison.Ordinal) == 0;
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
		if (!string.IsNullOrEmpty (archiveFileName))
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

		if (string.IsNullOrWhiteSpace (CheckedBuild))
			return;

		string mode = CheckedBuild;
		string sanitizerName;
		if (string.Compare ("asan", mode, StringComparison.Ordinal) == 0) {
			sanitizerName = "asan";
		} else if (string.Compare ("ubsan", mode, StringComparison.Ordinal) == 0) {
			sanitizerName = "ubsan_standalone";
		} else {
			LogSanitizerWarning ($"Unknown checked build mode '{CheckedBuild}'");
			return;
		}

		if (!IncludeWrapSh) {
			LogSanitizerError ("Checked builds require the wrapper script to be packaged. Please set the `$(AndroidIncludeWrapSh)` MSBuild property to `true` in your project.");
			return;
		}

		if (!libs.Any (info => IsWrapperScript (info.Path, info.Link))) {
			LogSanitizerError ($"Checked builds require the wrapper script to be packaged. Please add `wrap.sh` appropriate for the {CheckedBuild} checker to your project.");
			return;
		}

		NdkTools ndk = NdkTools.Create (AndroidNdkDirectory, logErrors: false, log: Log);
		if (Log.HasLoggedErrors) {
			return; // NdkTools.Create will log appropriate error
		}

		string clangDir = ndk.GetClangDeviceLibraryPath ();
		if (string.IsNullOrEmpty (clangDir)) {
			LogSanitizerError ($"Unable to find the clang compiler directory. Is NDK installed?");
			return;
		}

		foreach (string abi in supportedAbis) {
			string clangAbi = MonoAndroidHelper.MapAndroidAbiToClang (abi);
			if (string.IsNullOrEmpty (clangAbi)) {
				LogSanitizerError ($"Unable to map Android ABI {abi} to clang ABI");
				return;
			}

			string sanitizerLib = $"libclang_rt.{sanitizerName}-{clangAbi}-android.so";
			string sanitizerLibPath = Path.Combine (clangDir, sanitizerLib);
			if (!File.Exists (sanitizerLibPath)) {
				LogSanitizerError ($"Unable to find sanitizer runtime for the {CheckedBuild} checker at {sanitizerLibPath}");
				return;
			}

			AddNativeLibrary (files, sanitizerLibPath, abi, sanitizerLib);
		}
	}

	string? GetNativeLibraryAbi (ITaskItem lib)
	{
		// If Abi is explicitly specified, simply return it.
		var lib_abi = AndroidRidAbiHelper.GetNativeLibraryAbi (lib);

		if (string.IsNullOrWhiteSpace (lib_abi)) {
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
		string fileName = string.IsNullOrEmpty (archiveFileName) ? Path.GetFileName (path) : archiveFileName!;
		var item = (filePath: path, archivePath: MakeArchiveLibPath (abi, fileName));
		if (files.Any (x => x.archivePath == item.archivePath)) {
			Log.LogCodedWarning ("XA4301", path, 0, Properties.Resources.XA4301, item.archivePath);
			return;
		}

		ELFHelper.AssertValidLibraryAlignment (Log, ZipAlignmentPages, path, taskItem);
		if (!ELFHelper.IsEmptyAOTLibrary (Log, item.filePath)) {
			files.Add (item);
		} else {
			Log.LogDebugMessage ($"{item.filePath} is an empty (no executable code) AOT assembly, not including it in the archive");
		}
	}

	// This method is used only for internal warnings which will never be shown to the end user, therefore there's
	// no need to use coded warnings. (They are only used when the internal property $(_AndroidCheckedBuild) is set.)
	void LogSanitizerWarning (string message)
	{
		Log.LogWarning (message);
	}

	void LogSanitizerError (string message)
	{
		Log.LogError (message);
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
