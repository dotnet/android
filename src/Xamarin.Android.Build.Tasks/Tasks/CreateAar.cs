#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class CreateAar : AndroidTask
	{
		public override string TaskPrefix => "CAAR";

		public ITaskItem []? AndroidAssets { get; set; }

		public ITaskItem []? AndroidResources { get; set; }

		public ITaskItem []? AndroidEnvironment { get; set; }

		public ITaskItem? AndroidManifest { get; set; }

		public ITaskItem []? JarFiles { get; set; }

		public ITaskItem []? NativeLibraries { get; set; }

		public ITaskItem []? ProguardConfigurationFiles { get; set; }

		[Required]
		public string AssetDirectory { get; set; } = "";

		[Required]
		public string OutputFile { get; set; } = "";

		[Required]
		public string PrefixProperty { get; set; } = "";

		public override bool RunTask ()
		{
			if (Path.IsPathRooted (AssetDirectory)) {
				Log.LogCodedError ("XA1041", message: Properties.Resources.XA1041, PrefixProperty, AssetDirectory);
				return false;
			}
			Directory.CreateDirectory (Path.GetDirectoryName (OutputFile));

			using (var stream = File.Create (OutputFile))
			using (var aar = new ZipArchive (stream, ZipArchiveMode.Update)) {
				var existingEntries = new HashSet<string> (StringComparer.Ordinal);
				foreach (var entry in aar.Entries) {
					Log.LogDebugMessage ("Existing entry: " + entry.FullName);
					existingEntries.Add (entry.FullName);
				}
				if (AndroidAssets != null) {
					foreach (var asset in AndroidAssets) {
						// See: https://github.com/dotnet/android/commit/665cb59205f8ac565b6acbda740624844bc1cbd9
						if (Directory.Exists (asset.ItemSpec)) {
							Log.LogDebugMessage ($"Skipping item, is a directory: {asset.ItemSpec}");
							continue;
						}
						var relative = MonoAndroidHelper.GetRelativePathForAndroidAsset (AssetDirectory, asset);
						var archivePath = "assets/" + relative.Replace ('\\', '/');
						AddFile (aar, asset.ItemSpec, archivePath);
						existingEntries.Remove (archivePath);
					}
				}
				if (AndroidResources != null) {
					var nameCaseMap = new StringBuilder ();
					foreach (var resource in AndroidResources) {
						// See: https://github.com/dotnet/android/commit/665cb59205f8ac565b6acbda740624844bc1cbd9
						if (Directory.Exists (resource.ItemSpec)) {
							Log.LogDebugMessage ($"Skipping item, is a directory: {resource.ItemSpec}");
							continue;
						}
						var directory = Path.GetDirectoryName (resource.ItemSpec);
						var resourcePath = Path.GetFileName (directory) + "/" + Path.GetFileName (resource.ItemSpec);
						var archivePath = "res/" + resourcePath;
						AddFile (aar, resource.ItemSpec, archivePath);
						existingEntries.Remove (archivePath);

						nameCaseMap.Append (resource.GetMetadata ("LogicalName").Replace ('\\', '/'));
						nameCaseMap.Append (';');
						nameCaseMap.AppendLine (resourcePath);
					}
					if (nameCaseMap.Length > 0) {
						var archivePath = ".net/__res_name_case_map.txt";
						AddEntry (aar, archivePath, nameCaseMap.ToString ());
						existingEntries.Remove (archivePath);
					}
				}
				if (AndroidEnvironment != null) {
					foreach (var env in AndroidEnvironment) {
						var archivePath = $".net/env/{GetHashedFileName (env)}.env";
						AddFile (aar, env.ItemSpec, archivePath);
						existingEntries.Remove (archivePath);
					}
				}
				if (JarFiles != null) {
					foreach (var jar in JarFiles) {
						var pack = jar.GetMetadata ("Pack");
						if (string.Equals (pack, "false", StringComparison.OrdinalIgnoreCase)) {
							Log.LogDebugMessage ($"Skipping jar '{jar.ItemSpec}' because Pack='false'");
							continue;
						}
						var archivePath = $"libs/{GetHashedFileName (jar)}.jar";
						AddFile (aar, jar.ItemSpec, archivePath);
						existingEntries.Remove (archivePath);
					}
				}
				if (NativeLibraries != null) {
					foreach (var lib in NativeLibraries) {
						var abi = AndroidRidAbiHelper.GetNativeLibraryAbi (lib);
						if (abi.IsNullOrWhiteSpace ()) {
							Log.LogCodedError ("XA4301", lib.ItemSpec, 0, Properties.Resources.XA4301_ABI, lib.ItemSpec);
							continue;
						}
						var archivePath = "jni/" + abi + "/" + Path.GetFileName (lib.ItemSpec);
						AddFile (aar, lib.ItemSpec, archivePath);
						existingEntries.Remove (archivePath);
					}
				}
				if (ProguardConfigurationFiles != null) {
					var sb = new StringBuilder ();
					foreach (var file in ProguardConfigurationFiles) {
						sb.AppendLine (File.ReadAllText (file.ItemSpec));
					}
					AddEntry (aar, "proguard.txt", sb.ToString ());
				}
				if (AndroidManifest != null && File.Exists (AndroidManifest.ItemSpec)) {
					var manifest = File.ReadAllText (AndroidManifest.ItemSpec);
					var doc = XDocument.Parse(manifest);
					if (!(doc.Element ("manifest")?.Attribute ("package")?.Value).IsNullOrEmpty ()) {
						AddEntry (aar, "AndroidManifest.xml", manifest);
					} else {
						Log.LogDebugMessage ($"Skipping {AndroidManifest.ItemSpec}. The `manifest` does not have a `package` attribute.");
					}
				}
				foreach (var entry in existingEntries) {
					Log.LogDebugMessage ($"Removing {entry} as it is not longer required.");
					aar.GetEntry (entry)?.Delete ();
				}
			}

			// Delete the archive on failure
			if (Log.HasLoggedErrors && File.Exists (OutputFile)) {
				File.Delete (OutputFile);
			}

			return !Log.HasLoggedErrors;
		}

		static void AddFile (ZipArchive archive, string filename, string archivePath)
		{
			archive.GetEntry (archivePath)?.Delete ();
			archive.CreateEntryFromFile (filename, archivePath);
		}

		static void AddEntry (ZipArchive archive, string archivePath, string contents)
		{
			archive.GetEntry (archivePath)?.Delete ();
			var entry = archive.CreateEntry (archivePath);
			using var writer = new StreamWriter (entry.Open (), Files.UTF8withoutBOM);
			writer.Write (contents);
		}

		/// <summary>
		/// Hash the path to an ITaskItem to get a unique file name.
		/// Replaces \ with /, so we get the same hash on all platforms.
		/// </summary>
		static string GetHashedFileName (ITaskItem item) =>
			Files.HashString (item.ItemSpec.Replace ('\\', '/'));
	}
}
