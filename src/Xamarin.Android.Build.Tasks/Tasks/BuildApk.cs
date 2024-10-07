// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

using Java.Interop.Tools.Cecil;

using ArchiveFileList = System.Collections.Generic.List<(string filePath, string archivePath)>;
using Mono.Cecil;
using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class BuildApk : AndroidTask
	{
		const string ArchiveAssembliesPath = "lib";
		const string ArchiveLibPath = "lib";

		public override string TaskPrefix => "BLD";

		public string AndroidNdkDirectory { get; set; }

		[Required]
		public string ApkInputPath { get; set; }

		[Required]
		public string ApkOutputPath { get; set; }

		[Required]
		public string AppSharedLibrariesDir { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedFrameworkAssemblies { get; set; }

		public ITaskItem[] AdditionalNativeLibraryReferences { get; set; }

		public ITaskItem[] EmbeddedNativeLibraryAssemblies { get; set; }

		[Required]
		public ITaskItem[] FrameworkNativeLibraries { get; set; }

		[Required]
		public ITaskItem[] NativeLibraries { get; set; }

		[Required]
		public ITaskItem[] ApplicationSharedLibraries { get; set; }

		public ITaskItem[] BundleNativeLibraries { get; set; }

		public ITaskItem[] TypeMappings { get; set; }

		[Required]
		public ITaskItem [] DalvikClasses { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		public bool CreatePackagePerAbi { get; set; }

		public bool EmbedAssemblies { get; set; }

		public bool BundleAssemblies { get; set; }

		public ITaskItem[] JavaSourceFiles { get; set; }

		public ITaskItem[] JavaLibraries { get; set; }

		public string[] DoNotPackageJavaLibraries { get; set; }

		public string [] ExcludeFiles { get; set; }

		public string [] IncludeFiles { get; set; }

		public string Debug { get; set; }

		public string AndroidSequencePointsMode { get; set; }

		public string TlsProvider { get; set; }
		public string UncompressedFileExtensions { get; set; }

		// Make it required after https://github.com/xamarin/monodroid/pull/1094 is merged
		//[Required]
		public bool EnableCompression { get; set; }

		public bool IncludeWrapSh { get; set; }

		public string CheckedBuild { get; set; }

		public string RuntimeConfigBinFilePath { get; set; }

		public bool UseAssemblyStore { get; set; }

		public string ZipFlushFilesLimit { get; set; }

		public string ZipFlushSizeLimit { get; set; }

		public int ZipAlignmentPages { get; set; } = AndroidZipAlign.DefaultZipAlignment64Bit;

		[Required]
		public string ProjectFullPath { get; set; }

		[Output]
		public ITaskItem[] OutputFiles { get; set; }

		bool _Debug {
			get {
				return string.Equals (Debug, "true", StringComparison.OrdinalIgnoreCase);
			}
		}

		SequencePointsMode sequencePointsMode = SequencePointsMode.None;

		public ITaskItem[] LibraryProjectJars { get; set; }
		HashSet<string> uncompressedFileExtensions;

		// Do not use trailing / in the path
		protected virtual string RootPath => "";

		protected virtual string DalvikPath => "";

		protected virtual CompressionMethod UncompressedMethod => CompressionMethod.Store;

		protected virtual void FixupArchive (ZipArchiveEx zip) { }

		List<string> existingEntries = new List<string> ();

		List<Regex> excludePatterns = new List<Regex> ();

		List<Regex> includePatterns = new List<Regex> ();

		void ExecuteWithAbi (string [] supportedAbis, string apkInputPath, string apkOutputPath, bool debug, bool compress, IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> compressedAssembliesInfo, string assemblyStoreApkName)
		{
			ArchiveFileList files = new ArchiveFileList ();
			bool refresh = true;
			if (apkInputPath != null && File.Exists (apkInputPath) && !File.Exists (apkOutputPath)) {
				Log.LogDebugMessage ($"Copying {apkInputPath} to {apkOutputPath}");
				File.Copy (apkInputPath, apkOutputPath, overwrite: true);
				refresh = false;
			}
			using (var apk = new ZipArchiveEx (apkOutputPath, File.Exists (apkOutputPath) ? FileMode.Open : FileMode.Create )) {
				if (int.TryParse (ZipFlushFilesLimit, out int flushFilesLimit)) {
					apk.ZipFlushFilesLimit = flushFilesLimit;
				}
				if (int.TryParse (ZipFlushSizeLimit, out int flushSizeLimit)) {
					apk.ZipFlushSizeLimit = flushSizeLimit;
				}
				if (refresh) {
					for (long i = 0; i < apk.Archive.EntryCount; i++) {
						ZipEntry e = apk.Archive.ReadEntry ((ulong) i);
						Log.LogDebugMessage ($"Registering item {e.FullName}");
						existingEntries.Add (e.FullName);
					}
				}
				if (apkInputPath != null && File.Exists (apkInputPath) && refresh) {
					var lastWriteOutput = File.Exists (apkOutputPath) ? File.GetLastWriteTimeUtc (apkOutputPath) : DateTime.MinValue;
					var lastWriteInput = File.GetLastWriteTimeUtc (apkInputPath);
					using (var packaged = new ZipArchiveEx (apkInputPath, FileMode.Open)) {
						foreach (var entry in packaged.Archive) {
							// NOTE: aapt2 is creating zip entries on Windows such as `assets\subfolder/asset2.txt`
							var entryName = entry.FullName;
							if (entryName.Contains ("\\")) {
								entryName = entryName.Replace ('\\', '/');
								Log.LogDebugMessage ($"Fixing up malformed entry `{entry.FullName}` -> `{entryName}`");
							}
							Log.LogDebugMessage ($"Deregistering item {entryName}");
							existingEntries.Remove (entryName);
							if (lastWriteInput <= lastWriteOutput) {
								Log.LogDebugMessage ($"Skipping to next item. {lastWriteInput} <= {lastWriteOutput}.");
								continue;
							}
							if (apk.Archive.ContainsEntry (entryName)) {
								ZipEntry e = apk.Archive.ReadEntry (entryName);
								// check the CRC values as the ModifiedDate is always 01/01/1980 in the aapt generated file.
								if (entry.CRC == e.CRC && entry.CompressedSize == e.CompressedSize) {
									Log.LogDebugMessage ($"Skipping {entryName} from {apkInputPath} as its up to date.");
									continue;
								}
							}
							var ms = new MemoryStream ();
							entry.Extract (ms);
							Log.LogDebugMessage ($"Refreshing {entryName} from {apkInputPath}");
							apk.Archive.AddStream (ms, entryName, compressionMethod: entry.CompressionMethod);
						}
					}
				}
				apk.FixupWindowsPathSeparators ((a, b) => Log.LogDebugMessage ($"Fixing up malformed entry `{a}` -> `{b}`"));

				// Add classes.dx
				CompressionMethod dexCompressionMethod = GetCompressionMethod (".dex");
				foreach (var dex in DalvikClasses) {
					string apkName = dex.GetMetadata ("ApkName");
					string dexPath = string.IsNullOrWhiteSpace (apkName) ? Path.GetFileName (dex.ItemSpec) : apkName;
					AddFileToArchiveIfNewer (apk, dex.ItemSpec, DalvikPath + dexPath, compressionMethod: dexCompressionMethod);
					apk.Flush ();
				}

				if (EmbedAssemblies) {
					AddAssemblies (apk, debug, compress, compressedAssembliesInfo, assemblyStoreApkName);
					apk.Flush ();
				}

				AddRuntimeConfigBlob (apk);
				AddRuntimeLibraries (apk, supportedAbis);
				apk.Flush();
				AddNativeLibraries (files, supportedAbis);
				AddAdditionalNativeLibraries (files, supportedAbis);

				if (TypeMappings != null) {
					foreach (ITaskItem typemap in TypeMappings) {
						AddFileToArchiveIfNewer (apk, typemap.ItemSpec, RootPath + Path.GetFileName(typemap.ItemSpec), compressionMethod: UncompressedMethod);
					}
				}

				foreach (var file in files) {
					var item = Path.Combine (file.archivePath.Replace (Path.DirectorySeparatorChar, '/'));
					existingEntries.Remove (item);
					CompressionMethod compressionMethod = GetCompressionMethod (file.filePath);
					if (apk.SkipExistingFile (file.filePath, item, compressionMethod)) {
						Log.LogDebugMessage ($"Skipping {file.filePath} as the archive file is up to date.");
						continue;
					}
					Log.LogDebugMessage ("\tAdding {0}", file.filePath);
					apk.AddFileAndFlush (file.filePath, item, compressionMethod: compressionMethod);
				}

				var jarFiles = (JavaSourceFiles != null) ? JavaSourceFiles.Where (f => f.ItemSpec.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) : null;
				if (jarFiles != null && JavaLibraries != null)
					jarFiles = jarFiles.Concat (JavaLibraries);
				else if (JavaLibraries != null)
					jarFiles = JavaLibraries;

				var libraryProjectJars  = MonoAndroidHelper.ExpandFiles (LibraryProjectJars)
					.Where (jar => !MonoAndroidHelper.IsEmbeddedReferenceJar (jar));

				var jarFilePaths = libraryProjectJars.Concat (jarFiles != null ? jarFiles.Select (j => j.ItemSpec) : Enumerable.Empty<string> ());
				jarFilePaths = MonoAndroidHelper.DistinctFilesByContent (jarFilePaths);

				foreach (var jarFile in jarFilePaths) {
					using (var stream = File.OpenRead (jarFile))
					using (var jar = ZipArchive.Open (stream)) {
						foreach (var jarItem in jar) {
							if (jarItem.IsDirectory)
								continue;
							var name = jarItem.FullName;
							if (!PackagingUtils.CheckEntryForPackaging (name)) {
								continue;
							}
							var path = RootPath + name;
							existingEntries.Remove (path);
							if (apk.SkipExistingEntry (jarItem, path)) {
								Log.LogDebugMessage ($"Skipping {path} as the archive file is up to date.");
								continue;
							}
							// check for ignored items
							bool exclude = false;
							bool forceInclude = false;
							foreach (var include in includePatterns) {
								if (include.IsMatch (path)) {
									forceInclude = true;
									break;
								}
							}
							if (!forceInclude) {
								foreach (var pattern in excludePatterns) {
									if(pattern.IsMatch (path)) {
										Log.LogDebugMessage ($"Ignoring jar entry '{name}' from '{Path.GetFileName (jarFile)}'. Filename matched the exclude pattern '{pattern}'.");
										exclude = true;
										break;
									}
								}
							}
							if (exclude)
								continue;
							if (string.Compare (Path.GetFileName (name), "AndroidManifest.xml", StringComparison.OrdinalIgnoreCase) == 0) {
								Log.LogDebugMessage ("Ignoring jar entry {0} from {1}: the same file already exists in the apk", name, Path.GetFileName (jarFile));
								continue;
							}
							if (apk.Archive.Any (e => e.FullName == path)) {
								Log.LogDebugMessage ("Failed to add jar entry {0} from {1}: the same file already exists in the apk", name, Path.GetFileName (jarFile));
								continue;
							}
							byte [] data;
							using (var d = new MemoryStream ()) {
								jarItem.Extract (d);
								data = d.ToArray ();
							}
							Log.LogDebugMessage ($"Adding {path} from {jarFile} as the archive file is out of date.");
							apk.AddEntryAndFlush (data, path);
						}
					}
				}
				// Clean up Removed files.
				foreach (var entry in existingEntries) {
					// never remove an AndroidManifest. It may be renamed when using aab.
					if (string.Compare (Path.GetFileName (entry), "AndroidManifest.xml", StringComparison.OrdinalIgnoreCase) == 0)
						continue;
					Log.LogDebugMessage ($"Removing {entry} as it is not longer required.");
					apk.Archive.DeleteEntry (entry);
				}
				apk.Flush ();
				FixupArchive (apk);
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

			existingEntries.Clear ();

			foreach (var pattern in ExcludeFiles ?? Array.Empty<string> ()) {
				excludePatterns.Add (FileGlobToRegEx (pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
			}
			foreach (var pattern in IncludeFiles ?? Array.Empty<string> ()) {
				includePatterns.Add (FileGlobToRegEx (pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
			}

			bool debug = _Debug;
			bool compress = !debug && EnableCompression;
			IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> compressedAssembliesInfo = null;

			if (compress) {
				string key = CompressedAssemblyInfo.GetKey (ProjectFullPath);
				Log.LogDebugMessage ($"Retrieving assembly compression info with key '{key}'");
				compressedAssembliesInfo = BuildEngine4.UnregisterTaskObjectAssemblyLocal<IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>> (key, RegisteredTaskObjectLifetime.Build);
				if (compressedAssembliesInfo == null)
					throw new InvalidOperationException ($"Assembly compression info not found for key '{key}'. Compression will not be performed.");
			}

			ExecuteWithAbi (SupportedAbis, ApkInputPath, ApkOutputPath, debug, compress, compressedAssembliesInfo, assemblyStoreApkName: null);
			outputFiles.Add (ApkOutputPath);
			if (CreatePackagePerAbi && SupportedAbis.Length > 1) {
				foreach (var abi in SupportedAbis) {
					existingEntries.Clear ();
					var path = Path.GetDirectoryName (ApkOutputPath);
					var apk = Path.GetFileNameWithoutExtension (ApkOutputPath);
					ExecuteWithAbi (new [] { abi }, String.Format ("{0}-{1}", ApkInputPath, abi),
						Path.Combine (path, String.Format ("{0}-{1}.apk", apk, abi)),
					        debug, compress, compressedAssembliesInfo, assemblyStoreApkName: abi);
					outputFiles.Add (Path.Combine (path, String.Format ("{0}-{1}.apk", apk, abi)));
				}
			}

			OutputFiles = outputFiles.Select (a => new TaskItem (a)).ToArray ();

			Log.LogDebugTaskItems ("  [Output] OutputFiles :", OutputFiles);
			DSOWrapperGenerator.CleanUp (this);

			return !Log.HasLoggedErrors;
		}

		internal DSOWrapperGenerator.Config EnsureConfig ()
		{
			var config = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<DSOWrapperGenerator.Config> (ProjectSpecificTaskObjectKey (DSOWrapperGenerator.RegisteredConfigKey), RegisteredTaskObjectLifetime.Build);
			if (config is null) {
				throw new InvalidOperationException ("Internal error: no registered config found");
			}
			return config;
		}

		static Regex FileGlobToRegEx (string fileGlob, RegexOptions options)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (char c in fileGlob) {
				switch (c) {
					case '*': sb.Append (".*");
						break;
					case '?': sb.Append (".");
						break;
					case '.': sb.Append (@"\.");
						break;
					default: sb.Append (c);
						break;
				}
			}
			return new Regex (sb.ToString (), options);
		}

		void AddRuntimeConfigBlob (ZipArchiveEx apk)
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
					string wrappedSourcePath = DSOWrapperGenerator.WrapIt (MonoAndroidHelper.AbiToTargetArch (abi), RuntimeConfigBinFilePath, Path.GetFileName (inArchivePath), this);
					AddFileToArchiveIfNewer (apk, wrappedSourcePath, inArchivePath, compressionMethod: GetCompressionMethod (inArchivePath));
				}
			}
		}

		void AddAssemblies (ZipArchiveEx apk, bool debug, bool compress, IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> compressedAssembliesInfo, string assemblyStoreApkName)
		{
			string sourcePath;
			AssemblyCompression.AssemblyData compressedAssembly = null;
			string compressedOutputDir = Path.GetFullPath (Path.Combine (Path.GetDirectoryName (ApkOutputPath), "..", "lz4"));
			AssemblyStoreGenerator? storeGenerator;

			if (UseAssemblyStore) {
				storeGenerator = new AssemblyStoreGenerator (Log);
			} else {
				storeGenerator = null;
			}

			AssemblyStoreAssemblyInfo? storeAssemblyInfo = null;

			// Add user assemblies
			AddAssembliesFromCollection (ResolvedUserAssemblies);

			// Add framework assemblies
			AddAssembliesFromCollection (ResolvedFrameworkAssemblies);

			if (!UseAssemblyStore) {
				return;
			}

			Dictionary<AndroidTargetArch, string> assemblyStorePaths = storeGenerator.Generate (AppSharedLibrariesDir);

			if (assemblyStorePaths.Count == 0) {
				throw new InvalidOperationException ("Assembly store generator did not generate any stores");
			}

			if (assemblyStorePaths.Count != SupportedAbis.Length) {
				throw new InvalidOperationException ("Internal error: assembly store did not generate store for each supported ABI");
			}

			string inArchivePath;
			foreach (var kvp in assemblyStorePaths) {
				string abi = MonoAndroidHelper.ArchToAbi (kvp.Key);
				inArchivePath = MakeArchiveLibPath (abi, "lib" + Path.GetFileName (kvp.Value));
				string wrappedSourcePath = DSOWrapperGenerator.WrapIt (kvp.Key, kvp.Value, Path.GetFileName (inArchivePath), this);
				AddFileToArchiveIfNewer (apk, wrappedSourcePath, inArchivePath, GetCompressionMethod (inArchivePath));
			}

			void AddAssembliesFromCollection (ITaskItem[] assemblies)
			{
				Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> perArchAssemblies = MonoAndroidHelper.GetPerArchAssemblies (
					assemblies,
					SupportedAbis,
					validate: true,
					shouldSkip: (ITaskItem asm) => {
						if (bool.TryParse (asm.GetMetadata ("AndroidSkipAddToPackage"), out bool value) && value) {
							Log.LogDebugMessage ($"Skipping {asm.ItemSpec} due to 'AndroidSkipAddToPackage' == 'true' ");
							return true;
						}

						return false;
					}
				);

				foreach (var kvp in perArchAssemblies) {
					Log.LogDebugMessage ($"Adding assemblies for architecture '{kvp.Key}'");
					DoAddAssembliesFromArchCollection (kvp.Key, kvp.Value);
				}
			}

			void DoAddAssembliesFromArchCollection (AndroidTargetArch arch, Dictionary<string, ITaskItem> assemblies)
			{
				// In the "all assemblies are per-RID" world, assemblies, pdb and config are disguised as shared libraries (that is,
				// their names end with the .so extension) so that Android allows us to put them in the `lib/{ARCH}` directory.
				// For this reason, they have to be treated just like other .so files, as far as compression rules are concerned.
				// Thus, we no longer just store them in the apk but we call the `GetCompressionMethod` method to find out whether
				// or not we're supposed to compress .so files.
				foreach (ITaskItem assembly in assemblies.Values) {
					if (MonoAndroidHelper.IsReferenceAssembly (assembly.ItemSpec, Log)) {
						Log.LogCodedWarning ("XA0107", assembly.ItemSpec, 0, Properties.Resources.XA0107, assembly.ItemSpec);
					}

					sourcePath = CompressAssembly (assembly);

					// Add assembly
					(string assemblyPath, string assemblyDirectory) = GetInArchiveAssemblyPath (assembly);
					if (UseAssemblyStore) {
						storeAssemblyInfo = new AssemblyStoreAssemblyInfo (sourcePath, assembly);
					} else {
						string wrappedSourcePath = DSOWrapperGenerator.WrapIt (arch, sourcePath, Path.GetFileName (assemblyPath), this);
						AddFileToArchiveIfNewer (apk, wrappedSourcePath, assemblyPath, compressionMethod: GetCompressionMethod (assemblyPath));
					}

					// Try to add config if exists
					var config = Path.ChangeExtension (assembly.ItemSpec, "dll.config");
					if (UseAssemblyStore) {
						if (File.Exists (config)) {
							storeAssemblyInfo.ConfigFile = new FileInfo (config);
						}
					} else {
						AddAssemblyConfigEntry (apk, arch, assemblyDirectory, config);
					}

					// Try to add symbols if Debug
					if (debug) {
						var symbols = Path.ChangeExtension (assembly.ItemSpec, "pdb");
						string? symbolsPath = null;

						if (File.Exists (symbols)) {
							symbolsPath = symbols;
						}

						if (!String.IsNullOrEmpty (symbolsPath)) {
							if (UseAssemblyStore) {
								storeAssemblyInfo.SymbolsFile = new FileInfo (symbolsPath);
							} else {
								string archiveSymbolsPath = assemblyDirectory + MonoAndroidHelper.MakeDiscreteAssembliesEntryName (Path.GetFileName (symbols));
								string wrappedSymbolsPath = DSOWrapperGenerator.WrapIt (arch, symbolsPath, Path.GetFileName (archiveSymbolsPath), this);
								AddFileToArchiveIfNewer (
									apk,
									wrappedSymbolsPath,
									archiveSymbolsPath,
									compressionMethod: GetCompressionMethod (archiveSymbolsPath)
								);
							}
						}
					}

					if (UseAssemblyStore) {
						storeGenerator.Add (storeAssemblyInfo);
					}
				}
			}

			void EnsureCompressedAssemblyData (string sourcePath, uint descriptorIndex)
			{
				if (compressedAssembly == null)
					compressedAssembly = new AssemblyCompression.AssemblyData (sourcePath, descriptorIndex);
				else
					compressedAssembly.SetData (sourcePath, descriptorIndex);
			}

			string CompressAssembly (ITaskItem assembly)
			{
				if (!compress) {
					return assembly.ItemSpec;
				}

				if (bool.TryParse (assembly.GetMetadata ("AndroidSkipCompression"), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping compression of {assembly.ItemSpec} due to 'AndroidSkipCompression' == 'true' ");
					return assembly.ItemSpec;
				}

				string key = CompressedAssemblyInfo.GetDictionaryKey (assembly);
				AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (assembly);
				if (!compressedAssembliesInfo.TryGetValue (arch, out Dictionary<string, CompressedAssemblyInfo> assembliesInfo)) {
					throw new InvalidOperationException ($"Internal error: compression assembly info for architecture {arch} not available");
				}

				if (!assembliesInfo.TryGetValue (key, out CompressedAssemblyInfo info) || info == null) {
					Log.LogDebugMessage ($"Assembly missing from {nameof (CompressedAssemblyInfo)}: {key}");
					return assembly.ItemSpec;
				}

				EnsureCompressedAssemblyData (assembly.ItemSpec, info.DescriptorIndex);
				string assemblyOutputDir;
				string subDirectory = assembly.GetMetadata ("DestinationSubDirectory");
				string abi = MonoAndroidHelper.GetAssemblyAbi (assembly);
				if (!String.IsNullOrEmpty (subDirectory)) {
					assemblyOutputDir = Path.Combine (compressedOutputDir, abi, subDirectory);
				} else {
					assemblyOutputDir = Path.Combine (compressedOutputDir, abi);
				}
				AssemblyCompression.CompressionResult result = AssemblyCompression.Compress (compressedAssembly, assemblyOutputDir);
				if (result != AssemblyCompression.CompressionResult.Success) {
					switch (result) {
						case AssemblyCompression.CompressionResult.EncodingFailed:
							Log.LogMessage ($"Failed to compress {assembly.ItemSpec}");
							break;

						case AssemblyCompression.CompressionResult.InputTooBig:
							Log.LogMessage ($"Input assembly {assembly.ItemSpec} exceeds maximum input size");
							break;

						default:
							Log.LogMessage ($"Unknown error compressing {assembly.ItemSpec}");
							break;
					}
					return assembly.ItemSpec;
				}
				return compressedAssembly.DestinationPath;
			}
		}

		bool AddFileToArchiveIfNewer (ZipArchiveEx apk, string file, string inArchivePath, CompressionMethod compressionMethod = CompressionMethod.Default)
		{
			existingEntries.Remove (inArchivePath.Replace (Path.DirectorySeparatorChar, '/'));
			if (apk.SkipExistingFile (file, inArchivePath, compressionMethod)) {
				Log.LogDebugMessage ($"Skipping {file} as the archive file is up to date.");
				return false;
			}
			Log.LogDebugMessage ($"Adding {file} as the archive file is out of date.");
			apk.AddFileAndFlush (file, inArchivePath, compressionMethod: compressionMethod);
			return true;
		}

		void AddAssemblyConfigEntry (ZipArchiveEx apk, AndroidTargetArch arch, string assemblyPath, string configFile)
		{
			string inArchivePath = MonoAndroidHelper.MakeDiscreteAssembliesEntryName (assemblyPath + Path.GetFileName (configFile));
			existingEntries.Remove (inArchivePath);

			if (!File.Exists (configFile)) {
				return;
			}

			CompressionMethod compressionMethod = GetCompressionMethod (inArchivePath);
			if (apk.SkipExistingFile (configFile, inArchivePath, compressionMethod)) {
				Log.LogDebugMessage ($"Skipping {configFile} as the archive file is up to date.");
				return;
			}

			Log.LogDebugMessage ($"Adding {configFile} as the archive file is out of date.");
			string wrappedConfigFile = DSOWrapperGenerator.WrapIt (arch, configFile, Path.GetFileName (inArchivePath), this);
			apk.AddFileAndFlush (wrappedConfigFile, inArchivePath, compressionMethod);
		}

		/// <summary>
		/// Returns the in-archive path for an assembly
		/// </summary>
		(string assemblyFilePath, string assemblyDirectoryPath) GetInArchiveAssemblyPath (ITaskItem assembly)
		{
			var parts = new List<string> ();

			// The PrepareSatelliteAssemblies task takes care of properly setting `DestinationSubDirectory`, so we can just use it here.
			string? subDirectory = assembly.GetMetadata ("DestinationSubDirectory")?.Replace ('\\', '/');
			if (string.IsNullOrEmpty (subDirectory)) {
				throw new InvalidOperationException ($"Internal error: assembly '{assembly}' lacks the required `DestinationSubDirectory` metadata");
			}

			string assemblyName = Path.GetFileName (assembly.ItemSpec);
			if (UseAssemblyStore) {
				parts.Add (subDirectory);
				parts.Add (assemblyName);
			} else {
				// For discrete assembly entries we need to treat assemblies specially.
				// All of the assemblies have their names mangled so that the possibility to clash with "real" shared
				// library names is minimized. All of the assembly entries will start with a special character:
				//
				//   `_` - for regular assemblies (e.g. `_Mono.Android.dll.so`)
				//   `-` - for satellite assemblies (e.g. `-es-Mono.Android.dll.so`)
				//
				// Second of all, we need to treat satellite assemblies with even more care.
				// If we encounter one of them, we will return the culture as part of the path transformed
				// so that it forms a `-culture-` assembly file name prefix, not a `culture/` subdirectory.
				// This is necessary because Android doesn't allow subdirectories in `lib/{ABI}/`
				//
				string[] subdirParts = subDirectory.TrimEnd ('/').Split ('/');
				if (subdirParts.Length == 1) {
					// Not a satellite assembly
					parts.Add (subDirectory);
					parts.Add (MonoAndroidHelper.MakeDiscreteAssembliesEntryName (assemblyName));
				} else if (subdirParts.Length == 2) {
					parts.Add (subdirParts[0]);
					parts.Add (MonoAndroidHelper.MakeDiscreteAssembliesEntryName (assemblyName, subdirParts[1]));
				} else {
					throw new InvalidOperationException ($"Internal error: '{assembly}' `DestinationSubDirectory` metadata has too many components ({parts.Count} instead of 1 or 2)");
				}
			}

			string assemblyFilePath = MonoAndroidHelper.MakeZipArchivePath (ArchiveAssembliesPath, parts);
			return (assemblyFilePath, Path.GetDirectoryName (assemblyFilePath) + "/");
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

		void AddNativeLibraryToArchive (ZipArchiveEx apk, string abi, string filesystemPath, string inArchiveFileName, ITaskItem taskItem)
		{
			string archivePath = MakeArchiveLibPath (abi, inArchiveFileName);
			existingEntries.Remove (archivePath);
			CompressionMethod compressionMethod = GetCompressionMethod (archivePath);
			if (apk.SkipExistingFile (filesystemPath, archivePath, compressionMethod)) {
				Log.LogDebugMessage ($"Skipping {filesystemPath} (APK path: {archivePath}) as it is up to date.");
				return;
			}
			Log.LogDebugMessage ($"Adding native library: {filesystemPath} (APK path: {archivePath})");
			ELFHelper.AssertValidLibraryAlignment (Log, ZipAlignmentPages, filesystemPath, taskItem);
			apk.AddEntryAndFlush (archivePath, File.OpenRead (filesystemPath), compressionMethod);
		}

		void AddRuntimeLibraries (ZipArchiveEx apk, string [] supportedAbis)
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
