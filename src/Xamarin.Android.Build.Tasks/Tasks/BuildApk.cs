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
using Xamarin.Android.Build.Utilities;
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

		public ITaskItem[] BundleNativeLibraries { get; set; }

		public ITaskItem[] Environments { get; set; }

		[Required]
		public ITaskItem[] TypeMappings { get; set; }

		[Required]
		public string [] DalvikClasses { get; set; }

		[Required]
		public string SupportedAbis { get; set; }

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

		public string AndroidAotMode { get; set; }		

		public string AndroidSequencePointsMode { get; set; }

		public bool EnableLLVM { get; set; }

		public bool EnableSGenConcurrent { get; set; }

		public string StubApplicationDataFile { get; set; }
		public string AndroidGdbDebugServer { get; set; }
		public string AndroidEmbedProfilers { get; set; }
		public string HttpClientHandlerType { get; set; }
		public string TlsProvider { get; set; }


		static  readonly    string          MSBuildXamarinAndroidDirectory      = Path.GetDirectoryName (typeof (BuildApk).Assembly.Location);

		[Output]
		public ITaskItem[] OutputFiles { get; set; }

		[Output]
		public string BuildId { get; set; }

		bool _Debug {
			get {
				return string.Equals (Debug, "true", StringComparison.OrdinalIgnoreCase);
			}
		}

		SequencePointsMode sequencePointsMode = SequencePointsMode.None;

		AndroidDebugServer debugServer = AndroidDebugServer.Gdb;
		Guid buildId = Guid.NewGuid ();
		
		public ITaskItem[] LibraryProjectJars { get; set; }

		void ExecuteWithAbi (string supportedAbis, string apkInputPath, string apkOutputPath)
		{
			ArchiveFileList files = new ArchiveFileList ();
			if (apkInputPath != null)
				File.Copy (apkInputPath, apkOutputPath + "new", overwrite: true);
			using (var apk = new ZipArchiveEx (apkOutputPath + "new", apkInputPath != null ? FileMode.Open : FileMode.Create )) {
				apk.Archive.AddEntry ("NOTICE",
						Assembly.GetExecutingAssembly ().GetManifestResourceStream ("NOTICE.txt"));

				// Add classes.dx
				apk.Archive.AddFiles (DalvikClasses, useFileDirectories: false);

				if (EmbedAssemblies && !BundleAssemblies)
					AddAssemblies (apk);

				AddEnvironment (apk);
				AddRuntimeLibraries (apk, supportedAbis);
				apk.Flush();
				AddNativeLibraries (files, supportedAbis);
				apk.Flush();
				AddAdditionalNativeLibraries (files, supportedAbis);
				apk.Flush();
				AddNativeLibrariesFromAssemblies (apk, supportedAbis);
				apk.Flush();

				foreach (ITaskItem typemap in TypeMappings) {
					apk.Archive.AddFile (typemap.ItemSpec, Path.GetFileName(typemap.ItemSpec), compressionMethod: CompressionMethod.Store);
				}

				int count = 0;
				foreach (var file in files) {
					var item = Path.Combine (file.Item2, Path.GetFileName (file.Item1))
						.Replace (Path.DirectorySeparatorChar, '/');
					if (apk.Archive.ContainsEntry (item)) {
						Log.LogWarning (null, "XA4301", null, file.Item1, 0, 0, 0, 0, "Apk already contains the item {0}; ignoring.", item);
						continue;
					}
					apk.Archive.AddFile (file.Item1, item);
					count++;
					if (count == ZipArchiveEx.ZipFlushLimit) {
						apk.Flush();
						count = 0;
					}
				}
				if (_Debug)
					AddGdbservers (apk, files, supportedAbis, debugServer);

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
							if (apk.Archive.Any (e => e.FullName == jarItem.FullName))
								Log.LogMessage ("Warning: failed to add jar entry {0} from {1}: the same file already exists in the apk", jarItem.FullName, Path.GetFileName (jarFile));
							else
								apk.Archive.AddEntry (data, jarItem.FullName);
						}
					}
					count++;
					if (count == ZipArchiveEx.ZipFlushLimit) {
						apk.Flush();
						count = 0;
					}
				}
				if (StubApplicationDataFile != null && File.Exists (StubApplicationDataFile))
					apk.Archive.AddFile (StubApplicationDataFile, Path.GetFileName (StubApplicationDataFile));
			}
			MonoAndroidHelper.CopyIfZipChanged (apkOutputPath + "new", apkOutputPath);
			File.Delete (apkOutputPath + "new");
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("BuildApk Task");
			Log.LogDebugMessage ("  ApkInputPath: {0}", ApkInputPath);
			Log.LogDebugMessage ("  ApkOutputPath: {0}", ApkOutputPath);
			Log.LogDebugMessage ("  BundleAssemblies: {0}", BundleAssemblies);
			Log.LogDebugTaskItems ("  DalvikClasses:", DalvikClasses);
			Log.LogDebugMessage ("  SupportedAbis: {0}", SupportedAbis);
			Log.LogDebugMessage ("  UseSharedRuntime: {0}", UseSharedRuntime);
			Log.LogDebugMessage ("  Debug: {0}", Debug ?? "no");
			Log.LogDebugMessage ("  PreferNativeLibrariesWithDebugSymbols: {0}", PreferNativeLibrariesWithDebugSymbols);
			Log.LogDebugMessage ("  EmbedAssemblies: {0}", EmbedAssemblies);
			Log.LogDebugMessage ("  AndroidAotMode: {0}", AndroidAotMode);
			Log.LogDebugMessage ("  AndroidSequencePointsMode: {0}", AndroidSequencePointsMode);
			Log.LogDebugMessage ("  CreatePackagePerAbi: {0}", CreatePackagePerAbi);
			Log.LogDebugTaskItems ("  Environments:", Environments);
			Log.LogDebugTaskItems ("  ResolvedUserAssemblies:", ResolvedUserAssemblies);
			Log.LogDebugTaskItems ("  ResolvedFrameworkAssemblies:", ResolvedFrameworkAssemblies);
			Log.LogDebugTaskItems ("  NativeLibraries:", NativeLibraries);
			Log.LogDebugTaskItems ("  AdditionalNativeLibraryReferences:", AdditionalNativeLibraryReferences);
			Log.LogDebugTaskItems ("  BundleNativeLibraries:", BundleNativeLibraries);
			Log.LogDebugTaskItems ("  JavaSourceFiles:", JavaSourceFiles);
			Log.LogDebugTaskItems ("  JavaLibraries:", JavaLibraries);
			Log.LogDebugTaskItems ("  LibraryProjectJars:", LibraryProjectJars);
			Log.LogDebugTaskItems ("  AdditionalNativeLibraryReferences:", AdditionalNativeLibraryReferences);
			Log.LogDebugTaskItems ("  HttpClientHandlerType:", HttpClientHandlerType);
			Log.LogDebugMessage ("  TlsProvider: {0}", TlsProvider);

			Aot.TryGetSequencePointsMode (AndroidSequencePointsMode, out sequencePointsMode);

			var androidDebugServer = GdbPaths.GetAndroidDebugServer (AndroidGdbDebugServer);
			if (!androidDebugServer.HasValue) {
				Log.LogError ("Unable to determine debug server variant: {0}", AndroidGdbDebugServer);
				return false;
			}
			debugServer = androidDebugServer.Value;

			if (string.IsNullOrEmpty (AndroidEmbedProfilers) && _Debug) {
				AndroidEmbedProfilers = "log";
			}

			var outputFiles = new List<string> ();

			ExecuteWithAbi (SupportedAbis, ApkInputPath, ApkOutputPath);
			outputFiles.Add (ApkOutputPath);
			if (CreatePackagePerAbi) {
				var abis = SupportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
				if (abis.Length > 1)
					foreach (var abi in abis) {
						var path = Path.GetDirectoryName (ApkOutputPath);
						var apk = Path.GetFileNameWithoutExtension (ApkOutputPath);
						ExecuteWithAbi (abi, String.Format ("{0}-{1}", ApkInputPath, abi), 
							Path.Combine (path, String.Format ("{0}-{1}.apk", apk, abi)));
						outputFiles.Add (Path.Combine (path, String.Format ("{0}-{1}.apk", apk, abi)));
					}
			}

			BuildId = buildId.ToString ();

			Log.LogDebugMessage ("  [Output] BuildId: {0}", BuildId);

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

				if (MonoAndroidHelper.IsReferenceAssembly (assembly.ItemSpec)) {
					Log.LogWarning ($"{assembly.ItemSpec} is a reference assembly!");
				}
				// Add assembly
				apk.Archive.AddFile (assembly.ItemSpec, GetTargetDirectory (assembly.ItemSpec) + "/"  + Path.GetFileName (assembly.ItemSpec), compressionMethod: CompressionMethod.Store);

				// Try to add config if exists
				var config = Path.ChangeExtension (assembly.ItemSpec, "dll.config");
				AddAssemblyConfigEntry (apk, config);

				// Try to add symbols if Debug
				if (debug) {
					var symbols = Path.ChangeExtension (assembly.ItemSpec, "dll.mdb");

					if (File.Exists (symbols))
						apk.Archive.AddFile (symbols, "assemblies/" + Path.GetFileName (symbols), compressionMethod: CompressionMethod.Store);

					symbols = Path.ChangeExtension (assembly.ItemSpec, "pdb");

					if (File.Exists (symbols))
						apk.Archive.AddFile (symbols, "assemblies/" + Path.GetFileName (symbols), compressionMethod: CompressionMethod.Store);
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
				if (MonoAndroidHelper.IsReferenceAssembly (assembly.ItemSpec)) {
					Log.LogWarning ($"{assembly.ItemSpec} is a reference assembly!");
				}
				apk.Archive.AddFile (assembly.ItemSpec, "assemblies/" + Path.GetFileName (assembly.ItemSpec), compressionMethod: CompressionMethod.Store);
				var config = Path.ChangeExtension (assembly.ItemSpec, "dll.config");
				AddAssemblyConfigEntry (apk, config);
				// Try to add symbols if Debug
				if (debug) {
					var symbols = Path.ChangeExtension (assembly.ItemSpec, "dll.mdb");

					if (File.Exists (symbols))
						apk.Archive.AddFile (symbols, "assemblies/" + Path.GetFileName (symbols), compressionMethod: CompressionMethod.Store);

					symbols = Path.ChangeExtension (assembly.ItemSpec, "pdb");

					if (File.Exists (symbols))
						apk.Archive.AddFile (symbols, "assemblies/" + Path.GetFileName (symbols), compressionMethod: CompressionMethod.Store);
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
				apk.Archive.AddEntry ("assemblies/" + Path.GetFileName (configFile), dest, compressionMethod: CompressionMethod.Store);
			}
		}

		static string GetTargetDirectory (string path)
		{
			string culture, file;
			if (SatelliteAssembly.TryGetSatelliteCultureAndFileName (path, out culture, out file)) {
				return "assemblies/" + culture;
			}
			return "assemblies";
		}

		void AddEnvironment (ZipArchiveEx apk)
		{
			var environment = new StringWriter () {
				NewLine = "\n",
			};

			if (EnableLLVM) {
				environment.WriteLine ("mono.llvm=true");
			}

			AotMode aotMode;
			if (AndroidAotMode != null && Aot.GetAndroidAotMode(AndroidAotMode, out aotMode)) {
				environment.WriteLine ("mono.aot={0}", aotMode.ToString().ToLowerInvariant());
			}

			const string defaultLogLevel = "MONO_LOG_LEVEL=info";
			const string defaultMonoDebug = "MONO_DEBUG=gen-compact-seq-points";
			const string defaultHttpMessageHandler = "XA_HTTP_CLIENT_HANDLER_TYPE=System.Net.Http.HttpClientHandler, System.Net.Http";
			const string defaultTlsProvider = "XA_TLS_PROVIDER=default";
			string xamarinBuildId = string.Format ("XAMARIN_BUILD_ID={0}", buildId);

			bool haveLogLevel = false;
			bool haveMonoDebug = false;
			bool havebuildId = false;
			bool haveHttpMessageHandler = false;
			bool haveTlsProvider = false;
			bool haveMonoGCParams = false;

			foreach (ITaskItem env in Environments ?? new TaskItem[0]) {
				environment.WriteLine ("## Source File: {0}", env.ItemSpec);
				foreach (string line in File.ReadLines (env.ItemSpec)) {
					var lineToWrite = line;
					if (lineToWrite.StartsWith ("MONO_LOG_LEVEL=", StringComparison.Ordinal))
						haveLogLevel = true;
					if (lineToWrite.StartsWith ("MONO_GC_PARAMS=", StringComparison.Ordinal))
						haveMonoGCParams = true;
					if (lineToWrite.StartsWith ("XAMARIN_BUILD_ID=", StringComparison.Ordinal))
						havebuildId = true;
					if (lineToWrite.StartsWith ("MONO_DEBUG=", StringComparison.Ordinal)) {
						haveMonoDebug = true;
						if (sequencePointsMode != SequencePointsMode.None && !lineToWrite.Contains ("gen-compact-seq-points"))
							lineToWrite = line  + ",gen-compact-seq-points";
					}
					if (lineToWrite.StartsWith ("XA_HTTP_CLIENT_HANDLER_TYPE=", StringComparison.Ordinal))
						haveHttpMessageHandler = true;
					if (lineToWrite.StartsWith ("XA_TLS_PROVIDER=", StringComparison.Ordinal))
						haveTlsProvider = true;
					environment.WriteLine (lineToWrite);
				}
			}

			if (_Debug && !haveLogLevel) {
				environment.WriteLine (defaultLogLevel);
			}

			if (sequencePointsMode != SequencePointsMode.None && !haveMonoDebug) {
				environment.WriteLine (defaultMonoDebug);
			}

			if (!havebuildId)
				environment.WriteLine (xamarinBuildId);

			if (!haveHttpMessageHandler)
				environment.WriteLine (HttpClientHandlerType == null ? defaultHttpMessageHandler : $"XA_HTTP_CLIENT_HANDLER_TYPE={HttpClientHandlerType.Trim ()}");
			if (!haveTlsProvider)
				environment.WriteLine (TlsProvider == null ? defaultTlsProvider : $"XA_TLS_PROVIDER={TlsProvider.Trim ()}");
			if (!haveMonoGCParams) {
				if (EnableSGenConcurrent)
					environment.WriteLine ("MONO_GC_PARAMS=major=marksweep-conc");
				else
					environment.WriteLine ("MONO_GC_PARAMS=major=marksweep");
			}

			apk.Archive.AddEntry ("environment", environment.ToString (),
					new UTF8Encoding (encoderShouldEmitUTF8Identifier:false));
		}

		class LibInfo
		{
			public string Path;
			public string Abi;
		}

		static readonly string[] ArmAbis = new[]{
			"arm64-v8a",
			"armeabi-v7a",
			"armeabi",
		};

		public static readonly string[] ValidProfilers = new[]{
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

		void AddNativeLibrary (ZipArchiveEx apk, string abi, string filename, string inArchiveFileName = null)
		{
			string libPath = Path.Combine (MSBuildXamarinAndroidDirectory, "lib", abi);
			string path    = Path.Combine (libPath, filename);
			if (PreferNativeLibrariesWithDebugSymbols) {
				string debugPath = Path.Combine (libPath, Path.ChangeExtension (filename, ".d.so"));
				if (File.Exists (debugPath))
					path = debugPath;
			}

			string archivePath = string.Format ("lib/{0}/{1}", abi, inArchiveFileName ?? filename);
			Log.LogDebugMessage ($"Adding native library: {path} (APK path: {archivePath})");
			apk.Archive.AddEntry (archivePath, File.OpenRead (path));
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
			if (string.Compare ("btls", TlsProvider, StringComparison.OrdinalIgnoreCase) == 0) {
				AddNativeLibrary (apk, abi, "libmono-btls-shared.so");
			}
			// These are the other supported values
			//  * "default":
			//  * "legacy":
		}

		void AddRuntimeLibraries (ZipArchiveEx apk, string supportedAbis)
		{
			bool use_shared_runtime = String.Equals (UseSharedRuntime, "true", StringComparison.OrdinalIgnoreCase);
			var abis = supportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var abi in abis) {
				string library = string.Format ("libmono-android.{0}.so", _Debug ? "debug" : "release");
				AddNativeLibrary (apk, abi, library, "libmonodroid.so");
				if (!use_shared_runtime) {
					// include the sgen
					AddNativeLibrary (apk, abi, "libmonosgen-2.0.so");
				}
				AddBtlsLibs (apk, abi);
				AddProfilers (apk, abi);
			}
		}

		void AddNativeLibrariesFromAssemblies (ZipArchiveEx apk, string supportedAbis)
		{
			int count = 0;
			var abis = supportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			using (var res = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: false)) {
			foreach (var assembly in EmbeddedNativeLibraryAssemblies)
				res.Load (assembly.ItemSpec);
				foreach (var assemblyPath in EmbeddedNativeLibraryAssemblies) {
					var assembly = res.GetAssembly (assemblyPath.ItemSpec);
					foreach (var mod in assembly.Modules) {
						var ressozip = mod.Resources.FirstOrDefault (r => r.Name == "__AndroidNativeLibraries__.zip") as EmbeddedResource;
						if (ressozip == null)
							continue;
						var data = ressozip.GetResourceData ();
						using (var ms = new MemoryStream (data)) {
							using (var zip = ZipArchive.Open (ms)) {
								foreach (var e in zip.Where (x => abis.Any (a => x.FullName.Contains (a)))) {
									if (e.IsDirectory)
										continue;
									var key = e.FullName.Replace ("native_library_imports", "lib");
									if (apk.Archive.Any (k => k.FullName == key)) {
										Log.LogCodedWarning ("4301", "Apk already contains the item {0}; ignoring.", key);
										continue;
									}
									using (var s = new MemoryStream ()) {
										e.Extract (s);
										s.Position = 0;
										apk.Archive.AddEntry (s.ToArray (), key);
									}
								}
							}
						}
					}
					count++;
					if (count == ZipArchiveEx.ZipFlushLimit) {
						apk.Flush();
						count = 0;
					}
				}
			}
		}

		private void AddNativeLibraries (ArchiveFileList files, string supportedAbis)
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
				Log.LogError ("Cannot determine abi of native library {0}.", lib);
				return null;
			}

			return lib_abi;
		}

		void AddNativeLibraries (ArchiveFileList files, string supportedAbis, System.Collections.Generic.IEnumerable<LibInfo> libs)
		{
			if (libs.Any (lib => lib.Abi == null))
				Log.LogCodedWarning (
						"4301",
						"Could not determine abi of some native libraries, ignoring those: " +
				                    string.Join (", ", libs.Where (lib => lib.Abi == null).Select (lib => lib.Path)));
			libs = libs.Where (lib => lib.Abi != null);

			var abis = supportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			libs = libs.Where (lib => abis.Contains (lib.Abi));
			foreach (var arm in ArmAbis)
				foreach (var info in libs.Where (lib => lib.Abi == arm))
					AddNativeLibrary (files, info.Path, info.Abi);
			foreach (var info in libs.Where (lib => !ArmAbis.Contains (lib.Abi)))
				AddNativeLibrary (files, info.Path, info.Abi);
		}

		private void AddAdditionalNativeLibraries (ArchiveFileList files, string supportedAbis)
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

		private void AddGdbservers (ZipArchiveEx apk, ArchiveFileList files, string supportedAbis, AndroidDebugServer debugServer)
		{
			if (string.IsNullOrEmpty (AndroidNdkDirectory))
				return;

			var sdkBinDirectory = MonoAndroidHelper.GetOSBinPath ();
			int count = 0;
			foreach (var sabi in supportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)) {
				var arch = GdbPaths.GetArchFromAbi (sabi);
				var abi  = GdbPaths.GetAbiFromArch (arch);
				if (abi == null)
					continue;
				var debugServerFile = GdbPaths.GetDebugServerFileName (debugServer);
				if (files.Any (f => string.Equals (Path.GetFileName (f.Item1), debugServerFile, StringComparison.Ordinal) &&
							string.Equals (f.Item2, "lib/" + sabi, StringComparison.Ordinal)))
					continue;
				var entryName = string.Format ("lib/{0}/{1}", sabi, debugServerFile);
				var debugServerPath = GdbPaths.GetDebugServerPath (debugServer, arch, AndroidNdkDirectory, sdkBinDirectory);
				if (!File.Exists (debugServerPath))
					continue;
				Log.LogDebugMessage ("Adding {0} debug server '{1}' to the APK as '{2}'", sabi, debugServerPath, entryName);
				apk.Archive.AddEntry (entryName, File.OpenRead (debugServerPath));
				count++;
				if (count == ZipArchiveEx.ZipFlushLimit) {
					apk.Flush();
					count = 0;
				}
			}
		}
	}
}
