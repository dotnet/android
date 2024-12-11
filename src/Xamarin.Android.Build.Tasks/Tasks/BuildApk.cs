// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

using ArchiveFileList = System.Collections.Generic.List<(string filePath, string archivePath)>;
using Xamarin.Tools.Zip;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class BuildApk : AndroidTask
	{
		const string ArchiveLibPath = "lib";

		public override string TaskPrefix => "BLD";

		public string AndroidNdkDirectory { get; set; }

		[Required]
		public string ApkOutputPath { get; set; }

		public ITaskItem[] AdditionalNativeLibraryReferences { get; set; }

		[Required]
		public ITaskItem[] FrameworkNativeLibraries { get; set; }

		[Required]
		public ITaskItem[] NativeLibraries { get; set; }

		[Required]
		public ITaskItem[] ApplicationSharedLibraries { get; set; }

		public ITaskItem[] BundleNativeLibraries { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		public string AndroidSequencePointsMode { get; set; }

		public string UncompressedFileExtensions { get; set; }

		public bool IncludeWrapSh { get; set; }

		public string CheckedBuild { get; set; }

		public string RuntimeConfigBinFilePath { get; set; }

		public int ZipAlignmentPages { get; set; } = AndroidZipAlign.DefaultZipAlignment64Bit;

		[Required]
		public string AndroidBinUtilsDirectory { get; set; }

		[Required]
		public string IntermediateOutputPath { get; set; }

		[Output]
		public ITaskItem[] OutputFiles { get; set; }

		[Output]
		public ITaskItem[] OutputApkFiles { get; set; }

		[Output]
		public ITaskItem [] DSODirectoriesToDelete { get; set; }

		SequencePointsMode sequencePointsMode = SequencePointsMode.None;

		HashSet<string> uncompressedFileExtensions;

		protected virtual CompressionMethod UncompressedMethod => CompressionMethod.Store;

		protected virtual void FixupArchive (ZipArchiveFileListBuilder zip) { }

		void ExecuteWithAbi (DSOWrapperGenerator.Config dsoWrapperConfig, string [] supportedAbis, string apkOutputPath)
		{
			ArchiveFileList files = new ArchiveFileList ();

			using (var apk = new ZipArchiveFileListBuilder (apkOutputPath, File.Exists (apkOutputPath) ? FileMode.Open : FileMode.Create)) {

				AddRuntimeConfigBlob (dsoWrapperConfig, apk);
				AddRuntimeLibraries (apk, supportedAbis);
				apk.Flush();
				AddNativeLibraries (files, supportedAbis);
				AddAdditionalNativeLibraries (files, supportedAbis);

				foreach (var file in files) {
					var item = Path.Combine (file.archivePath.Replace (Path.DirectorySeparatorChar, '/'));
					CompressionMethod compressionMethod = GetCompressionMethod (file.filePath);
					if (apk.SkipExistingFile (file.filePath, item, compressionMethod)) {
						Log.LogDebugMessage ($"Skipping {file.filePath} as the archive file is up to date.");
						continue;
					}
					Log.LogDebugMessage ("\tAdding {0}", file.filePath);
					apk.AddFileAndFlush (file.filePath, item, compressionMethod: compressionMethod);
				}

				FixupArchive (apk);

				OutputApkFiles = apk.ApkFiles.ToArray ();
			}
		}

		public override bool RunTask ()
		{
			Aot.TryGetSequencePointsMode (AndroidSequencePointsMode, out sequencePointsMode);

			var outputFiles = new List<string> ();
			uncompressedFileExtensions = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (string? e in UncompressedFileExtensions?.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string> ()) {
				string? ext = e?.Trim ();
				if (String.IsNullOrEmpty (ext)) {
					continue;
				}

				if (ext[0] != '.') {
					ext = $".{ext}";
				}
				uncompressedFileExtensions.Add (ext);
			}

			DSOWrapperGenerator.Config dsoWrapperConfig = DSOWrapperGenerator.GetConfig (Log, AndroidBinUtilsDirectory, IntermediateOutputPath);
			ExecuteWithAbi (dsoWrapperConfig, SupportedAbis, ApkOutputPath);
			outputFiles.Add (ApkOutputPath);

			OutputFiles = outputFiles.Select (a => new TaskItem (a)).ToArray ();

			Log.LogDebugTaskItems ("  [Output] OutputFiles :", OutputFiles);
			DSODirectoriesToDelete = DSOWrapperGenerator.GetDirectoriesToCleanUp (dsoWrapperConfig).Select (d => new TaskItem (d)).ToArray ();

			return !Log.HasLoggedErrors;
		}

		void AddRuntimeConfigBlob (DSOWrapperGenerator.Config dsoWrapperConfig, ZipArchiveFileListBuilder apk)
		{
			// We will place rc.bin in the `lib` directory next to the blob, to make startup slightly faster, as we will find the config file right after we encounter
			// our assembly store.  Not only that, but also we'll be able to skip scanning the `base.apk` archive when split configs are enabled (which they are in 99%
			// of cases these days, since AAB enforces that split).  `base.apk` contains only ABI-agnostic file, while one of the split config files contains only
			// ABI-specific data+code.
			if (!String.IsNullOrEmpty (RuntimeConfigBinFilePath) && File.Exists (RuntimeConfigBinFilePath)) {
				foreach (string abi in SupportedAbis) {
					// Prefix it with `a` because bundletool sorts entries alphabetically, and this will place it right next to `assemblies.*.blob.so`, which is what we
					// like since we can finish scanning the zip central directory earlier at startup.
					string inArchivePath = MakeArchiveLibPath (abi, "libarc.bin.so");
					string wrappedSourcePath = DSOWrapperGenerator.WrapIt (Log, dsoWrapperConfig, MonoAndroidHelper.AbiToTargetArch (abi), RuntimeConfigBinFilePath, Path.GetFileName (inArchivePath));
					AddFileToArchiveIfNewer (apk, wrappedSourcePath, inArchivePath, compressionMethod: GetCompressionMethod (inArchivePath));
				}
			}
		}

		bool AddFileToArchiveIfNewer (ZipArchiveFileListBuilder apk, string file, string inArchivePath, CompressionMethod compressionMethod = CompressionMethod.Default)
		{
			if (apk.SkipExistingFile (file, inArchivePath, compressionMethod)) {
				Log.LogDebugMessage ($"Skipping {file} as the archive file is up to date.");
				return false;
			}
			Log.LogDebugMessage ($"Adding {file} as the archive file is out of date.");
			apk.AddFileAndFlush (file, inArchivePath, compressionMethod: compressionMethod);
			return true;
		}

		sealed class LibInfo
		{
			public string Path;
			public string Link;
			public string Abi;
			public string ArchiveFileName;
			public ITaskItem Item;
		}

		CompressionMethod GetCompressionMethod (string fileName)
		{
			return uncompressedFileExtensions.Contains (Path.GetExtension (fileName)) ? UncompressedMethod : CompressionMethod.Default;
		}

		void AddNativeLibraryToArchive (ZipArchiveFileListBuilder apk, string abi, string filesystemPath, string inArchiveFileName, ITaskItem taskItem)
		{
			string archivePath = MakeArchiveLibPath (abi, inArchiveFileName);
			CompressionMethod compressionMethod = GetCompressionMethod (archivePath);
			if (apk.SkipExistingFile (filesystemPath, archivePath, compressionMethod)) {
				Log.LogDebugMessage ($"Skipping {filesystemPath} (APK path: {archivePath}) as it is up to date.");
				return;
			}
			Log.LogDebugMessage ($"Adding native library: {filesystemPath} (APK path: {archivePath})");
			ELFHelper.AssertValidLibraryAlignment (Log, ZipAlignmentPages, filesystemPath, taskItem);
			apk.AddFileAndFlush (filesystemPath, archivePath, compressionMethod);
		}

		void AddRuntimeLibraries (ZipArchiveFileListBuilder apk, string [] supportedAbis)
		{
			foreach (var abi in supportedAbis) {
				foreach (ITaskItem item in ApplicationSharedLibraries) {
					if (String.Compare (abi, item.GetMetadata ("abi"), StringComparison.Ordinal) != 0)
						continue;
					AddNativeLibraryToArchive (apk, abi, item.ItemSpec, Path.GetFileName (item.ItemSpec), item);
				}
			}
		}

		bool IsWrapperScript (string path, string link)
		{
			if (Path.DirectorySeparatorChar == '/') {
				path = path.Replace ('\\', '/');
			}

			if (String.Compare (Path.GetFileName (path), "wrap.sh", StringComparison.Ordinal) == 0) {
				return true;
			}

			if (String.IsNullOrEmpty (link)) {
				return false;
			}

			if (Path.DirectorySeparatorChar == '/') {
				link = link.Replace ('\\', '/');
			}

			return String.Compare (Path.GetFileName (link), "wrap.sh", StringComparison.Ordinal) == 0;
		}

		bool IncludeNativeLibrary (ITaskItem item)
		{
			if (IncludeWrapSh)
				return true;

			return !IsWrapperScript (item.ItemSpec, item.GetMetadata ("Link"));
		}

		string GetArchiveFileName (ITaskItem item)
		{
			string archiveFileName = item.GetMetadata ("ArchiveFileName");
			if (!String.IsNullOrEmpty (archiveFileName))
				return archiveFileName;

			if (!IsWrapperScript (item.ItemSpec, item.GetMetadata ("Link"))) {
				return null;
			}

			return "wrap.sh";
		}

		private void AddNativeLibraries (ArchiveFileList files, string [] supportedAbis)
		{
			var frameworkLibs = FrameworkNativeLibraries.Select (v => new LibInfo {
				Path = v.ItemSpec,
				Link = v.GetMetadata ("Link"),
				Abi = GetNativeLibraryAbi (v),
				ArchiveFileName = GetArchiveFileName (v),
				Item = v,
			});

			AddNativeLibraries (files, supportedAbis, frameworkLibs);

			var libs = NativeLibraries.Concat (BundleNativeLibraries ?? Enumerable.Empty<ITaskItem> ())
				.Where (v => IncludeNativeLibrary (v))
				.Select (v => new LibInfo {
						Path = v.ItemSpec,
						Link = v.GetMetadata ("Link"),
						Abi = GetNativeLibraryAbi (v),
						ArchiveFileName = GetArchiveFileName (v),
						Item = v,
					}
				);

			AddNativeLibraries (files, supportedAbis, libs);

			if (String.IsNullOrWhiteSpace (CheckedBuild))
				return;

			string mode = CheckedBuild;
			string sanitizerName;
			if (String.Compare ("asan", mode, StringComparison.Ordinal) == 0) {
				sanitizerName = "asan";
			} else if (String.Compare ("ubsan", mode, StringComparison.Ordinal) == 0) {
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
			if (String.IsNullOrEmpty (clangDir)) {
				LogSanitizerError ($"Unable to find the clang compiler directory. Is NDK installed?");
				return;
			}

			foreach (string abi in supportedAbis) {
				string clangAbi = MonoAndroidHelper.MapAndroidAbiToClang (abi);
				if (String.IsNullOrEmpty (clangAbi)) {
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

		string GetNativeLibraryAbi (ITaskItem lib)
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
				AddNativeLibrary (files, info.Path, info.Abi, info.ArchiveFileName, info.Item);
			}
		}

		private void AddAdditionalNativeLibraries (ArchiveFileList files, string [] supportedAbis)
		{
			if (AdditionalNativeLibraryReferences == null || !AdditionalNativeLibraryReferences.Any ())
				return;

			var libs = AdditionalNativeLibraryReferences
				.Select (l => new LibInfo {
					Path = l.ItemSpec,
					Abi = AndroidRidAbiHelper.GetNativeLibraryAbi (l),
					ArchiveFileName = l.GetMetadata ("ArchiveFileName"),
					Item = l,
				});

			AddNativeLibraries (files, supportedAbis, libs);
		}

		void AddNativeLibrary (ArchiveFileList files, string path, string abi, string archiveFileName, ITaskItem? taskItem = null)
		{
			string fileName = string.IsNullOrEmpty (archiveFileName) ? Path.GetFileName (path) : archiveFileName;
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
		// no need to use coded warnings.
		void LogSanitizerWarning (string message)
		{
			Log.LogWarning (message);
		}

		void LogSanitizerError (string message)
		{
			Log.LogError (message);
		}

		static string MakeArchiveLibPath (string abi, string fileName) => MonoAndroidHelper.MakeZipArchivePath (ArchiveLibPath, abi, fileName);
	}
}
