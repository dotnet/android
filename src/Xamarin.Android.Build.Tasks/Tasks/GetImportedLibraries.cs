using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GetImportedLibraries : AndroidTask
	{
		public override string TaskPrefix => "GIL";

		static readonly string [] IgnoredManifestDirectories = new [] {
			"bin",
			"manifest",
			"aapt",
		};

		[Required]
		public ITaskItem[] ExtractedDirectories { get; set; }

		public string CacheFile { get; set;} 

		[Output]
		public ITaskItem [] Jars { get; set; }

		[Output]
		public ITaskItem [] NativeLibraries { get; set; }

		[Output]
		public ITaskItem [] ManifestDocuments { get; set; }

		public override bool RunTask ()
		{
			var manifestDocuments = new List<ITaskItem> ();
			var nativeLibraries   = new List<ITaskItem> ();
			var jarFiles          = new List<ITaskItem> ();
			foreach (var extractedDirectory in ExtractedDirectories) {
				if (!Directory.Exists (extractedDirectory.ItemSpec)) {
					continue;
				}
				string originalFile = extractedDirectory.GetMetadata (ResolveLibraryProjectImports.OriginalFile);
				string nuGetPackageId = extractedDirectory.GetMetadata (ResolveLibraryProjectImports.NuGetPackageId);
				string nuGetPackageVersion = extractedDirectory.GetMetadata (ResolveLibraryProjectImports.NuGetPackageVersion);
				foreach (var file in Directory.EnumerateFiles (extractedDirectory.ItemSpec, "*", SearchOption.AllDirectories)) {
					if (file.EndsWith (".so", StringComparison.OrdinalIgnoreCase)) {
						if (AndroidRidAbiHelper.GetNativeLibraryAbi (file) != null)
							nativeLibraries.Add (new TaskItem (file, new Dictionary<string, string> {
								[ResolveLibraryProjectImports.OriginalFile] = originalFile,
								[ResolveLibraryProjectImports.NuGetPackageId] = nuGetPackageId,
								[ResolveLibraryProjectImports.NuGetPackageVersion] = nuGetPackageVersion,
							}));
						continue;
					}
					if (file.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) {
						jarFiles.Add (new TaskItem (file, new Dictionary<string, string> {
								[ResolveLibraryProjectImports.OriginalFile] = originalFile,
								[ResolveLibraryProjectImports.NuGetPackageId] = nuGetPackageId,
								[ResolveLibraryProjectImports.NuGetPackageVersion] = nuGetPackageVersion,
							}));
						continue;
					}
					if (file.EndsWith (".xml", StringComparison.OrdinalIgnoreCase)) {
						if (Path.GetFileName (file) != "AndroidManifest.xml")
							continue;
						// there could be ./AndroidManifest.xml and bin/AndroidManifest.xml, which will be the same. So, ignore "bin" ones.
						var directory = Path.GetFileName (Path.GetDirectoryName (file));
						if (IgnoredManifestDirectories.Contains (directory))
							continue;
						var doc = XDocument.Load(file);
						if (string.IsNullOrEmpty (doc.Element ("manifest")?.Attribute ("package")?.Value ?? string.Empty)) {
							Log.LogCodedWarning ("XA4315", file, 0, Properties.Resources.XA4315, file);
							continue;
						}
						manifestDocuments.Add (new TaskItem (file, new Dictionary<string, string> {
							[ResolveLibraryProjectImports.OriginalFile] = originalFile,
							[ResolveLibraryProjectImports.NuGetPackageId] = nuGetPackageId,
							[ResolveLibraryProjectImports.NuGetPackageVersion] = nuGetPackageVersion,
						}));
					}
				}
			}

			ManifestDocuments = manifestDocuments.ToArray ();
			NativeLibraries = nativeLibraries.ToArray ();
			Jars = jarFiles.ToArray ();

			if (!string.IsNullOrEmpty (CacheFile)) {
				var document = new XDocument (
							new XDeclaration ("1.0", "UTF-8", null),
							new XElement ("Paths",
									new XElement ("ManifestDocuments", ManifestDocuments.ToXElements ("ManifestDocument", ResolveLibraryProjectImports.KnownMetadata)),
									new XElement ("NativeLibraries", NativeLibraries.ToXElements ("NativeLibrary", ResolveLibraryProjectImports.KnownMetadata)),
									new XElement ("Jars", Jars.ToXElements ("Jar", ResolveLibraryProjectImports.KnownMetadata))
						));
				document.SaveIfChanged (CacheFile);
			}

			Log.LogDebugTaskItems ("  NativeLibraries: ", NativeLibraries);
			Log.LogDebugTaskItems ("  Jars: ", Jars);
			Log.LogDebugTaskItems ("  ManifestDocuments: ", ManifestDocuments);

			return true;
		}
	}


}
