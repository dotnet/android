#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks;
using static Xamarin.Android.Tasks.LibraryProjectImportsMetadata;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks
{
	[MSBuildMultiThreadableTask]
	public class ResolveLibraryProjectImports : MultithreadedAndroidTask
	{
		public override string TaskPrefix => "RLP";

		internal const string AndroidSkipResourceExtraction = "AndroidSkipResourceExtraction";

		[Required]
		public string ImportsDirectory { get; set; } = "";

		[Required]
		public string NativeImportsDirectory { get; set; } = "";

		[Required]
		public string OutputDirectory { get; set; } = "";

		[Required]
		public string OutputImportDirectory { get; set; } = "";

		[Required]
		public ITaskItem[] Assemblies { get; set; } = [];

		public ITaskItem []? AarLibraries { get; set; }

		[Required]
		public string AssemblyIdentityMapFile { get; set; } = "";

		public string? CacheFile { get; set; }

		[Required]
		public bool DesignTimeBuild { get; set; }

		public bool AndroidApplication { get; set; }

		[Output]
		public ITaskItem []? Jars { get; set; }

		[Output]
		public ITaskItem []? ResolvedAssetDirectories { get; set; }

		[Output]
		public ITaskItem []? ResolvedResourceDirectories { get; set; }

		[Output]
		public ITaskItem []? ResolvedEnvironmentFiles { get; set; }

		[Output]
		public ITaskItem []? ResolvedResourceDirectoryStamps { get; set; }

		[Output]
		public ITaskItem []? ProguardConfigFiles { get; set; }

		[Output]
		public ITaskItem []? ExtractedDirectories { get; set; }

		AssemblyIdentityMap assemblyMap = new AssemblyIdentityMap ();

		// Extracts library project contents under e.g. obj/Debug/[__library_projects__/*.jar | res/*/*]
		// Extracts library project contents under e.g. obj/Debug/[lp/*.jar | res/*/*]
		public override bool RunTask ()
		{
			var jars                          = new Dictionary<string, ITaskItem> ();
			var resolvedResourceDirectories   = new List<ITaskItem> ();
			var resolvedAssetDirectories      = new List<ITaskItem> ();
			var resolvedEnvironmentFiles      = new List<ITaskItem> ();
			var proguardConfigFiles           = new List<ITaskItem> ();
			var extractedDirectories          = new List<ITaskItem> ();

			assemblyMap.Load (AssemblyIdentityMapFile, TaskEnvironment);
			try {
				Extract (jars, resolvedResourceDirectories, resolvedAssetDirectories, resolvedEnvironmentFiles, proguardConfigFiles, extractedDirectories);
			} catch (InvalidDataException ex) {
				Log.LogCodedError ("XA1004", ex.Message);
				Log.LogDebugMessage (ex.ToString ());
			}

			Jars                        = jars.Values.ToArray ();
			ResolvedResourceDirectories = resolvedResourceDirectories.ToArray ();
			ResolvedAssetDirectories    = resolvedAssetDirectories.ToArray ();
			ResolvedEnvironmentFiles    = resolvedEnvironmentFiles.ToArray ();
			ProguardConfigFiles         = proguardConfigFiles.ToArray ();
			ExtractedDirectories        = extractedDirectories.ToArray ();

			ResolvedResourceDirectoryStamps = ResolvedResourceDirectories
				.Select (s => new TaskItem (GetFullPath (Path.Combine (s.ItemSpec, "../..")) + ".stamp"))
				.ToArray ();

			if (OS.IsWindows) {
				foreach (var directory in ResolvedResourceDirectories) {
					Files.SetDirectoryWriteable (directory.ItemSpec);
				}

				foreach (var directory in ResolvedAssetDirectories) {
					Files.SetDirectoryWriteable (directory.ItemSpec);
				}
			}

			if (!CacheFile.IsNullOrEmpty ()) {
				var document = new XDocument (
					new XDeclaration ("1.0", "UTF-8", null),
					new XElement ("Paths",
						new XElement ("Jars",
							Jars.Select (e => new XElement ("Jar", e))),
						new XElement ("ResolvedResourceDirectories",
							LibraryProjectImportsMetadata.ToXElements (ResolvedResourceDirectories, "ResolvedResourceDirectory")),
						new XElement ("ResolvedAssetDirectories",
							LibraryProjectImportsMetadata.ToXElements (ResolvedAssetDirectories, "ResolvedAssetDirectory")),
						new XElement ("ResolvedEnvironmentFiles",
							LibraryProjectImportsMetadata.ToXElements (ResolvedEnvironmentFiles, "ResolvedEnvironmentFile")),
						new XElement ("ResolvedResourceDirectoryStamps",
							LibraryProjectImportsMetadata.ToXElements (ResolvedResourceDirectoryStamps, "ResolvedResourceDirectoryStamp")),
						new XElement ("ProguardConfigFiles",
							LibraryProjectImportsMetadata.ToXElements (ProguardConfigFiles, "ProguardConfigFile")),
						new XElement ("ExtractedDirectories",
							LibraryProjectImportsMetadata.ToXElements (ExtractedDirectories, "ExtractedDirectory"))
					));
				document.SaveIfChanged (CacheFile, TaskEnvironment);
			}

			assemblyMap.Save (AssemblyIdentityMapFile, TaskEnvironment);

			return !Log.HasLoggedErrors;
		}

		// Extracts library project contents under e.g. obj/Debug/[__library_projects__/*.jar | res/*/*]
		// Extracts library project contents under e.g. obj/Debug/[lp/*.jar | res/*/*]
		void Extract (
				IDictionary<string, ITaskItem> jars,
				ICollection<ITaskItem> resolvedResourceDirectories,
				ICollection<ITaskItem> resolvedAssetDirectories,
				ICollection<ITaskItem> resolvedEnvironments,
				ICollection<ITaskItem> proguardConfigFiles,
				ICollection<ITaskItem> extractedDirectories)
		{
			// lets "upgrade" the old directory.
			string oldPath = GetFullPath (Path.Combine (OutputImportDirectory, "..", "__library_projects__"));
			if (!OutputImportDirectory.Contains ("__library_projects__") && Directory.Exists (TaskEnvironment.GetAbsolutePath (oldPath))) {
				Files.SetDirectoryWriteable (Path.Combine (oldPath, ".."));
				Directory.Delete (TaskEnvironment.GetAbsolutePath (oldPath), recursive: true);
			}
			var outdir = GetFullPath (OutputImportDirectory);
			Directory.CreateDirectory (TaskEnvironment.GetAbsolutePath (outdir));

			bool skip;
			foreach (var assemblyItem in Assemblies) {
				var assemblyPath = assemblyItem.ItemSpec;
				var assemblyFullPath = GetFullPath (assemblyPath);
				var fileName = Path.GetFileName (assemblyPath);
				if (MonoAndroidHelper.IsFrameworkAssembly (fileName)) {
					Log.LogDebugMessage ($"Skipping framework assembly '{fileName}'.");
					continue;
				}
				if (!File.Exists (TaskEnvironment.GetAbsolutePath (assemblyFullPath))) {
					Log.LogDebugMessage ($"Skipping non-existent dependency '{assemblyPath}'.");
					continue;
				}
				if (bool.TryParse (assemblyItem.GetMetadata (AndroidSkipResourceExtraction), out skip) && skip) {
					Log.LogDebugMessage ("Skipping resource extraction for '{0}' .", assemblyPath);
					continue;
				}
				string assemblyIdentName = assemblyMap.GetLibraryImportDirectoryNameForAssembly (fileName);
				string outDirForDll = Path.Combine (outdir, assemblyIdentName);
				string importsDir = Path.Combine (outDirForDll, ImportsDirectory);
				string nativeimportsDir = Path.Combine (outDirForDll, NativeImportsDirectory);
				string resDir = Path.Combine (importsDir, "res");
				string assetsDir = Path.Combine (importsDir, "assets");
				string nuGetPackageId = assemblyItem.GetMetadata (NuGetPackageId) ?? "";
				string nuGetPackageVersion = assemblyItem.GetMetadata (NuGetPackageVersion) ?? "";
				extractedDirectories.Add (new TaskItem (outDirForDll, new Dictionary<string, string> {
					[OriginalFile] = assemblyPath,
					[NuGetPackageId] = nuGetPackageId,
					[NuGetPackageVersion] = nuGetPackageVersion
				}));

				// Skip already-extracted resources.
				bool updated = false;
				string assemblyHash = Files.HashFile (assemblyFullPath);
				string stamp = Path.Combine (outdir, assemblyIdentName + ".stamp");
				string? stampHash = File.Exists (TaskEnvironment.GetAbsolutePath (stamp)) ? File.ReadAllText (TaskEnvironment.GetAbsolutePath (stamp)) : null;
				if (assemblyHash == stampHash) {
					Log.LogDebugMessage ("Skipped resource lookup for {0}: extracted files are up to date", assemblyPath);
					if (Directory.Exists (TaskEnvironment.GetAbsolutePath (importsDir))) {
						foreach (var file in Directory.EnumerateFiles (TaskEnvironment.GetAbsolutePath (importsDir), "*.jar", SearchOption.AllDirectories)) {
							AddJar (jars, GetFullPath (file), nuGetPackageId: nuGetPackageId, nuGetPackageVersion: nuGetPackageVersion);
						}
					}
					if (Directory.Exists (TaskEnvironment.GetAbsolutePath (resDir))) {
						var taskItem = new TaskItem (GetFullPath (resDir), new Dictionary<string, string> {
							[OriginalFile] = assemblyPath,
							[NuGetPackageId] = nuGetPackageId,
							[NuGetPackageVersion] = nuGetPackageVersion,
						});
						if (bool.TryParse (assemblyItem.GetMetadata (AndroidSkipResourceProcessing), out skip) && skip)
							taskItem.SetMetadata (AndroidSkipResourceProcessing, "True");
						resolvedResourceDirectories.Add (taskItem);
					}
					if (Directory.Exists (TaskEnvironment.GetAbsolutePath (assetsDir)))
						resolvedAssetDirectories.Add (new TaskItem (GetFullPath (assetsDir), new Dictionary<string, string> {
							[OriginalFile] = assemblyPath,
							[NuGetPackageId] = nuGetPackageId,
							[NuGetPackageVersion] = nuGetPackageVersion,
						}));
					continue;
				}

				Log.LogDebugMessage ($"Refreshing {fileName}");

				using (var pe = new PEReader (File.OpenRead (TaskEnvironment.GetAbsolutePath (assemblyFullPath)))) {
					if (!pe.HasMetadata) {
						Log.LogDebugMessage ($"Skipping non-.NET assembly: {assemblyPath}");
						continue;
					}
					var reader = pe.GetMetadataReader ();
					foreach (var handle in reader.ManifestResources) {
						var resource = reader.GetManifestResource (handle);
						string name = reader.GetString (resource.Name);

						// android environment files
						if (name.StartsWith ("__AndroidEnvironment__", StringComparison.OrdinalIgnoreCase)) {
							if (nuGetPackageId.IsNullOrEmpty ()) {
								Log.LogCodedWarning ("XA0149", Properties.Resources.XA0149_Assembly, name, assemblyPath);
							} else {
								Log.LogCodedWarning ("XA0149", Properties.Resources.XA0149, name, nuGetPackageId, nuGetPackageVersion, assemblyPath);
							}
						}
						// embedded jars (EmbeddedJar, EmbeddedReferenceJar)
						else if (name.EndsWith (".jar", StringComparison.InvariantCultureIgnoreCase)) {
							// SAFETY: resource comes from this local reader. The synchronous copy and
							// stream disposal finish before pe's enclosing using exits, also on failure.
							using (var stream = unsafe (pe.GetEmbeddedResourceStream (resource))) {
								AddJar (jars, importsDir, name, assemblyPath, nuGetPackageId: nuGetPackageId, nuGetPackageVersion: nuGetPackageVersion);
								updated |= Files.CopyIfStreamChanged (stream, Files.GetArchiveExtractionPath (importsDir, name), TaskEnvironment);
							}
						}
						// embedded native libraries
						else if (name == "__AndroidNativeLibraries__.zip") {
							List<string> files = new List<string> ();
							// SAFETY: resource comes from this local reader. Extraction and both nested
							// disposals complete before pe is disposed, including exceptions and early returns.
							using (var stream = unsafe (pe.GetEmbeddedResourceStream (resource)))
							using (var zip = new ZipArchive (stream, ZipArchiveMode.Read)) {
								try {
									updated |= Files.ExtractAll (TaskEnvironment, zip, nativeimportsDir, modifyCallback: (entryFullName) => {
										files.Add (GetFullPath (Path.Combine (nativeimportsDir, entryFullName)));
										return entryFullName
											.Replace ("native_library_imports\\", "")
											.Replace ("native_library_imports/", "");
									}, deleteCallback: (fileToDelete) => {
										return !files.Contains (fileToDelete);
									}, log: Log);
								} catch (PathTooLongException ex) {
									Log.LogCodedError ("XA4303", Properties.Resources.XA4303, assemblyPath, ex);
									return;
								} catch (NotSupportedException ex) {
									Log.LogCodedError ("XA4303", Properties.Resources.XA4303, assemblyPath, ex);
									return;
								}
							}
						}
						// embedded AndroidResourceLibrary archive
						else if (name == "__AndroidLibraryProjects__.zip") {
							// temporarily extracted directory will look like:
							//    __library_projects__/[dllname]/[library_project_imports | jlibs]/bin
							// SAFETY: resource comes from this local reader. Extraction and both nested
							// disposals complete before pe is disposed, including exceptions and early returns.
							using (var stream = unsafe (pe.GetEmbeddedResourceStream (resource)))
							using (var zip = new ZipArchive (stream, ZipArchiveMode.Read)) {
								try {
									updated |= Files.ExtractAll (TaskEnvironment, zip, importsDir, modifyCallback: (entryFullName) => {
										var path = entryFullName
											.Replace ("library_project_imports\\", "")
											.Replace ("library_project_imports/", "");
										if (path.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) {
											AddJar (jars, importsDir, path, assemblyPath, nuGetPackageId, nuGetPackageVersion);
										}
										return path;
									}, deleteCallback: (fileToDelete) => {
										return !jars.ContainsKey (fileToDelete);
									}, log: Log);
								} catch (PathTooLongException ex) {
									Log.LogCodedError ("XA4303", Properties.Resources.XA4303, assemblyPath, ex);
									return;
								} catch (NotSupportedException ex) {
									Log.LogCodedError ("XA4303", Properties.Resources.XA4303, assemblyPath, ex);
									return;
								}
							}

							// We used to *copy* the resources to overwrite other resources,
							// which resulted in missing resource issue.
							// Here we replaced copy with use of '-S' option and made it to work.
							if (Directory.Exists (TaskEnvironment.GetAbsolutePath (resDir))) {
								var taskItem = new TaskItem (GetFullPath (resDir), new Dictionary<string, string> {
									[OriginalFile] = assemblyPath,
									[NuGetPackageId] = nuGetPackageId,
									[NuGetPackageVersion] = nuGetPackageVersion,
								});
								if (bool.TryParse (assemblyItem.GetMetadata (AndroidSkipResourceProcessing), out skip) && skip)
									taskItem.SetMetadata (AndroidSkipResourceProcessing, "True");
								resolvedResourceDirectories.Add (taskItem);
							}
							if (Directory.Exists (TaskEnvironment.GetAbsolutePath (assetsDir))) {
								resolvedAssetDirectories.Add (new TaskItem (GetFullPath (assetsDir), new Dictionary<string, string> {
									[OriginalFile] = assemblyPath,
									[NuGetPackageId] = nuGetPackageId,
									[NuGetPackageVersion] = nuGetPackageVersion,
								}));
							}
						}
					}
				}

				if (Directory.Exists (TaskEnvironment.GetAbsolutePath (importsDir))) {
					// Delete unknown files in the top directory of importsDir
					foreach (var file in Directory.EnumerateFiles (TaskEnvironment.GetAbsolutePath (importsDir), "*")) {
						var fullPath = GetFullPath (file);
						if (file.StartsWith ("__AndroidEnvironment__", StringComparison.OrdinalIgnoreCase) && !resolvedEnvironments.Any (x => x.ItemSpec == fullPath)) {
							Log.LogDebugMessage ($"Deleting unknown AndroidEnvironment file: {Path.GetFileName (file)}");
							File.Delete (TaskEnvironment.GetAbsolutePath (fullPath));
							updated = true;
						} else if (file.EndsWith (".jar", StringComparison.OrdinalIgnoreCase) && !jars.ContainsKey (fullPath)) {
							Log.LogDebugMessage ($"Deleting unknown jar: {Path.GetFileName (file)}");
							File.Delete (TaskEnvironment.GetAbsolutePath (fullPath));
							updated = true;
						}
					}
					if (assemblyHash != stampHash) {
						Log.LogDebugMessage ($"Saving hash to {stamp}, changes: {updated}");
						//NOTE: if the hash is different we always want to write the file, but preserve the timestamp if no changes
						WriteAllText (stamp, assemblyHash, preserveTimestamp: !updated);
					}
				}
			}
			foreach (var aarFile in AarLibraries ?? []) {
				var aarFullPath = GetFullPath (aarFile.ItemSpec);
				if (!File.Exists (TaskEnvironment.GetAbsolutePath (aarFullPath)))
					continue;
				string aarIdentityName = Path.GetFileName (aarFile.ItemSpec);
				aarIdentityName = assemblyMap.GetLibraryImportDirectoryNameForAssembly (aarIdentityName);
				string outDirForDll = Path.Combine (outdir, aarIdentityName);
				string importsDir = Path.Combine (outDirForDll, ImportsDirectory);
				string resDir = Path.Combine (importsDir, "res");
				string rTxt = Path.Combine (importsDir, "R.txt");
				string assetsDir = Path.Combine (importsDir, "assets");
				string proguardFile = Path.Combine (importsDir, "proguard.txt");
				string nuGetPackageId = aarFile.GetMetadata (NuGetPackageId) ?? "";
				string nuGetPackageVersion = aarFile.GetMetadata (NuGetPackageVersion) ?? "";
				extractedDirectories.Add (new TaskItem (outDirForDll, new Dictionary<string, string> {
					[OriginalFile] = aarFile.ItemSpec,
					[NuGetPackageId] = nuGetPackageId,
					[NuGetPackageVersion] = nuGetPackageVersion,
				}));

				bool updated = false;
				string aarHash = Files.HashFile (aarFullPath);
				string stamp = Path.Combine (outdir, aarIdentityName + ".stamp");
				string? stampHash = File.Exists (TaskEnvironment.GetAbsolutePath (stamp)) ? File.ReadAllText (TaskEnvironment.GetAbsolutePath (stamp)) : null;
				if (aarHash == stampHash) {
					Log.LogDebugMessage ("Skipped {0}: extracted files are up to date", aarFile.ItemSpec);
					if (Directory.Exists (TaskEnvironment.GetAbsolutePath (importsDir))) {
						foreach (var file in Directory.EnumerateFiles (TaskEnvironment.GetAbsolutePath (importsDir), "*.jar", SearchOption.AllDirectories)) {
							AddJar (jars, GetFullPath (file), nuGetPackageId: nuGetPackageId, nuGetPackageVersion: nuGetPackageVersion);
						}
					}
					if (Directory.Exists (TaskEnvironment.GetAbsolutePath (resDir)) || File.Exists (TaskEnvironment.GetAbsolutePath (rTxt))) {
						var skipProcessing = aarFile.GetMetadata (AndroidSkipResourceProcessing);
						if (skipProcessing.IsNullOrEmpty ()) {
							skipProcessing = "True";
						}
						resolvedResourceDirectories.Add (new TaskItem (GetFullPath (resDir), new Dictionary<string, string> {
							[OriginalFile] = aarFullPath,
							[AndroidSkipResourceProcessing] = skipProcessing,
							[NuGetPackageId] = nuGetPackageId,
							[NuGetPackageVersion] = nuGetPackageVersion,
						}));
					}
					if (Directory.Exists (TaskEnvironment.GetAbsolutePath (assetsDir)))
						resolvedAssetDirectories.Add (new TaskItem  (GetFullPath (assetsDir), new Dictionary<string, string> {
							[OriginalFile] = aarFullPath,
							[NuGetPackageId] = nuGetPackageId,
							[NuGetPackageVersion] = nuGetPackageVersion,
						}));
					if (AndroidApplication && File.Exists (TaskEnvironment.GetAbsolutePath (proguardFile))) {
						proguardConfigFiles.Add (new TaskItem (GetFullPath (proguardFile), new Dictionary<string, string> {
							[OriginalFile] = aarFullPath,
							[NuGetPackageId] = nuGetPackageId,
							[NuGetPackageVersion] = nuGetPackageVersion,
						}));
					}
					continue;
				}

				Log.LogDebugMessage ($"Refreshing {aarFile.ItemSpec}");

				// temporarily extracted directory will look like:
				// _lp_/[aarFile]

				using (var zip = ReadZipFile (aarFullPath)) {
					try {
						updated |= Files.ExtractAll (TaskEnvironment, zip, importsDir, modifyCallback: (entryFullName) => {
							var entryFileName = Path.GetFileName (entryFullName);
							var entryPath = Path.GetDirectoryName (entryFullName) ?? "";
							if (entryFileName.StartsWith ("internal_impl", StringComparison.InvariantCulture)) {
								var hash = Files.HashString (entryFileName);
								var jar = Path.Combine (entryPath, $"internal_impl-{hash}.jar");
								AddJar (jars, importsDir, jar, aarFullPath, nuGetPackageId: nuGetPackageId, nuGetPackageVersion: nuGetPackageVersion);
								return jar;
							}
							if (entryFullName.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) {
								AddJar (jars, importsDir, entryFullName, aarFullPath, nuGetPackageId: nuGetPackageId, nuGetPackageVersion: nuGetPackageVersion);
							} else if (entryFullName.StartsWith (".net/env/", StringComparison.OrdinalIgnoreCase) ||
									entryFullName.StartsWith (".net\\env\\", StringComparison.OrdinalIgnoreCase)) {
								var fullPath = Files.GetArchiveExtractionPath (importsDir, entryFullName);
								resolvedEnvironments.Add (new TaskItem (fullPath, new Dictionary<string, string> {
									[OriginalFile] = aarFile.ItemSpec,
									[NuGetPackageId] = nuGetPackageId,
									[NuGetPackageVersion] = nuGetPackageVersion,
								}));
							}
							return entryFullName;
						}, deleteCallback: (fileToDelete) => {
							return !jars.ContainsKey (fileToDelete);
						}, skipCallback: Files.ShouldSkipEntryInAar, log: Log);

						if (Directory.Exists (TaskEnvironment.GetAbsolutePath (importsDir)) && aarHash != stampHash) {
							Log.LogDebugMessage ($"Saving hash to {stamp}, changes: {updated}");
							//NOTE: if the hash is different we always want to write the file, but preserve the timestamp if no changes
							WriteAllText (stamp, aarHash, preserveTimestamp: !updated);
						}
					} catch (PathTooLongException ex) {
						Log.LogErrorFromException (new PathTooLongException ($"Error extracting resources from \"{aarFile.ItemSpec}\"", ex));
					}
				}
				if (Directory.Exists (TaskEnvironment.GetAbsolutePath (resDir)) || File.Exists (TaskEnvironment.GetAbsolutePath (rTxt))) {
					var skipProcessing = aarFile.GetMetadata (AndroidSkipResourceProcessing);
					if (skipProcessing.IsNullOrEmpty ()) {
						skipProcessing = "True";
					}
					resolvedResourceDirectories.Add (new TaskItem (GetFullPath (resDir), new Dictionary<string, string> {
						[OriginalFile] = aarFullPath,
						[AndroidSkipResourceProcessing] = skipProcessing,
						[NuGetPackageId] = nuGetPackageId,
						[NuGetPackageVersion] = nuGetPackageVersion,
					}));
				}
				if (Directory.Exists (TaskEnvironment.GetAbsolutePath (assetsDir)))
					resolvedAssetDirectories.Add (new TaskItem (GetFullPath (assetsDir), new Dictionary<string, string> {
						[OriginalFile] = aarFullPath,
						[NuGetPackageId] = nuGetPackageId,
						[NuGetPackageVersion] = nuGetPackageVersion,
					}));
				if (AndroidApplication && File.Exists (TaskEnvironment.GetAbsolutePath (proguardFile))) {
					proguardConfigFiles.Add (new TaskItem (GetFullPath (proguardFile), new Dictionary<string, string> {
						[OriginalFile] = aarFullPath,
						[NuGetPackageId] = nuGetPackageId,
						[NuGetPackageVersion] = nuGetPackageVersion,
					}));
				}
			}
		}

		static void AddJar (IDictionary<string, ITaskItem> jars, string destination, string path, string? originalFile = null, string? nuGetPackageId = null, string? nuGetPackageVersion = null)
		{
			var fullPath = Files.GetArchiveExtractionPath (destination, path);
			AddJar (jars, fullPath, originalFile: originalFile, nuGetPackageId: nuGetPackageId, nuGetPackageVersion: nuGetPackageVersion);
		}

		static void AddJar (IDictionary<string, ITaskItem> jars, string fullPath, string? originalFile = null, string? nuGetPackageId = null, string? nuGetPackageVersion = null)
		{
			jars.TryAdd (fullPath, new TaskItem (fullPath, new Dictionary<string, string?> {
				[OriginalFile] = originalFile,
				[NuGetPackageId] = nuGetPackageId,
				[NuGetPackageVersion] = nuGetPackageVersion,
			}));
		}

		string GetFullPath (string path) => TaskEnvironment.GetAbsolutePath (path).GetCanonicalForm ().Value;

		ZipArchive ReadZipFile (string path)
		{
			try {
				return ZipFile.OpenRead (TaskEnvironment.GetAbsolutePath (path));
			} catch (InvalidDataException ex) {
				throw new InvalidDataException ($"There was an error opening {path}. The file is probably corrupt. Try deleting it and building again. {ex.Message}", ex);
			}
		}

		void WriteAllText (string path, string contents, bool preserveTimestamp)
		{
			if (preserveTimestamp && File.Exists (TaskEnvironment.GetAbsolutePath (path))) {
				var timestamp = File.GetLastWriteTimeUtc (TaskEnvironment.GetAbsolutePath (path));
				File.WriteAllText (TaskEnvironment.GetAbsolutePath (path), contents);
				File.SetLastWriteTimeUtc (TaskEnvironment.GetAbsolutePath (path), timestamp);
			} else {
				File.WriteAllText (TaskEnvironment.GetAbsolutePath (path), contents);
			}
		}
	}
}
