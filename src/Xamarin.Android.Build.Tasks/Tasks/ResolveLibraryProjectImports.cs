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

namespace Xamarin.Android.Tasks
{
	public class ResolveLibraryProjectImports : AndroidTask
	{
		public override string TaskPrefix => "RLP";

		internal const string AndroidSkipResourceExtraction = "AndroidSkipResourceExtraction";

		[Required]
		public string ImportsDirectory { get; set; } = "";

		[Required]
		public string NativeImportsDirectory { get; set; } = "";

		[Required]
		public string OutputImportDirectory { get; set; } = "";

		[Required]
		public ITaskItem[] Assemblies { get; set; } = [];

		[Required]
		public string AssemblyIdentityMapFile { get; set; } = "";

		public string? CacheFile { get; set; }

		[Output]
		public ITaskItem []? Jars { get; set; }

		[Output]
		public ITaskItem []? ResolvedAssetDirectories { get; set; }

		[Output]
		public ITaskItem []? ResolvedResourceDirectories { get; set; }

		[Output]
		public ITaskItem []? ResolvedResourceDirectoryStamps { get; set; }

		[Output]
		public ITaskItem []? ExtractedDirectories { get; set; }

		internal const string OriginalFile = "OriginalFile";
		internal const string AndroidSkipResourceProcessing = "AndroidSkipResourceProcessing";

		internal const string NuGetPackageVersion = "NuGetPackageVersion";

		internal const string NuGetPackageId = "NuGetPackageId";

		internal static readonly string [] KnownMetadata = new [] {
			OriginalFile,
			AndroidSkipResourceProcessing,
			NuGetPackageId,
			NuGetPackageVersion,
		};

		AssemblyIdentityMap assemblyMap = new AssemblyIdentityMap();

		public ResolveLibraryProjectImports ()
		{
		}

		// Extracts Android library content embedded in assemblies built by "classic" Xamarin.Android
		// under e.g. obj/Debug/lp/[id]/[jl/*.jar | jl/res/*/* | nl/*/*.so]. .aar files are extracted
		// by the _AndroidExtractAarLibraries target in Xamarin.Android.EmbeddedResource.targets.
		public override bool RunTask ()
		{
			var jars                          = new Dictionary<string, ITaskItem> ();
			var resolvedResourceDirectories   = new List<ITaskItem> ();
			var resolvedAssetDirectories      = new List<ITaskItem> ();
			var extractedDirectories          = new List<ITaskItem> ();

			assemblyMap.Load (AssemblyIdentityMapFile);
			try {
				Extract (jars, resolvedResourceDirectories, resolvedAssetDirectories, extractedDirectories);
			} catch (InvalidDataException ex) {
				Log.LogCodedError ("XA1004", ex.Message);
				Log.LogDebugMessage (ex.ToString ());
			}

			Jars                        = jars.Values.ToArray ();
			ResolvedResourceDirectories = resolvedResourceDirectories.ToArray ();
			ResolvedAssetDirectories    = resolvedAssetDirectories.ToArray ();
			ExtractedDirectories        = extractedDirectories.ToArray ();

			ResolvedResourceDirectoryStamps = ResolvedResourceDirectories
				.Select (s => new TaskItem (Path.GetFullPath (Path.Combine (s.ItemSpec, "../..")) + ".stamp"))
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
							Jars.Select(e => new XElement ("Jar", e))),
						new XElement ("ResolvedResourceDirectories",
							ResolvedResourceDirectories.ToXElements ("ResolvedResourceDirectory", KnownMetadata)),
						new XElement ("ResolvedAssetDirectories",
							ResolvedAssetDirectories.ToXElements ("ResolvedAssetDirectory", KnownMetadata)),
						new XElement ("ResolvedResourceDirectoryStamps",
							ResolvedResourceDirectoryStamps.ToXElements ("ResolvedResourceDirectoryStamp", KnownMetadata)),
						new XElement ("ExtractedDirectories",
							ExtractedDirectories.ToXElements ("ExtractedDirectory", KnownMetadata))
					));
				document.SaveIfChanged (CacheFile);
			}

			assemblyMap.Save (AssemblyIdentityMapFile);

			return !Log.HasLoggedErrors;
		}

		void Extract (
				IDictionary<string, ITaskItem> jars,
				ICollection<ITaskItem> resolvedResourceDirectories,
				ICollection<ITaskItem> resolvedAssetDirectories,
				ICollection<ITaskItem> extractedDirectories)
		{
			var outdir = Path.GetFullPath (OutputImportDirectory);
			Directory.CreateDirectory (outdir);

			bool skip;
			foreach (var assemblyItem in Assemblies) {
				var assemblyPath = assemblyItem.ItemSpec;
				var fileName = Path.GetFileName (assemblyPath);
				if (MonoAndroidHelper.IsFrameworkAssembly (fileName)) {
					Log.LogDebugMessage ($"Skipping framework assembly '{fileName}'.");
					continue;
				}
				if (!File.Exists (assemblyPath)) {
					Log.LogDebugMessage ($"Skipping non-existent dependency '{assemblyPath}'.");
					continue;
				}
				if (bool.TryParse (assemblyItem.GetMetadata (AndroidSkipResourceExtraction), out skip) && skip) {
					Log.LogDebugMessage ("Skipping resource extraction for '{0}' .", assemblyPath);
					continue;
				}
				string assemblyIdentName = assemblyMap.GetLibraryImportDirectoryNameForAssembly (fileName);
				string outDirForDll = Path.Combine (OutputImportDirectory, assemblyIdentName);
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
				string assemblyHash = Files.HashFile (assemblyPath);
				string stamp = Path.Combine (outdir, assemblyIdentName + ".stamp");
				string? stampHash = File.Exists (stamp) ? File.ReadAllText (stamp) : null;
				if (assemblyHash == stampHash) {
					Log.LogDebugMessage ("Skipped resource lookup for {0}: extracted files are up to date", assemblyPath);
					if (Directory.Exists (importsDir) || Directory.Exists (nativeimportsDir))
						LogLegacyEmbeddedContentWarning (assemblyPath, nuGetPackageId, nuGetPackageVersion);
					if (Directory.Exists (importsDir)) {
						foreach (var file in Directory.EnumerateFiles (importsDir, "*.jar", SearchOption.AllDirectories)) {
							AddJar (jars, Path.GetFullPath (file), nuGetPackageId: nuGetPackageId, nuGetPackageVersion: nuGetPackageVersion);
						}
					}
					if (Directory.Exists (resDir)) {
						var taskItem = new TaskItem (Path.GetFullPath (resDir), new Dictionary<string, string> {
							[OriginalFile] = assemblyPath,
							[NuGetPackageId] = nuGetPackageId,
							[NuGetPackageVersion] = nuGetPackageVersion,
						});
						if (bool.TryParse (assemblyItem.GetMetadata (AndroidSkipResourceProcessing), out skip) && skip)
							taskItem.SetMetadata (AndroidSkipResourceProcessing, "True");
						resolvedResourceDirectories.Add (taskItem);
					}
					if (Directory.Exists (assetsDir))
						resolvedAssetDirectories.Add (new TaskItem (Path.GetFullPath (assetsDir), new Dictionary<string, string> {
							[OriginalFile] = assemblyPath,
							[NuGetPackageId] = nuGetPackageId,
							[NuGetPackageVersion] = nuGetPackageVersion,
						}));
					continue;
				}

				Log.LogDebugMessage ($"Refreshing {fileName}");

				bool hasLegacyContent = false;
				using (var pe = new PEReader (File.OpenRead (assemblyPath))) {
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
							hasLegacyContent = true;
							// SAFETY: resource comes from this local reader. The synchronous copy and
							// stream disposal finish before pe's enclosing using exits, also on failure.
							using (var stream = unsafe (pe.GetEmbeddedResourceStream (resource))) {
								AddJar (jars, importsDir, name, assemblyPath, nuGetPackageId: nuGetPackageId, nuGetPackageVersion: nuGetPackageVersion);
								updated |= Files.CopyIfStreamChanged (stream, Files.GetArchiveExtractionPath (importsDir, name));
							}
						}
						// embedded native libraries
						else if (name == "__AndroidNativeLibraries__.zip") {
							hasLegacyContent = true;
							List<string> files = new List<string> ();
							// SAFETY: resource comes from this local reader. Extraction and both nested
							// disposals complete before pe is disposed, including exceptions and early returns.
							using (var stream = unsafe (pe.GetEmbeddedResourceStream (resource)))
							using (var zip = new ZipArchive (stream, ZipArchiveMode.Read)) {
								try {
									updated |= Files.ExtractAll (zip, nativeimportsDir, modifyCallback: (entryFullName) => {
										files.Add (Path.GetFullPath (Path.Combine (nativeimportsDir, entryFullName)));
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
							hasLegacyContent = true;
							// temporarily extracted directory will look like:
							//    __library_projects__/[dllname]/[library_project_imports | jlibs]/bin
							// SAFETY: resource comes from this local reader. Extraction and both nested
							// disposals complete before pe is disposed, including exceptions and early returns.
							using (var stream = unsafe (pe.GetEmbeddedResourceStream (resource)))
							using (var zip = new ZipArchive (stream, ZipArchiveMode.Read)) {
								try {
									updated |= Files.ExtractAll (zip, importsDir, modifyCallback: (entryFullName) => {
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
							if (Directory.Exists (resDir)) {
								var taskItem = new TaskItem (Path.GetFullPath (resDir), new Dictionary<string, string> {
									[OriginalFile] = assemblyPath,
									[NuGetPackageId] = nuGetPackageId,
									[NuGetPackageVersion] = nuGetPackageVersion,
								});
								if (bool.TryParse (assemblyItem.GetMetadata (AndroidSkipResourceProcessing), out skip) && skip)
									taskItem.SetMetadata (AndroidSkipResourceProcessing, "True");
								resolvedResourceDirectories.Add (taskItem);
							}
							if (Directory.Exists (assetsDir)) {
								resolvedAssetDirectories.Add (new TaskItem (Path.GetFullPath (assetsDir), new Dictionary<string, string> {
									[OriginalFile] = assemblyPath,
									[NuGetPackageId] = nuGetPackageId,
									[NuGetPackageVersion] = nuGetPackageVersion,
								}));
							}
						}
					}
				}

				if (hasLegacyContent)
					LogLegacyEmbeddedContentWarning (assemblyPath, nuGetPackageId, nuGetPackageVersion);

				if (Directory.Exists (importsDir)) {
					// Delete unknown jars in the top directory of importsDir
					foreach (var file in Directory.EnumerateFiles (importsDir, "*")) {
						var fullPath = Path.GetFullPath (file);
						if (file.EndsWith (".jar", StringComparison.OrdinalIgnoreCase) && !jars.ContainsKey (fullPath)) {
							Log.LogDebugMessage ($"Deleting unknown jar: {Path.GetFileName (file)}");
							File.Delete (fullPath);
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
		}

		void LogLegacyEmbeddedContentWarning (string assemblyPath, string nuGetPackageId, string nuGetPackageVersion)
		{
			if (nuGetPackageId.IsNullOrEmpty ()) {
				Log.LogCodedWarning ("XA0150", Properties.Resources.XA0150_Assembly, assemblyPath);
			} else {
				Log.LogCodedWarning ("XA0150", Properties.Resources.XA0150, nuGetPackageId, nuGetPackageVersion, assemblyPath);
			}
		}

		static void AddJar (IDictionary<string, ITaskItem> jars, string destination, string path, string? originalFile = null, string? nuGetPackageId = null, string? nuGetPackageVersion = null)
		{
			var fullPath = Files.GetArchiveExtractionPath (destination, path);
			AddJar (jars, fullPath, originalFile: originalFile, nuGetPackageId: nuGetPackageId, nuGetPackageVersion: nuGetPackageVersion);
		}

		static void AddJar (IDictionary<string, ITaskItem> jars, string fullPath, string? originalFile = null, string? nuGetPackageId = null, string? nuGetPackageVersion = null)
		{
			if (!jars.ContainsKey (fullPath)) {
				jars.Add (fullPath, new TaskItem (fullPath, new Dictionary<string, string?> {
					[OriginalFile] = originalFile,
					[NuGetPackageId] = nuGetPackageId,
					[NuGetPackageVersion] = nuGetPackageVersion,
				}));
			}
		}

		void WriteAllText (string path, string contents, bool preserveTimestamp)
		{
			if (preserveTimestamp && File.Exists (path)) {
				var timestamp = File.GetLastWriteTimeUtc (path);
				File.WriteAllText (path, contents);
				File.SetLastWriteTimeUtc (path, timestamp);
			} else {
				File.WriteAllText (path, contents);
			}
		}
	}
}
