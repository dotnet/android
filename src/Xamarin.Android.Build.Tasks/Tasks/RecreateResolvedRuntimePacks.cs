using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace Xamarin.Android.Tasks;

public class RecreateResolvedRuntimePacks : AndroidTask
{
	public override string TaskPrefix => "RRRP";

	static readonly string[] RuntimeLibraries = [
		"libmono-android.debug.so",
		"libmono-android.release.so",
		"libnet-android.debug.so",
		"libnet-android.release.so",
	];

	static readonly string[] LibraryNamesToIgnore = [
		"libarchive-dso-stub.so",
		"libc.so",
		"libdl.so",
		"liblog.so",
		"libm.so",
		"libz.so",
	];

	[Required]
	public ITaskItem[] ResolvedNativeLibraries { get; set; }

	[Output]
	public ITaskItem[] ResolvedRuntimePacks { get; set; }

	// Contains items for libraries which are to be removed from @(_ResolvedNativeLibraries), so that
	// they aren't copied to the AAB/APK.  There's a number of such libraries in our runtime packs, they
	// are used only at build time and mustn't be included in the application archives.
	[Output]
	public ITaskItem[] SharedLibrariesToIgnore { get; set; }

	public override bool RunTask ()
	{
		var ignoreLibNames = new List<string> ();
		foreach (string libName in LibraryNamesToIgnore) {
			ignoreLibNames.Add (String.Format ("{0}native{0}{1}", Path.DirectorySeparatorChar, libName));
		}

		// We need to find `libc.so` that comes from one of our runtime packs
		var libcPath = String.Format ("{0}native{0}libc.so", Path.DirectorySeparatorChar);
		var runtimePacks = new Dictionary <string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
		var maybeIgnoreLibs = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
		var runtimePackPaths = new List<string> ();

		foreach (ITaskItem library in ResolvedNativeLibraries) {
			foreach (string libPathTail in ignoreLibNames) {
				if (!library.ItemSpec.EndsWith (libPathTail, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}

				string libraryRid = library.GetMetadata ("RuntimeIdentifier") ?? String.Empty;
				maybeIgnoreLibs[$"{libraryRid}{libPathTail}"] = library;
				break;
			}

			if (!library.ItemSpec.EndsWith (libcPath, StringComparison.OrdinalIgnoreCase)) {
				continue;
			}

			if (!GetMetadata (library, "RuntimeIdentifier", out string? rid) || runtimePacks.ContainsKey (rid)) {
				continue;
			}

			if (!GetMetadata (library, "NuGetPackageId", out string? nugetPackageId)) {
				continue;
			}

			if (!GetMetadata (library, "NuGetPackageVersion", out string? nugetPackageVersion)) {
				continue;
			}

			string tail = String.Format ("{0}runtimes{0}{1}{2}", Path.DirectorySeparatorChar, rid, libcPath);
			int tailIndex = library.ItemSpec.IndexOf (tail);
			if (tailIndex < 0) {
				continue;
			}
			string packageDir = library.ItemSpec.Substring (0, tailIndex);

			// Double-check that this is in fact our runtime pack.  This is needed to avoid the (however improbable)
			// situation where the application references a nuget which comes with `libc.so` and we mistakenly identify
			// it to be our runtime pack.

			// Archive DSO stub must always exist
			if (!PackNativeFileExists (packageDir, rid, DSOWrapperGenerator.StubFileName)) {
				Log.LogDebugMessage ($"Runtime pack '{packageDir}' doesn't contain '{DSOWrapperGenerator.StubFileName}'. Pack ignored.");
				continue;
			}

			// Either one of the runtime libraries must exist
			bool runtimeLibraryFound = false;
			foreach (string runtimeLibrary in RuntimeLibraries) {
				if (PackNativeFileExists (packageDir, rid, runtimeLibrary)) {
					runtimeLibraryFound = true;
					continue;
				}
				Log.LogDebugMessage ($"Runtime library '{runtimeLibrary}' not found in pack '{packageDir}'");
			}
			if (!runtimeLibraryFound) {
				Log.LogDebugMessage ($"Runtime pack '{packageDir}' doesn't contain any runtime shared libraries.  Pack ignored.");
				continue;
			}

			var pack = new TaskItem (nugetPackageId);
			pack.SetMetadata ("FrameworkName", "Microsoft.Android");
			pack.SetMetadata ("NuGetPackageId", nugetPackageId);
			pack.SetMetadata ("NuGetPackageVersion", nugetPackageVersion);
			pack.SetMetadata ("RuntimeIdentifier", rid);
			pack.SetMetadata ("PackageDirectory", packageDir);

			runtimePackPaths.Add (packageDir);
			runtimePacks.Add (rid, pack);
		}
		ResolvedRuntimePacks = runtimePacks.Values.ToArray ();

		var librariesToIgnore = new List<ITaskItem> ();
		foreach (string path in runtimePackPaths) {
			string runtimePackPath = path;
			if (path[path.Length - 1] != Path.DirectorySeparatorChar) {
				runtimePackPath = $"{path}{Path.DirectorySeparatorChar}";
			}

			Log.LogDebugMessage ($"Runtime pack path: {runtimePackPath}");
			foreach (ITaskItem library in maybeIgnoreLibs.Values) {
				if (library.ItemSpec.StartsWith (runtimePackPath)) {
					librariesToIgnore.Add (library);
					Log.LogDebugMessage ($"  ignore library: {library}");
				}
			}
		};
		SharedLibrariesToIgnore = librariesToIgnore.ToArray ();

		return !Log.HasLoggedErrors;

		bool GetMetadata (ITaskItem item, string metadataName, out string? metadataValue)
		{
			metadataValue = item.GetMetadata (metadataName);
			return !String.IsNullOrEmpty (metadataValue);
		}

		bool PackNativeFileExists (string packageDir, string rid, string fileName)
		{
			string packFilePath = Path.Combine (packageDir, "runtimes", rid, "native", fileName);
			return File.Exists (packFilePath);
		}
	}
}
