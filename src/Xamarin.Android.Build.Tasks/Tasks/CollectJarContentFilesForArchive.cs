#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Collects extra files from inside Jar libraries to be added to the final archive.
/// </summary>
public class CollectJarContentFilesForArchive : AndroidTask
{
	const string KotlinProjectStructureMetadata = "META-INF/kotlin-project-structure-metadata.json";

	public override string TaskPrefix => "CJC";

	public string AndroidPackageFormat { get; set; } = "";

	public string [] ExcludeFiles { get; set; } = [];

	public string [] IncludeFiles { get; set; } = [];

	public ITaskItem [] JavaSourceFiles { get; set; } = [];

	public ITaskItem [] JavaLibraries { get; set; } = [];

	public ITaskItem [] LibraryProjectJars { get; set; } = [];

	readonly List<Regex> excludePatterns = new List<Regex> ();

	readonly List<Regex> includePatterns = new List<Regex> ();

	[Output]
	public ITaskItem [] FilesToAddToArchive { get; set; } = [];

	public override bool RunTask ()
	{
		var rootPath = AndroidPackageFormat.Equals ("aab", StringComparison.InvariantCultureIgnoreCase) ? "root/" : "";

		foreach (var pattern in ExcludeFiles) {
			excludePatterns.Add (FileGlobToRegEx (pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
		}

		foreach (var pattern in IncludeFiles) {
			includePatterns.Add (FileGlobToRegEx (pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
		}

		// Grab distinct .jar files from:
		// - JavaSourceFiles
		// - JavaLibraries
		// - LibraryProjectJars
		var jarFiles = (JavaSourceFiles != null) ? JavaSourceFiles.Where (f => f.ItemSpec.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) : null;

		if (jarFiles != null && JavaLibraries != null)
			jarFiles = jarFiles.Concat (JavaLibraries);
		else if (JavaLibraries != null)
			jarFiles = JavaLibraries;

		var libraryProjectJars = MonoAndroidHelper.ExpandFiles (LibraryProjectJars)
			.Where (jar => !MonoAndroidHelper.IsEmbeddedReferenceJar (jar));

		var jarFilePaths = libraryProjectJars.Concat (jarFiles != null ? jarFiles.Select (j => j.ItemSpec) : Enumerable.Empty<string> ());
		jarFilePaths = MonoAndroidHelper.DistinctFilesByContent (jarFilePaths);

		// Find files in the .jar files that match our include patterns to be added to the archive
		var files = new PackageFileListBuilder ();

		foreach (var jarFile in jarFilePaths) {
			using (var stream = File.OpenRead (jarFile))
			using (var jar = ZipArchive.Open (stream)) {
				bool isKotlinMultiplatformMetadataArchive = jar.ContainsEntry (KotlinProjectStructureMetadata);
				foreach (var jarItem in jar) {
					if (jarItem.IsDirectory)
						continue;
					var name = jarItem.FullName;
					if (!PackagingUtils.CheckEntryForPackaging (name)) {
						continue;
					}

					var path = rootPath + name;

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
						if (IsBuildTimeOnlyEntry (name, isKotlinMultiplatformMetadataArchive)) {
							Log.LogDebugMessage ($"Ignoring build-time jar entry '{name}' from '{Path.GetFileName (jarFile)}'.");
							continue;
						}

						foreach (var pattern in excludePatterns) {
							if (pattern.IsMatch (path)) {
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

					// An item's ItemSpec should be unique so use both the jar file name and the entry name
					files.AddItem ($"{jarFile}#{jarItem.FullName}", path, jarItem.FullName);
				}
			}
		}

		FilesToAddToArchive = files.ToArray ();

		return !Log.HasLoggedErrors;
	}

	static bool IsBuildTimeOnlyEntry (string name, bool isKotlinMultiplatformMetadataArchive)
	{
		var normalizedName = name.Replace ('\\', '/');

		if (isKotlinMultiplatformMetadataArchive &&
				(normalizedName.Equals (KotlinProjectStructureMetadata, StringComparison.OrdinalIgnoreCase) ||
				!normalizedName.StartsWith ("META-INF/", StringComparison.OrdinalIgnoreCase))) {
			return true;
		}

		var extension = Path.GetExtension (normalizedName);
		if (extension.Equals (".jar", StringComparison.OrdinalIgnoreCase) ||
				extension.Equals (".knm", StringComparison.OrdinalIgnoreCase)) {
			return true;
		}

		if (normalizedName.Equals ("R.txt", StringComparison.OrdinalIgnoreCase) ||
				normalizedName.Equals ("proguard.txt", StringComparison.OrdinalIgnoreCase)) {
			return true;
		}

		return normalizedName.Equals ("META-INF/com/android/build/gradle/aar-metadata.properties", StringComparison.OrdinalIgnoreCase) ||
			normalizedName.StartsWith ("META-INF/proguard/", StringComparison.OrdinalIgnoreCase) ||
			normalizedName.StartsWith ("META-INF/com.android.tools/proguard/", StringComparison.OrdinalIgnoreCase) ||
			normalizedName.StartsWith ("META-INF/com.android.tools/r8", StringComparison.OrdinalIgnoreCase);
	}

	static Regex FileGlobToRegEx (string fileGlob, RegexOptions options)
	{
		StringBuilder sb = new StringBuilder ();
		foreach (char c in fileGlob) {
			switch (c) {
				case '*':
					sb.Append (".*");
					break;
				case '?':
					sb.Append (".");
					break;
				case '.':
					sb.Append (@"\.");
					break;
				default:
					sb.Append (c);
					break;
			}
		}
		return new Regex (sb.ToString (), options);
	}
}
