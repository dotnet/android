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
			var outputDirectory = Path.GetDirectoryName (OutputFile);
			if (!outputDirectory.IsNullOrEmpty ())
				Directory.CreateDirectory (outputDirectory);

			var entries = new List<(string Name, string? Filename, string? Contents)> ();
			if (AndroidAssets != null) {
				foreach (var asset in AndroidAssets) {
					// See: https://github.com/dotnet/android/commit/665cb59205f8ac565b6acbda740624844bc1cbd9
					if (Directory.Exists (asset.ItemSpec)) {
						Log.LogDebugMessage ($"Skipping item, is a directory: {asset.ItemSpec}");
						continue;
					}
					var relative = MonoAndroidHelper.GetRelativePathForAndroidAsset (AssetDirectory, asset);
					var archivePath = "assets/" + relative.Replace ('\\', '/');
					AddFile (entries, asset.ItemSpec, archivePath);
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
					AddFile (entries, resource.ItemSpec, archivePath);

					nameCaseMap.Append (resource.GetMetadata ("LogicalName").Replace ('\\', '/'));
					nameCaseMap.Append (';');
					nameCaseMap.AppendLine (resourcePath);
				}
				if (nameCaseMap.Length > 0) {
					var archivePath = ".net/__res_name_case_map.txt";
					AddEntry (entries, archivePath, nameCaseMap.ToString ());
				}
			}
			if (AndroidEnvironment != null) {
				foreach (var env in AndroidEnvironment) {
					var archivePath = $".net/env/{GetHashedFileName (env)}.env";
					AddFile (entries, env.ItemSpec, archivePath);
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
					AddFile (entries, jar.ItemSpec, archivePath);
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
					AddFile (entries, lib.ItemSpec, archivePath);
				}
			}
			if (ProguardConfigurationFiles != null) {
				var sb = new StringBuilder ();
				foreach (var file in ProguardConfigurationFiles) {
					sb.AppendLine (File.ReadAllText (file.ItemSpec));
				}
				AddEntry (entries, "proguard.txt", sb.ToString ());
			}
			if (AndroidManifest != null && File.Exists (AndroidManifest.ItemSpec)) {
				var manifest = File.ReadAllText (AndroidManifest.ItemSpec);
				var doc = XDocument.Parse(manifest);
				if (!(doc.Element ("manifest")?.Attribute ("package")?.Value).IsNullOrEmpty ()) {
					AddEntry (entries, "AndroidManifest.xml", manifest);
				} else {
					Log.LogDebugMessage ($"Skipping {AndroidManifest.ItemSpec}. The `manifest` does not have a `package` attribute.");
				}
			}

			var lastEntry = new Dictionary<string, int> (StringComparer.Ordinal);
			for (var i = 0; i < entries.Count; i++)
				lastEntry [entries [i].Name] = i;
			using (var stream = File.Create (OutputFile))
			using (var aar = new ZipArchive (stream, ZipArchiveMode.Create)) {
				for (var i = 0; i < entries.Count; i++) {
					var entry = entries [i];
					if (lastEntry [entry.Name] != i)
						continue;
					if (entry.Filename is string filename) {
						aar.CreateEntryFromFile (filename, entry.Name);
					} else if (entry.Contents is string contents) {
						using var writer = new StreamWriter (aar.CreateEntry (entry.Name).Open (), Files.UTF8withoutBOM);
						writer.Write (contents);
					} else {
						throw new InvalidOperationException ($"Archive entry '{entry.Name}' has neither a source file nor contents.");
					}
				}
			}

			// Delete the archive on failure
			if (Log.HasLoggedErrors && File.Exists (OutputFile)) {
				File.Delete (OutputFile);
			}

			return !Log.HasLoggedErrors;
		}

		static void AddFile (List<(string Name, string? Filename, string? Contents)> entries, string filename, string archivePath) =>
			entries.Add ((archivePath, filename, null));

		static void AddEntry (List<(string Name, string? Filename, string? Contents)> entries, string archivePath, string contents) =>
			entries.Add ((archivePath, null, contents));

		/// <summary>
		/// Hash the path to an ITaskItem to get a unique file name.
		/// Replaces \ with /, so we get the same hash on all platforms.
		/// </summary>
		static string GetHashedFileName (ITaskItem item) =>
			Files.HashString (item.ItemSpec.Replace ('\\', '/'));
	}
}
