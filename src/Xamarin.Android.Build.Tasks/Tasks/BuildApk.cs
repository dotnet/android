// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Ionic.Zlib;
using Ionic.Zip;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

using Java.Interop.Tools.Cecil;

using ArchiveFileList = System.Collections.Generic.List<System.Tuple<string, string>>;
using Mono.Cecil;
using Xamarin.Android.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class BuildApk : Task
	{
		public string AndroidNdkDirectory { get; set; }

		public string SdkBinDirectory { get; set; }

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

		public string AndroidAotMode { get; set; }		

		public string AndroidSequencePointsMode { get; set; }

		public bool EnableLLVM { get; set; }

		public string StubApplicationDataFile { get; set; }
		public string AndroidGdbDebugServer { get; set; }
		public string AndroidEmbedProfilers { get; set; }
		public string HttpClientHandlerType { get; set; }

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
			using (ZipFile apk = apkInputPath != null ? MonoAndroidHelper.ReadZipFile (apkInputPath) : new ZipFile ()) {

				apk.AddEntry ("NOTICE",
						Assembly.GetExecutingAssembly ().GetManifestResourceStream ("NOTICE.txt"));

				// Add classes.dx
				apk.AddFiles (DalvikClasses, string.Empty);

				if (EmbedAssemblies && !BundleAssemblies)
					AddAssemblies (apk);

				AddEnvironment (apk);
				AddRuntimeLibraries (apk, supportedAbis);
				AddNativeLibraries (files, supportedAbis);
				AddAdditionalNativeLibraries (files, supportedAbis);
				AddNativeLibrariesFromAssemblies (apk, supportedAbis);

				foreach (ITaskItem typemap in TypeMappings) {
					apk.AddFile (typemap.ItemSpec, "").CompressionLevel = CompressionLevel.None;
				}

				foreach (var file in files) {
					var item = Path.Combine (file.Item2, Path.GetFileName (file.Item1))
						.Replace (Path.DirectorySeparatorChar, '/');
					if (apk.ContainsEntry (item)) {
						Log.LogWarning (null, "XA4301", null, file.Item1, 0, 0, 0, 0, "Apk already contains the item {0}; ignoring.", item);
						continue;
					}
					apk.AddFile (file.Item1, file.Item2);
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

				foreach (var jarFile in jarFilePaths) {
					using (var jar = MonoAndroidHelper.ReadZipFile (jarFile)) {
						foreach (var jarItem in jar.Where (ze => !ze.IsDirectory && !ze.FileName.StartsWith ("META-INF") && !ze.FileName.EndsWith (".class") && !ze.FileName.EndsWith (".java") && !ze.FileName.EndsWith ("MANIFEST.MF"))) {
							byte[] data;
							using (var d = new System.IO.MemoryStream ())
							using (var i = jarItem.OpenReader ()) {
								i.CopyTo (d);
								data = d.ToArray ();
							}
							if (apk.Entries.Any (e => e.FileName == jarItem.FileName))
								Log.LogMessage ("Warning: failed to add jar entry {0} from {1}: the same file already exists in the apk", jarItem.FileName, Path.GetFileName (jarFile));
							else
								apk.AddEntry (jarItem.FileName, data);
						}
					}
				}
				if (StubApplicationDataFile != null && File.Exists (StubApplicationDataFile))
					AddZipEntry (apk, StubApplicationDataFile, string.Empty);
				
				apk.Save (apkOutputPath + "new");
				MonoAndroidHelper.CopyIfZipChanged (apkOutputPath + "new", apkOutputPath);
				File.Delete (apkOutputPath + "new");
			}

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
			Log.LogDebugMessage ("  EmbedAssemblies: {0}", EmbedAssemblies);
			Log.LogDebugMessage ("  AndroidAotMode: {0}", AndroidAotMode);
			Log.LogDebugMessage ("  AndroidSequencePointsMode: {0}", AndroidSequencePointsMode);
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

			Aot.TryGetSequencePointsMode (AndroidSequencePointsMode, out sequencePointsMode);

			var androidDebugServer = GdbPaths.GetAndroidDebugServer (AndroidGdbDebugServer);
			if (!androidDebugServer.HasValue) {
				Log.LogError ("Unable to determine debug server variant: {0}", AndroidGdbDebugServer);
				return false;
			}
			debugServer = androidDebugServer.Value;

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

		void AddZipEntry (ZipFile apk, string file, string path)
		{
			apk.AddFile (file, path);
		}

		private void AddAssemblies (ZipFile apk)
		{
			bool debug = _Debug;
			bool use_shared_runtime = String.Equals (UseSharedRuntime, "true", StringComparison.OrdinalIgnoreCase);

			foreach (ITaskItem assembly in ResolvedUserAssemblies) {
				// Add assembly
				apk.AddFile (assembly.ItemSpec, GetTargetDirectory (assembly.ItemSpec)).CompressionLevel = CompressionLevel.None;

				// Try to add config if exists
				var config = Path.ChangeExtension (assembly.ItemSpec, "dll.config");
				AddAssemblyConfigEntry (apk, config);

				// Try to add symbols if Debug
				if (debug) {
					var symbols = Path.ChangeExtension (assembly.ItemSpec, "dll.mdb");

					if (File.Exists (symbols))
						apk.AddFile (symbols, "assemblies").CompressionLevel = CompressionLevel.None;
				}
			}

			if (use_shared_runtime)
				return;

			// Add framework assemblies
			foreach (ITaskItem assembly in ResolvedFrameworkAssemblies) {
				apk.AddFile (assembly.ItemSpec, "assemblies").CompressionLevel = CompressionLevel.None;
				var config = Path.ChangeExtension (assembly.ItemSpec, "dll.config");
				AddAssemblyConfigEntry (apk, config);
				// Try to add symbols if Debug
				if (debug) {
					var symbols = Path.ChangeExtension (assembly.ItemSpec, "dll.mdb");

					if (File.Exists (symbols))
						apk.AddFile (symbols, "assemblies").CompressionLevel = CompressionLevel.None;
				}
			}
		}

		void AddAssemblyConfigEntry (ZipFile apk, string configFile)
		{
			if (!File.Exists (configFile))
				return;

			using (var source = File.OpenRead (configFile)) {
				var dest = new MemoryStream ();
				source.CopyTo (dest);
				dest.WriteByte (0);
				dest.Position = 0;
				apk.AddEntry ("assemblies/" + Path.GetFileName (configFile), dest).CompressionLevel = CompressionLevel.None;
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

		void AddEnvironment (ZipFile apk)
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
			const string defaultHttpMessageHandler = "XA_HTTP_CLIENT_HANDLER_TYPE=Xamarin.Android.Net.AndroidClientHandler";
			string xamarinBuildId = string.Format ("XAMARIN_BUILD_ID={0}", buildId);

			if (Environments == null) {
				if (_Debug)
					environment.WriteLine (defaultLogLevel);
				if (sequencePointsMode != SequencePointsMode.None)
					environment.WriteLine (defaultMonoDebug);
				environment.WriteLine (xamarinBuildId);
				apk.AddEntry ("environment", environment.ToString(),
						new UTF8Encoding (encoderShouldEmitUTF8Identifier:false));
				return;
			}

			bool haveLogLevel = false;
			bool haveMonoDebug = false;
			bool havebuildId = false;
			bool haveHttpMessageHandler = false;

			foreach (ITaskItem env in Environments) {
				environment.WriteLine ("## Source File: {0}", env.ItemSpec);
				foreach (string line in File.ReadLines (env.ItemSpec)) {
					var lineToWrite = line;
					if (lineToWrite.StartsWith ("MONO_LOG_LEVEL=", StringComparison.Ordinal))
						haveLogLevel = true;
					if (lineToWrite.StartsWith ("XAMARIN_BUILD_ID=", StringComparison.Ordinal))
						havebuildId = true;
					if (lineToWrite.StartsWith ("MONO_DEBUG=", StringComparison.Ordinal)) {
						haveMonoDebug = true;
						if (sequencePointsMode != SequencePointsMode.None && !lineToWrite.Contains ("gen-compact-seq-points"))
							lineToWrite = line  + ",gen-compact-seq-points";
					}
					if (lineToWrite.StartsWith ("XA_HTTP_CLIENT_HANDLER_TYPE=", StringComparison.Ordinal))
						haveHttpMessageHandler = true;
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
			
			apk.AddEntry ("environment", environment.ToString (),
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

		void AddProfilers (ZipFile apk, string abi)
		{
			var root = Path.GetDirectoryName (typeof(BuildApk).Assembly.Location);
			if (!string.IsNullOrEmpty (AndroidEmbedProfilers)) {
				foreach (var profiler in ParseProfilers (AndroidEmbedProfilers)) {
					var library = string.Format ("libmono-profiler-{1}.{0}.so", abi, profiler);
					var path = Path.Combine (root, "lib", abi, library);
					apk.AddEntry (string.Format ("lib/{0}/libmono-profiler.so", abi), File.OpenRead (path));
				}
			}
		}

		void AddRuntimeLibraries (ZipFile apk, string supportedAbis)
		{
			var root = Path.GetDirectoryName (typeof(BuildApk).Assembly.Location);
			bool use_shared_runtime = String.Equals (UseSharedRuntime, "true", StringComparison.OrdinalIgnoreCase);
			var abis = supportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var abi in abis) {
				string library = string.Format ("libmono-android.{0}.{1}.so", _Debug ? "debug" : "release", abi);
				var path = Path.Combine (root, "lib", abi, library);
				apk.AddEntry (string.Format ("lib/{0}/libmonodroid.so", abi), File.OpenRead (path));
				if (!use_shared_runtime) {
					// include the sgen
					library = "libmonosgen-2.0.so";
					path = Path.Combine (root, "lib", abi, library);
					apk.AddEntry (string.Format ("lib/{0}/libmonosgen-2.0.so", abi), File.OpenRead (path));
				}
				AddProfilers (apk, abi);
			}
		}

		void AddNativeLibrariesFromAssemblies (ZipFile apk, string supportedAbis)
		{
			var abis = supportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			var res = new DirectoryAssemblyResolver (Console.WriteLine, loadDebugSymbols: false);
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
						using (var zip = ZipFile.Read (ms)) {
							foreach (var e in zip.Entries.Where (x => abis.Any (a => x.FileName.Contains (a)))) {
								if (e.IsDirectory)
									continue;
								var key = e.FileName.Replace ("native_library_imports", "lib");
								if (apk.ContainsEntry (key)) {
									Log.LogCodedWarning ("4301", "Apk already contains the item {0}; ignoring.", key);
									continue;
								}
								using (var s = new MemoryStream ()) {
									e.Extract (s);
									apk.AddEntry (key, s.ToArray ());
								}
							}
						}
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

		private void AddGdbservers (ZipFile apk, ArchiveFileList files, string supportedAbis, AndroidDebugServer debugServer)
		{
			if (string.IsNullOrEmpty (AndroidNdkDirectory))
				return;

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
				var debugServerPath = GdbPaths.GetDebugServerPath (debugServer, arch, AndroidNdkDirectory, SdkBinDirectory);
				if (!File.Exists (debugServerPath))
					continue;
				Log.LogDebugMessage ("Adding {0} debug server '{1}' to the APK as '{2}'", sabi, debugServerPath, entryName);
				apk.AddEntry (entryName, File.OpenRead (debugServerPath));
			}
		}
	}
}
