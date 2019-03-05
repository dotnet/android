using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Mono.Cecil;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Xamarin.Tools.Zip;

using Java.Interop.Tools.Cecil;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class ResolveLibraryProjectImports : Task
	{
		[Required]
		public string ImportsDirectory { get; set; }

		[Required]
		public string NativeImportsDirectory { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public string OutputImportDirectory { get; set; }

		[Required]
		public ITaskItem[] Assemblies { get; set; }

		public ITaskItem [] AarLibraries { get; set; }

		[Required]
		public bool UseShortFileNames { get; set; }

		[Required]
		public string AssemblyIdentityMapFile { get; set; }

		public string CacheFile { get; set; }

		public string [] AssembliesToSkipCases { get; set; }

		[Required]
		public bool DesignTimeBuild { get; set; }

		[Output]
		public string [] Jars { get; set; }
		
		[Output]
		public string [] ResolvedAssetDirectories { get; set; }

		[Output]
		public ITaskItem [] ResolvedResourceDirectories { get; set; }

		[Output]
		public string [] ResolvedEnvironmentFiles { get; set; }

		[Output]
		public ITaskItem [] ResolvedResourceDirectoryStamps { get; set; }

		internal const string OriginalFile = "OriginalFile";
		internal const string SkipAndroidResourceProcessing = "SkipAndroidResourceProcessing";
		static readonly string [] knownMetadata = new [] {
			OriginalFile,
			SkipAndroidResourceProcessing
		};

		AssemblyIdentityMap assemblyMap = new AssemblyIdentityMap();
		HashSet<string> assembliesToSkip;

		public ResolveLibraryProjectImports ()
		{
		}

		// Extracts library project contents under e.g. obj/Debug/[__library_projects__/*.jar | res/*/*]
		// Extracts library project contents under e.g. obj/Debug/[lp/*.jar | res/*/*]
		public override bool Execute ()
		{
			var jars                          = new List<string> ();
			var resolvedResourceDirectories   = new List<ITaskItem> ();
			var resolvedAssetDirectories      = new List<string> ();
			var resolvedEnvironmentFiles      = new List<string> ();

			assemblyMap.Load (AssemblyIdentityMapFile);
			assembliesToSkip = new HashSet<string> (AssembliesToSkipCases ?? new string [0], StringComparer.OrdinalIgnoreCase);

			using (var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: false)) {
				try {
					Extract (resolver, jars, resolvedResourceDirectories, resolvedAssetDirectories, resolvedEnvironmentFiles);
				} catch (ZipIOException ex) {
					Log.LogCodedError ("XA1004", ex.Message);
					Log.LogDebugMessage (ex.ToString ());
				}
			}

			Jars                        = jars.ToArray ();
			ResolvedResourceDirectories = resolvedResourceDirectories.ToArray ();
			ResolvedAssetDirectories    = resolvedAssetDirectories.ToArray ();
			ResolvedEnvironmentFiles    = resolvedEnvironmentFiles.ToArray ();

			ResolvedResourceDirectoryStamps = ResolvedResourceDirectories
				.Select (s => new TaskItem (Path.GetFullPath (Path.Combine (s.ItemSpec, "../..")) + ".stamp"))
				.ToArray ();

			foreach (var directory in ResolvedResourceDirectories) {
				MonoAndroidHelper.SetDirectoryWriteable (directory.ItemSpec);
			}

			foreach (var directory in ResolvedAssetDirectories) {
				MonoAndroidHelper.SetDirectoryWriteable (directory);
			}

			if (!string.IsNullOrEmpty (CacheFile)) {
				var document = new XDocument (
					new XDeclaration ("1.0", "UTF-8", null),
					new XElement ("Paths",
						new XElement ("Jars",
							Jars.Select(e => new XElement ("Jar", e))),
						new XElement ("ResolvedResourceDirectories",
							ResolvedResourceDirectories.Select(dir => {
								var e = new XElement ("ResolvedResourceDirectory", dir.ItemSpec);
								foreach (var name in knownMetadata) {
									var value = dir.GetMetadata (name);
									if (!string.IsNullOrEmpty (value))
										e.SetAttributeValue (name, value);
								}
								return e;
							})),
						new XElement ("ResolvedAssetDirectories", 
							ResolvedAssetDirectories.Select(e => new XElement ("ResolvedAssetDirectory", e))),
						new XElement ("ResolvedEnvironmentFiles", 
							ResolvedEnvironmentFiles.Select(e => new XElement ("ResolvedEnvironmentFile", e))),
						new XElement ("ResolvedResourceDirectoryStamps",
							ResolvedResourceDirectoryStamps.Select(e => new XElement ("ResolvedResourceDirectoryStamp", e)))
					));
				document.SaveIfChanged (CacheFile);
			}

			assemblyMap.Save (AssemblyIdentityMapFile);

			Log.LogDebugTaskItems ("  Jars: ", Jars);
			Log.LogDebugTaskItems ("  ResolvedResourceDirectories: ", ResolvedResourceDirectories);
			Log.LogDebugTaskItems ("  ResolvedAssetDirectories: ", ResolvedAssetDirectories);
			Log.LogDebugTaskItems ("  ResolvedEnvironmentFiles: ", ResolvedEnvironmentFiles);
			Log.LogDebugTaskItems ("  ResolvedResourceDirectoryStamps: ", ResolvedResourceDirectoryStamps);

			return !Log.HasLoggedErrors;
		}

		static string GetTargetAssembly (ITaskItem assemblyName)
		{
			var suffix = assemblyName.ItemSpec.EndsWith (".dll") ? String.Empty : ".dll";
			string hintPath = assemblyName.GetMetadata ("HintPath").Replace (Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
			string fileName = assemblyName.ItemSpec + suffix;
			if (!String.IsNullOrEmpty (hintPath) && !File.Exists (hintPath))
				hintPath = null;
			string assemblyPath = String.IsNullOrEmpty (hintPath) ? fileName : hintPath;
			string fileNameOnly = Path.GetFileName (fileName);
			if (MonoAndroidHelper.IsFrameworkAssembly (fileName) &&
					!MonoAndroidHelper.FrameworkEmbeddedJarLookupTargets.Contains (fileNameOnly) &&
					!MonoAndroidHelper.FrameworkEmbeddedNativeLibraryAssemblies.Contains (fileNameOnly)) {
				return null;
			}
			return Path.GetFullPath (assemblyPath);
		}

		// Extracts library project contents under e.g. obj/Debug/[__library_projects__/*.jar | res/*/*]
		// Extracts library project contents under e.g. obj/Debug/[lp/*.jar | res/*/*]
		void Extract (
				DirectoryAssemblyResolver res,
				ICollection<string> jars,
				ICollection<ITaskItem> resolvedResourceDirectories,
				ICollection<string> resolvedAssetDirectories,
				ICollection<string> resolvedEnvironments)
		{
			// lets "upgrade" the old directory.
			string oldPath = Path.GetFullPath (Path.Combine (OutputImportDirectory, "..", "__library_projects__"));
			if (!OutputImportDirectory.Contains ("__library_projects__") && Directory.Exists (oldPath)) {
				MonoAndroidHelper.SetDirectoryWriteable (Path.Combine (oldPath, ".."));
				Directory.Delete (oldPath, recursive: true);
			}
			var outdir = Path.GetFullPath (OutputImportDirectory);
			Directory.CreateDirectory (outdir);

			foreach (var assembly in Assemblies)
				res.Load (assembly.ItemSpec);

			// FIXME: reorder references by import priority (not sure how to do that yet)
			foreach (var assemblyPath in Assemblies
					.Select (a => GetTargetAssembly (a))
					.Where (a => a != null)
					.Distinct ()) {
				if (DesignTimeBuild && !File.Exists (assemblyPath)) {
					Log.LogDebugMessage ("Skipping non existant dependancy '{0}' due to design time build.", assemblyPath);
					continue;
				}
				string assemblyFileName = Path.GetFileNameWithoutExtension (assemblyPath);
				string assemblyIdentName = assemblyFileName;
				if (UseShortFileNames) {
					assemblyIdentName = assemblyMap.GetLibraryImportDirectoryNameForAssembly (assemblyFileName);
				}
				string outDirForDll = Path.Combine (OutputImportDirectory, assemblyIdentName);
				string importsDir = Path.Combine (outDirForDll, ImportsDirectory);
				string nativeimportsDir = Path.Combine (outDirForDll, NativeImportsDirectory);
#if SEPARATE_CRUNCH
				// FIXME: review these binResDir thing and enable this. Eclipse does this.
				// Enabling these blindly causes build failure on ActionBarSherlock.
				//string binResDir = Path.Combine (importsDir, "bin", "res");
				//string binAssemblyDir = Path.Combine (importsDir, "bin", "assets");
#endif
				string resDir = Path.Combine (importsDir, "res");
				string assetsDir = Path.Combine (importsDir, "assets");

				// Skip already-extracted resources.
				bool updated = false;
				string assemblyHash = MonoAndroidHelper.HashFile (assemblyPath);
				string stamp = Path.Combine (outdir, assemblyIdentName + ".stamp");
				string stampHash = File.Exists (stamp) ? File.ReadAllText (stamp) : null;
				if (assemblyHash == stampHash) {
					Log.LogDebugMessage ("Skipped resource lookup for {0}: extracted files are up to date", assemblyPath);
#if SEPARATE_CRUNCH
					// FIXME: review these binResDir/binAssemblyDir thing and enable this. Eclipse does this.
					// Enabling these blindly causes build failure on ActionBarSherlock.
					if (Directory.Exists (binResDir))
						resolvedResourceDirectories.Add (binResDir);
					if (Directory.Exists (binAssemblyDir))
						resolvedAssetDirectories.Add (binAssemblyDir);
#endif
					if (Directory.Exists (resDir)) {
						var taskItem = new TaskItem (Path.GetFullPath (resDir), new Dictionary<string, string> {
							{ OriginalFile, assemblyPath },
						});
						if (assembliesToSkip.Contains (assemblyFileName))
							taskItem.SetMetadata (SkipAndroidResourceProcessing, "True");
						resolvedResourceDirectories.Add (taskItem);
					}
					if (Directory.Exists (assetsDir))
						resolvedAssetDirectories.Add (Path.GetFullPath (assetsDir));
					foreach (var env in Directory.EnumerateFiles (outDirForDll, "__AndroidEnvironment__*", SearchOption.TopDirectoryOnly)) {
						resolvedEnvironments.Add (env);
					}
					continue;
				}

				Log.LogDebugMessage ($"Refreshing {assemblyFileName}.dll");

				var assembly = res.GetAssembly (assemblyPath);
				foreach (var mod in assembly.Modules) {
					// android environment files
					foreach (var envtxt in mod.Resources
							.Where (r => r.Name.StartsWith ("__AndroidEnvironment__", StringComparison.OrdinalIgnoreCase))
							.Where (r => r is EmbeddedResource)
							.Cast<EmbeddedResource> ()) {
						var outFile = Path.Combine (outDirForDll, envtxt.Name);
						using (var stream = envtxt.GetResourceStream ()) {
							updated |= MonoAndroidHelper.CopyIfStreamChanged (stream, outFile);
						}
						resolvedEnvironments.Add (Path.GetFullPath (outFile));
					}

					// embedded jars (EmbeddedJar, EmbeddedReferenceJar)
					var resjars = mod.Resources
						.Where (r => r.Name.EndsWith (".jar", StringComparison.InvariantCultureIgnoreCase))
						.Select (r => (EmbeddedResource) r);
					foreach (var resjar in resjars) {
						using (var stream = resjar.GetResourceStream ()) {
							AddJar (jars, importsDir, resjar.Name);
							updated |= MonoAndroidHelper.CopyIfStreamChanged (stream, Path.Combine (importsDir, resjar.Name));
						}
					}

					var libzip = mod.Resources.FirstOrDefault (r => r.Name == "__AndroidNativeLibraries__.zip") as EmbeddedResource;
					if (libzip != null) {
						List<string> files = new List<string> ();
						using (var stream = libzip.GetResourceStream ())
						using (var zip = Xamarin.Tools.Zip.ZipArchive.Open (stream)) {
							try {
								updated |= Files.ExtractAll (zip, nativeimportsDir, modifyCallback: (entryFullName) => {
									files.Add (Path.GetFullPath (Path.Combine (nativeimportsDir, entryFullName)));
									return entryFullName
										.Replace ("native_library_imports\\", "")
										.Replace ("native_library_imports/", "");
								}, deleteCallback: (fileToDelete) => {
									return !files.Contains (fileToDelete);
								});
							} catch (PathTooLongException ex) {
								Log.LogCodedError ("XA4303", $"Error extracting resources from \"{assemblyPath}\": {ex}");
								return;
							} catch (NotSupportedException ex) {
								Log.LogCodedError ("XA4303", $"Error extracting resources from \"{assemblyPath}\": {ex}");
								return;
							}
						}
					}

					// embedded AndroidResourceLibrary archive
					var reszip = mod.Resources.FirstOrDefault (r => r.Name == "__AndroidLibraryProjects__.zip") as EmbeddedResource;
					if (reszip != null) {
						// temporarily extracted directory will look like:
						//    __library_projects__/[dllname]/[library_project_imports | jlibs]/bin
						using (var stream = reszip.GetResourceStream ())
						using (var zip = Xamarin.Tools.Zip.ZipArchive.Open (stream)) {
							try {
								updated |= Files.ExtractAll (zip, importsDir, modifyCallback: (entryFullName) => {
									var path = entryFullName
										.Replace ("library_project_imports\\","")
										.Replace ("library_project_imports/", "");
									if (path.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) {
										AddJar (jars, importsDir, path);
									}
									return path;
								}, deleteCallback: (fileToDelete) => {
									return !jars.Contains (fileToDelete);
								});
							} catch (PathTooLongException ex) {
								Log.LogCodedError ("XA4303", $"Error extracting resources from \"{assemblyPath}\": {ex}");
								return;
							} catch (NotSupportedException ex) {
								Log.LogCodedError ("XA4303", $"Error extracting resources from \"{assemblyPath}\": {ex}");
								return;
							}
						}

						// We used to *copy* the resources to overwrite other resources,
						// which resulted in missing resource issue.
						// Here we replaced copy with use of '-S' option and made it to work.
#if SEPARATE_CRUNCH
						// FIXME: review these binResDir/binAssemblyDir thing and enable this. Eclipse does this.
						// Enabling these blindly causes build failure on ActionBarSherlock.
						if (Directory.Exists (binResDir))
							resolvedResourceDirectories.Add (binResDir);
						if (Directory.Exists (binAssemblyDir))
							resolvedAssetDirectories.Add (binAssemblyDir);
#endif
						if (Directory.Exists (resDir)) {
							var taskItem = new TaskItem (Path.GetFullPath (resDir), new Dictionary<string, string> {
								{ OriginalFile, assemblyPath }
							});
							if (assembliesToSkip.Contains (assemblyFileName))
								taskItem.SetMetadata (SkipAndroidResourceProcessing, "True");
							resolvedResourceDirectories.Add (taskItem);
						}
						if (Directory.Exists (assetsDir))
							resolvedAssetDirectories.Add (Path.GetFullPath (assetsDir));
					}
				}

				if (Directory.Exists (importsDir) && assemblyHash != stampHash) {
					Log.LogDebugMessage ($"Saving hash to {stamp}, changes: {updated}");
					//NOTE: if the hash is different we always want to write the file, but preserve the timestamp if no changes
					WriteAllText (stamp, assemblyHash, preserveTimestamp: !updated);
				}
			}
			foreach (var aarFile in AarLibraries ?? new ITaskItem[0]) {
				if (!File.Exists (aarFile.ItemSpec))
					continue;
				string aarIdentityName = Path.GetFileNameWithoutExtension (aarFile.ItemSpec);
				if (UseShortFileNames) {
					aarIdentityName = assemblyMap.GetLibraryImportDirectoryNameForAssembly (aarIdentityName);
				}
				string outDirForDll = Path.Combine (OutputImportDirectory, aarIdentityName);
				string importsDir = Path.Combine (outDirForDll, ImportsDirectory);
				string resDir = Path.Combine (importsDir, "res");
				string assetsDir = Path.Combine (importsDir, "assets");

				bool updated = false;
				string aarHash = MonoAndroidHelper.HashFile (aarFile.ItemSpec);
				string stamp = Path.Combine (outdir, Path.GetFileNameWithoutExtension (aarFile.ItemSpec) + ".stamp");
				string stampHash = File.Exists (stamp) ? File.ReadAllText (stamp) : null;
				if (aarHash == stampHash) {
					Log.LogDebugMessage ("Skipped {0}: extracted files are up to date", aarFile.ItemSpec);
					if (Directory.Exists (resDir))
						resolvedResourceDirectories.Add (new TaskItem (Path.GetFullPath (resDir), new Dictionary<string, string> {
							{ OriginalFile, Path.GetFullPath (aarFile.ItemSpec) },
							{ SkipAndroidResourceProcessing, "True" },
						}));
					if (Directory.Exists (assetsDir))
						resolvedAssetDirectories.Add (Path.GetFullPath (assetsDir));
					continue;
				}

				Log.LogDebugMessage ($"Refreshing {aarFile.ItemSpec}");

				// temporarily extracted directory will look like:
				// _lp_/[aarFile]
				using (var zip = MonoAndroidHelper.ReadZipFile (aarFile.ItemSpec)) {
					try {
						updated |= Files.ExtractAll (zip, importsDir, modifyCallback: (entryFullName) => {
							var entryFileName = Path.GetFileName (entryFullName);
							var entryPath = Path.GetDirectoryName (entryFullName);
							if (entryFileName.StartsWith ("internal_impl", StringComparison.InvariantCulture)) {
								var hash = Files.HashString (entryFileName);
								var jar = Path.Combine (entryPath, $"internal_impl-{hash}.jar");
								AddJar (jars, importsDir, jar);
								return jar;
							}
							if (entryFullName.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) {
								AddJar (jars, importsDir, entryFullName);
							}
							return entryFullName;
						}, deleteCallback: (fileToDelete) => {
							return !jars.Contains (fileToDelete);
						});

						if (Directory.Exists (importsDir) && aarHash != stampHash) {
							Log.LogDebugMessage ($"Saving hash to {stamp}, changes: {updated}");
							//NOTE: if the hash is different we always want to write the file, but preserve the timestamp if no changes
							WriteAllText (stamp, aarHash, preserveTimestamp: !updated);
						}
					} catch (PathTooLongException ex) {
						Log.LogErrorFromException (new PathTooLongException ($"Error extracting resources from \"{aarFile.ItemSpec}\"", ex));
					}
				}
				if (Directory.Exists (resDir))
					resolvedResourceDirectories.Add (new TaskItem (Path.GetFullPath (resDir), new Dictionary<string, string> {
						{ OriginalFile, Path.GetFullPath (aarFile.ItemSpec) },
						{ SkipAndroidResourceProcessing, "True" },
					}));
				if (Directory.Exists (assetsDir))
					resolvedAssetDirectories.Add (Path.GetFullPath (assetsDir));
			}
		}

		void AddJar (ICollection<string> jars, string destination, string path)
		{
			var dir = Path.GetFullPath (destination);
			var jar = Path.Combine (dir, path);
			if (!jars.Contains (jar))
				jars.Add (jar);
		}

		void WriteAllText (string path, string contents, bool preserveTimestamp)
		{
			if (preserveTimestamp && File.Exists (path)) {
				var timestamp = File.GetLastWriteTimeUtc (path);
				File.WriteAllText (path, contents);
				MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (path, timestamp, Log);
			} else {
				File.WriteAllText (path, contents);
			}
		}
	}
}
