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

using ArchiveFileList = System.Collections.Generic.List<System.Tuple<string, string>>;
using Mono.Cecil;
using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks
{
	public class BuildApk : Task
	{
		public string AndroidNdkDirectory { get; set; }

		[Required]
		public string ApkInputPath { get; set; }
		
		[Required]
		public string ApkOutputPath { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedFrameworkAssemblies { get; set; }

		public ITaskItem[] AdditionalNativeLibraryReferences { get; set; }

		public ITaskItem[] EmbeddedNativeLibraryAssemblies { get; set; }

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

		[Required]
		public string UseSharedRuntime { get; set; }

		public bool EmbedAssemblies { get; set; }

		public bool BundleAssemblies { get; set; }

		public ITaskItem[] JavaSourceFiles { get; set; }

		public ITaskItem[] JavaLibraries { get; set; }

		public string[] DoNotPackageJavaLibraries { get; set; }

		public string Debug { get; set; }

		public bool PreferNativeLibrariesWithDebugSymbols { get; set; }

		public string AndroidSequencePointsMode { get; set; }

		public string AndroidEmbedProfilers { get; set; }
		public string TlsProvider { get; set; }
		public string UncompressedFileExtensions { get; set; }

		static  readonly    string          MSBuildXamarinAndroidDirectory      = Path.GetDirectoryName (typeof (BuildApk).Assembly.Location);

		[Output]
		public ITaskItem[] OutputFiles { get; set; }

		bool _Debug {
			get {
				return string.Equals (Debug, "true", StringComparison.OrdinalIgnoreCase);
			}
		}

		SequencePointsMode sequencePointsMode = SequencePointsMode.None;
		
		public ITaskItem[] LibraryProjectJars { get; set; }
		string [] uncompressedFileExtensions;

		protected virtual string RootPath => "";

		protected virtual string AssembliesPath => RootPath + "assemblies/";

		protected virtual string DalvikPath => "";

		protected virtual CompressionMethod UncompressedMethod => CompressionMethod.Store;

		protected virtual void FixupArchive (ZipArchiveEx zip) { }

		void ExecuteWithAbi (string [] supportedAbis, string apkInputPath, string apkOutputPath)
		{
			var temp = apkOutputPath + "new";
			ArchiveFileList files = new ArchiveFileList ();
			if (apkInputPath != null)
				File.Copy (apkInputPath, temp, overwrite: true);
			using (var apk = new ZipArchiveEx (temp, apkInputPath != null ? FileMode.Open : FileMode.Create )) {
				apk.FixupWindowsPathSeparators ((a, b) => Log.LogDebugMessage ($"Fixing up malformed entry `{a}` -> `{b}`"));
				apk.Archive.AddEntry (RootPath + "NOTICE",
						Assembly.GetExecutingAssembly ().GetManifestResourceStream ("NOTICE.txt"));

				// Add classes.dx
				foreach (var dex in DalvikClasses) {
					string apkName = dex.GetMetadata ("ApkName");
					string dexPath = string.IsNullOrWhiteSpace (apkName) ? Path.GetFileName (dex.ItemSpec) : apkName;
					apk.Archive.AddFile (dex.ItemSpec, DalvikPath + dexPath);
				}

				if (EmbedAssemblies && !BundleAssemblies)
					AddAssemblies (apk);

				AddRuntimeLibraries (apk, supportedAbis);
				apk.Flush();
				AddNativeLibraries (files, supportedAbis);
				apk.Flush();
				AddAdditionalNativeLibraries (files, supportedAbis);
				apk.Flush();

				if (TypeMappings != null) {
					foreach (ITaskItem typemap in TypeMappings) {
						apk.Archive.AddFile (typemap.ItemSpec, RootPath + Path.GetFileName(typemap.ItemSpec), compressionMethod: UncompressedMethod);
					}
				}

				int count = 0;
				foreach (var file in files) {
					var item = Path.Combine (file.Item2, Path.GetFileName (file.Item1))
						.Replace (Path.DirectorySeparatorChar, '/');
					if (apk.Archive.ContainsEntry (item)) {
						Log.LogCodedWarning ("XA4301", file.Item1, 0, "Apk already contains the item {0}; ignoring.", item);
						continue;
					}
					apk.Archive.AddFile (file.Item1, item, compressionMethod: GetCompressionMethod (file.Item1));
					count++;
					if (count == ZipArchiveEx.ZipFlushLimit) {
						apk.Flush();
						count = 0;
					}
				}

				var jarFiles = (JavaSourceFiles != null) ? JavaSourceFiles.Where (f => f.ItemSpec.EndsWith (".jar")) : null;
				if (jarFiles != null && JavaLibraries != null)
					jarFiles = jarFiles.Concat (JavaLibraries);
				else if (JavaLibraries != null)
					jarFiles = JavaLibraries;

				var libraryProjectJars  = MonoAndroidHelper.ExpandFiles (LibraryProjectJars)
					.Where (jar => !MonoAndroidHelper.IsEmbeddedReferenceJar (jar));

				var jarFilePaths = libraryProjectJars.Concat (jarFiles != null ? jarFiles.Select (j => j.ItemSpec) : Enumerable.Empty<string> ());
				jarFilePaths = MonoAndroidHelper.DistinctFilesByContent (jarFilePaths);

				count = 0;
				foreach (var jarFile in jarFilePaths) {
					using (var jar = ZipArchive.Open (File.OpenRead (jarFile))) {
						foreach (var jarItem in jar.Where (ze => !ze.IsDirectory && !ze.FullName.StartsWith ("META-INF") && !ze.FullName.EndsWith (".class") && !ze.FullName.EndsWith (".java") && !ze.FullName.EndsWith ("MANIFEST.MF"))) {
							byte [] data;
							using (var d = new System.IO.MemoryStream ()) {
								jarItem.Extract (d);
								data = d.ToArray ();
							}
							var path = RootPath + jarItem.FullName;
							if (apk.Archive.Any (e => e.FullName == path))
								Log.LogMessage ("Warning: failed to add jar entry {0} from {1}: the same file already exists in the apk", jarItem.FullName, Path.GetFileName (jarFile));
							else
								apk.Archive.AddEntry (data, path);
						}
					}
					count++;
					if (count == ZipArchiveEx.ZipFlushLimit) {
						apk.Flush();
						count = 0;
					}
				}
				FixupArchive (apk);
			}
			if (MonoAndroidHelper.CopyIfZipChanged (temp, apkOutputPath)) {
				Log.LogDebugMessage ($"Copied {temp} to {apkOutputPath}");
			} else {
				Log.LogDebugMessage ($"Skipped {apkOutputPath}: up to date");
			}
			File.Delete (temp);
		}

		public override bool Execute ()
		{
			Aot.TryGetSequencePointsMode (AndroidSequencePointsMode, out sequencePointsMode);

			if (string.IsNullOrEmpty (AndroidEmbedProfilers) && _Debug) {
				AndroidEmbedProfilers = "log";
			}

			var outputFiles = new List<string> ();
			uncompressedFileExtensions = UncompressedFileExtensions?.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries) ?? new string [0];

			ExecuteWithAbi (SupportedAbis, ApkInputPath, ApkOutputPath);
			outputFiles.Add (ApkOutputPath);
			if (CreatePackagePerAbi && SupportedAbis.Length > 1) {
				foreach (var abi in SupportedAbis) {
					var path = Path.GetDirectoryName (ApkOutputPath);
					var apk = Path.GetFileNameWithoutExtension (ApkOutputPath);
					ExecuteWithAbi (new [] { abi }, String.Format ("{0}-{1}", ApkInputPath, abi), 
						Path.Combine (path, String.Format ("{0}-{1}.apk", apk, abi)));
					outputFiles.Add (Path.Combine (path, String.Format ("{0}-{1}.apk", apk, abi)));
				}
			}

			OutputFiles = outputFiles.Select (a => new TaskItem (a)).ToArray ();

			Log.LogDebugTaskItems ("  [Output] OutputFiles :", OutputFiles);

			return !Log.HasLoggedErrors;
		}

		private void AddAssemblies (ZipArchiveEx apk)
		{
			bool debug = _Debug;
			bool use_shared_runtime = String.Equals (UseSharedRuntime, "true", StringComparison.OrdinalIgnoreCase);

			int count = 0;
			foreach (ITaskItem assembly in ResolvedUserAssemblies) {
				if (bool.TryParse (assembly.GetMetadata ("AndroidSkipAddToPackage"), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping {assembly.ItemSpec} due to 'AndroidSkipAddToPackage' == 'true' ");
					continue;
				}
				if (MonoAndroidHelper.IsReferenceAssembly (assembly.ItemSpec)) {
					Log.LogCodedWarning ("XA0107", assembly.ItemSpec, 0, "{0} is a Reference Assembly!", assembly.ItemSpec);
				}
				// Add assembly
				apk.Archive.AddFile (assembly.ItemSpec, GetTargetDirectory (assembly.ItemSpec) + "/"  + Path.GetFileName (assembly.ItemSpec), compressionMethod: UncompressedMethod);

				// Try to add config if exists
				var config = Path.ChangeExtension (assembly.ItemSpec, "dll.config");
				AddAssemblyConfigEntry (apk, config);

				// Try to add symbols if Debug
				if (debug) {
					var symbols = Path.ChangeExtension (assembly.ItemSpec, "dll.mdb");

					if (File.Exists (symbols))
						apk.Archive.AddFile (symbols, AssembliesPath + Path.GetFileName (symbols), compressionMethod: UncompressedMethod);

					symbols = Path.ChangeExtension (assembly.ItemSpec, "pdb");

					if (File.Exists (symbols))
						apk.Archive.AddFile (symbols, AssembliesPath + Path.GetFileName (symbols), compressionMethod: UncompressedMethod);
				}
				count++;
				if (count == ZipArchiveEx.ZipFlushLimit) {
					apk.Flush();
					count = 0;
				}
			}

			if (use_shared_runtime)
				return;

			count = 0;
			// Add framework assemblies
			foreach (ITaskItem assembly in ResolvedFrameworkAssemblies) {
				if (bool.TryParse (assembly.GetMetadata ("AndroidSkipAddToPackage"), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping {assembly.ItemSpec} due to 'AndroidSkipAddToPackage' == 'true' ");
					continue;
				}
				if (MonoAndroidHelper.IsReferenceAssembly (assembly.ItemSpec)) {
					Log.LogCodedWarning ("XA0107", assembly.ItemSpec, 0, "{0} is a Reference Assembly!", assembly.ItemSpec);
				}
				apk.Archive.AddFile (assembly.ItemSpec, AssembliesPath + Path.GetFileName (assembly.ItemSpec), compressionMethod: UncompressedMethod);
				var config = Path.ChangeExtension (assembly.ItemSpec, "dll.config");
				AddAssemblyConfigEntry (apk, config);
				// Try to add symbols if Debug
				if (debug) {
					var symbols = Path.ChangeExtension (assembly.ItemSpec, "dll.mdb");

					if (File.Exists (symbols))
						apk.Archive.AddFile (symbols, AssembliesPath + Path.GetFileName (symbols), compressionMethod: UncompressedMethod);

					symbols = Path.ChangeExtension (assembly.ItemSpec, "pdb");

					if (File.Exists (symbols))
						apk.Archive.AddFile (symbols, AssembliesPath + Path.GetFileName (symbols), compressionMethod: UncompressedMethod);
				}
				count++;
				if (count == ZipArchiveEx.ZipFlushLimit) {
					apk.Flush();
					count = 0;
				}
			}
		}

		void AddAssemblyConfigEntry (ZipArchiveEx apk, string configFile)
		{
			if (!File.Exists (configFile))
				return;

			using (var source = File.OpenRead (configFile)) {
				var dest = new MemoryStream ();
				source.CopyTo (dest);
				dest.WriteByte (0);
				dest.Position = 0;
				apk.Archive.AddEntry (AssembliesPath + Path.GetFileName (configFile), dest, compressionMethod: UncompressedMethod);
			}
		}

		string GetTargetDirectory (string path)
		{
			string culture, file;
			if (SatelliteAssembly.TryGetSatelliteCultureAndFileName (path, out culture, out file)) {
				return AssembliesPath + culture;
			}
			return AssembliesPath.TrimEnd ('/');
		}

		class LibInfo
		{
			public string Path;
			public string Abi;
		}

		static readonly string[] ArmAbis = new[]{
			"arm64-v8a",
			"armeabi-v7a",
		};

		public static readonly string[] ValidProfilers = new[]{
			"aot",
			"log",
		};

		HashSet<string> ParseProfilers (string value)
		{
			var results = new HashSet<string> ();
			var values = value.Split (',', ';');
			foreach (var v in values) {
				if (string.Compare (v, "all", true) == 0) {
					results.UnionWith (ValidProfilers);
					break;
				}
				if (Array.BinarySearch (ValidProfilers, v, StringComparer.OrdinalIgnoreCase) < 0)
					throw new InvalidOperationException ("Unsupported --profiler value: " + v + ".");
				results.Add (v.ToLowerInvariant ());
			}
			return results;
		}

		CompressionMethod GetCompressionMethod (string fileName)
		{
			if (uncompressedFileExtensions.Any (x => string.Compare (x, Path.GetExtension (fileName), StringComparison.OrdinalIgnoreCase) == 0))
				return UncompressedMethod;
			return CompressionMethod.Default;
		}

		void AddNativeLibraryToArchive (ZipArchiveEx apk, string abi, string filesystemPath, string inArchiveFileName)
		{
			string archivePath = $"lib/{abi}/{inArchiveFileName}";
			Log.LogDebugMessage ($"Adding native library: {filesystemPath} (APK path: {archivePath})");
			apk.Archive.AddEntry (archivePath, File.OpenRead (filesystemPath), compressionMethod: GetCompressionMethod (archivePath));
		}

		void AddNativeLibrary (ZipArchiveEx apk, string abi, string filename, string inArchiveFileName = null)
		{
			string libPath = Path.Combine (MSBuildXamarinAndroidDirectory, "lib", abi);
			string path    = Path.Combine (libPath, filename);
			if (PreferNativeLibrariesWithDebugSymbols) {
				string debugPath = Path.Combine (libPath, Path.ChangeExtension (filename, ".d.so"));
				if (File.Exists (debugPath))
					path = debugPath;
			}

			AddNativeLibraryToArchive (apk, abi, path, inArchiveFileName ?? filename);
		}

		void AddProfilers (ZipArchiveEx apk, string abi)
		{
			if (!string.IsNullOrEmpty (AndroidEmbedProfilers)) {
				foreach (var profiler in ParseProfilers (AndroidEmbedProfilers)) {
					var library = string.Format ("libmono-profiler-{0}.so", profiler);
					AddNativeLibrary (apk, abi, library);
				}
			}
		}

		void AddBtlsLibs (ZipArchiveEx apk, string abi)
		{
			if (string.IsNullOrEmpty (TlsProvider) ||
					string.Compare ("btls", TlsProvider, StringComparison.OrdinalIgnoreCase) == 0) {
				AddNativeLibrary (apk, abi, "libmono-btls-shared.so");
			}
			// These are the other supported values
			//  * "default":
			//  * "legacy":
		}

		void AddRuntimeLibraries (ZipArchiveEx apk, string [] supportedAbis)
		{
			bool use_shared_runtime = String.Equals (UseSharedRuntime, "true", StringComparison.OrdinalIgnoreCase);
			foreach (var abi in supportedAbis) {
				string library = string.Format ("libmono-android.{0}.so", _Debug ? "debug" : "release");
				AddNativeLibrary (apk, abi, library, "libmonodroid.so");

				foreach (ITaskItem item in ApplicationSharedLibraries) {
					if (String.Compare (abi, item.GetMetadata ("abi"), StringComparison.Ordinal) != 0)
						continue;
					AddNativeLibraryToArchive (apk, abi, item.ItemSpec, Path.GetFileName (item.ItemSpec));
				}

				if (!use_shared_runtime) {
					// include the sgen
					AddNativeLibrary (apk, abi, "libmonosgen-2.0.so");
				}
				AddBtlsLibs (apk, abi);
				AddProfilers (apk, abi);
			}
		}

		private void AddNativeLibraries (ArchiveFileList files, string [] supportedAbis)
		{
			var libs = NativeLibraries.Concat (BundleNativeLibraries ?? Enumerable.Empty<ITaskItem> ())
				.Select (v => new LibInfo { Path = v.ItemSpec, Abi = GetNativeLibraryAbi (v) });

			AddNativeLibraries (files, supportedAbis, libs);
		}

		string GetNativeLibraryAbi (ITaskItem lib)
		{
			// If Abi is explicitly specified, simply return it.
			var lib_abi = MonoAndroidHelper.GetNativeLibraryAbi (lib);
			
			if (string.IsNullOrWhiteSpace (lib_abi)) {
				Log.LogCodedError ("XA4301", lib.ItemSpec, 0, "Cannot determine abi of native library {0}.", lib.ItemSpec);
				return null;
			}

			return lib_abi;
		}

		void AddNativeLibraries (ArchiveFileList files, string [] supportedAbis, System.Collections.Generic.IEnumerable<LibInfo> libs)
		{
			if (libs.Any (lib => lib.Abi == null))
				Log.LogCodedWarning (
						"XA4301",
						"Could not determine abi of some native libraries, ignoring those: " +
				                    string.Join (", ", libs.Where (lib => lib.Abi == null).Select (lib => lib.Path)));
			libs = libs.Where (lib => lib.Abi != null);
			libs = libs.Where (lib => supportedAbis.Contains (lib.Abi));
			foreach (var arm in ArmAbis)
				foreach (var info in libs.Where (lib => lib.Abi == arm))
					AddNativeLibrary (files, info.Path, info.Abi);
			foreach (var info in libs.Where (lib => !ArmAbis.Contains (lib.Abi)))
				AddNativeLibrary (files, info.Path, info.Abi);
		}

		private void AddAdditionalNativeLibraries (ArchiveFileList files, string [] supportedAbis)
		{
			if (AdditionalNativeLibraryReferences == null || !AdditionalNativeLibraryReferences.Any ())
				return;

			var libs = AdditionalNativeLibraryReferences
				.Select (l => new LibInfo { Path = l.ItemSpec, Abi = MonoAndroidHelper.GetNativeLibraryAbi (l) });

			AddNativeLibraries (files, supportedAbis, libs);
		}

		void AddNativeLibrary (ArchiveFileList files, string path, string abi)
		{
			Log.LogMessage (MessageImportance.Low, "\tAdding {0}", path);
			files.Add (new Tuple<string, string> (path, string.Format ("lib/{0}", abi)));
		}
	}
}
