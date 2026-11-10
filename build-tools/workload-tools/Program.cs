using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Xamarin.Android.BuildTools
{
	static class Program
	{
		static int Main (string [] args)
		{
			try {
				if (args.Length == 2 && args [0] == "--permissions") {
					ApplyPermissions (args [1]);
					return 0;
				}
				if (args.Length < 7 || args.Length > 8)
					throw new ArgumentException ("Expected: SDK root, SDK version, manifest directory, pack root, package directory, output project, workload (or dependencies).");

				var sdkRoot = Path.GetFullPath (args [0]);
				var sdkVersion = args [1];
				if (!Directory.Exists (Path.Combine (sdkRoot, "sdk", sdkVersion)))
					throw new DirectoryNotFoundException ($"The .NET SDK '{sdkVersion}' is not installed in '{sdkRoot}'.");
				var manifestDirectory = Path.GetFullPath (args [2]);
				var packRoot = Path.GetFullPath (args [3]);
				var packageDirectories = args [4].Split (Path.PathSeparator).Select (Path.GetFullPath).ToArray ();
				var outputProject = Path.GetFullPath (args [5]);
				var sdkProvider = new SdkDirectoryWorkloadManifestProvider (sdkRoot, sdkVersion, null, null);
				var localProvider = new SdkDirectoryWorkloadManifestProvider (packRoot, sdkVersion, null, null);
				var provider = new LocalManifestProvider (sdkProvider, localProvider, manifestDirectory);
				var resolver = WorkloadResolver.Create (provider, sdkRoot, sdkVersion, null, _ => null);
				var workloadIds = new List<WorkloadId> ();
				if (args [6] == "dependencies") {
					using var manifest = JsonDocument.Parse (File.ReadAllText (Path.Combine (manifestDirectory, "WorkloadManifest.json")));
					foreach (var dependency in manifest.RootElement.GetProperty ("workloads").GetProperty ("android").GetProperty ("extends").EnumerateArray ()) {
						var name = dependency.GetString () ?? throw new InvalidDataException ("A workload dependency has no name.");
						workloadIds.Add (new WorkloadId (name));
					}
				} else {
					workloadIds.Add (new WorkloadId (args [6]));
				}

				var packIds = workloadIds.SelectMany (resolver.GetPacksInWorkload);
				if (args [6] == "dependencies")
					packIds = packIds.Append (new WorkloadPackId ("Microsoft.Android.Sdk.net11"));

				var downloads = new Dictionary<string, List<string>> (StringComparer.OrdinalIgnoreCase);
				var installs = new XElement ("ItemGroup");
				var resolvedPacks = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
				var packages = packageDirectories.Where (Directory.Exists).SelectMany (d => Directory.GetFiles (d, "*.nupkg"))
					.ToDictionary (Path.GetFileName, StringComparer.OrdinalIgnoreCase);
				foreach (var packId in packIds.Distinct ()) {
					var pack = resolver.TryGetPackInfo (packId);
					if (pack == null)
						continue; // The resolver excludes packs with no alias for this host.
					if (args [6] == "dependencies" && Directory.Exists (pack.Path))
						continue;

					var id = pack.ResolvedPackageId.ToLowerInvariant ();
					var version = pack.Version.ToLowerInvariant ();
					var fileName = $"{id}.{version}.nupkg";
					if (!resolvedPacks.Add (fileName))
						continue;
					string source;
					if (packages.TryGetValue (fileName, out var localPackage)) {
						source = localPackage;
					} else {
						if (!downloads.TryGetValue (pack.ResolvedPackageId, out var versions)) {
							versions = [];
							downloads.Add (pack.ResolvedPackageId, versions);
						}
						versions.Add ($"[{pack.Version}]");
						source = $"$(NuGetPackageRoot){id}/{version}/{fileName}";
					}
					var packed = pack.Kind == WorkloadPackKind.Template || pack.Kind == WorkloadPackKind.Library;
					var destination = pack.Kind switch {
						WorkloadPackKind.Template => Path.Combine (packRoot, "template-packs"),
						WorkloadPackKind.Library => Path.Combine (packRoot, "library-packs"),
						WorkloadPackKind.Tool => Path.Combine (packRoot, "tool-packs", pack.ResolvedPackageId, pack.Version),
						WorkloadPackKind.Sdk or WorkloadPackKind.Framework => Path.Combine (packRoot, "packs", pack.ResolvedPackageId, pack.Version),
						_ => throw new InvalidDataException ($"Unknown workload pack kind: {pack.Kind}"),
					};
					installs.Add (new XElement ("_WorkloadPack", new XAttribute ("Include", source),
						new XElement ("Destination", destination),
						new XElement ("Packed", packed.ToString ().ToLowerInvariant ()),
						new XElement ("PackageFileName", fileName)));
				}

				var dotnetPath = Path.Combine (sdkRoot, OperatingSystem.IsWindows () ? "dotnet.exe" : "dotnet");
				var toolPath = typeof (Program).Assembly.Location;
				var project = new XDocument (new XElement ("Project", new XAttribute ("Sdk", "Microsoft.Build.NoTargets"),
					new XElement ("PropertyGroup", new XElement ("TargetFramework", "netstandard2.0"),
						args.Length == 8 && args [7] != "" ? new XElement ("RestoreSources", args [7]) : null),
					new XElement ("ItemGroup", downloads.Select (d => new XElement ("PackageDownload",
						new XAttribute ("Include", d.Key), new XAttribute ("Version", string.Join (";", d.Value))))), installs,
					new XElement ("Target", new XAttribute ("Name", "Install"),
						new XAttribute ("Condition", "'@(_WorkloadPack->Count())' != '0'"),
						new XAttribute ("Inputs", $"@(_WorkloadPack);$(MSBuildThisFileFullPath);{toolPath}"),
						new XAttribute ("Outputs", "%(_WorkloadPack.Destination)/%(_WorkloadPack.PackageFileName).installed"),
						new XElement ("Unzip", new XAttribute ("SourceFiles", "@(_WorkloadPack)"),
							new XAttribute ("DestinationFolder", "%(_WorkloadPack.Destination)"),
							new XAttribute ("Condition", "'%(_WorkloadPack.Packed)' != 'true'")),
						new XElement ("Copy", new XAttribute ("SourceFiles", "@(_WorkloadPack)"),
							new XAttribute ("DestinationFolder", "%(_WorkloadPack.Destination)"),
							new XAttribute ("Condition", "'%(_WorkloadPack.Packed)' == 'true'")),
						new XElement ("Exec", new XAttribute ("Command", $"\"{dotnetPath}\" \"{toolPath}\" --permissions \"%(_WorkloadPack.Destination)\""),
							new XAttribute ("Condition", "'%(_WorkloadPack.Packed)' != 'true'")),
						new XElement ("Touch", new XAttribute ("Files", "%(_WorkloadPack.Destination)/%(_WorkloadPack.PackageFileName).installed"),
							new XAttribute ("AlwaysCreate", "true")))));
				Directory.CreateDirectory (Path.GetDirectoryName (outputProject) ?? throw new InvalidDataException ("The output project has no directory."));
				File.WriteAllText (outputProject, project.ToString ());
				return 0;
			} catch (Exception ex) {
				Console.Error.WriteLine ($"Workload dependency resolution failed: {ex}");
				return 1;
			}
		}

		static void ApplyPermissions (string directory)
		{
			if (OperatingSystem.IsWindows ())
				return;
			var permissionsFile = Path.Combine (directory, "data", "UnixFilePermissions.xml");
			if (!File.Exists (permissionsFile))
				return;
			foreach (var entry in XDocument.Load (permissionsFile).Descendants ("File")) {
				var path = (string) entry.Attribute ("Path") ?? throw new InvalidDataException ("A file permission entry has no path.");
				var permission = (string) entry.Attribute ("Permission") ?? throw new InvalidDataException ("A file permission entry has no mode.");
				File.SetUnixFileMode (Files.GetArchiveExtractionPath (directory, path), (UnixFileMode) Convert.ToInt32 (permission, 8));
			}
		}

		sealed class LocalManifestProvider : IWorkloadManifestProvider
		{
			readonly IWorkloadManifestProvider sdkProvider;
			readonly IWorkloadManifestProvider localProvider;
			readonly string directory;

			public LocalManifestProvider (IWorkloadManifestProvider sdkProvider, IWorkloadManifestProvider localProvider, string directory)
			{
				this.sdkProvider = sdkProvider;
				this.localProvider = localProvider;
				this.directory = directory;
			}

			public IEnumerable<ReadableWorkloadManifest> GetManifests ()
			{
				var localManifests = localProvider.GetManifests ().ToDictionary (m => m.ManifestId, StringComparer.OrdinalIgnoreCase);
				foreach (var manifest in sdkProvider.GetManifests ().Where (m => !localManifests.ContainsKey (m.ManifestId)).Concat (localManifests.Values)) {
					if (!manifest.ManifestId.Equals ("microsoft.net.sdk.android", StringComparison.OrdinalIgnoreCase))
						yield return manifest;
				}
				var path = Path.Combine (directory, "WorkloadManifest.json");
				yield return new ReadableWorkloadManifest ("microsoft.net.sdk.android", directory, path,
					GetSdkFeatureBand (), "", () => File.OpenRead (path), () => null);
			}

			public void RefreshWorkloadManifests () => sdkProvider.RefreshWorkloadManifests ();
			public string GetSdkFeatureBand () => sdkProvider.GetSdkFeatureBand ();
			public IWorkloadManifestProvider.WorkloadVersionInfo GetWorkloadVersion () => sdkProvider.GetWorkloadVersion ();
			public Dictionary<string, WorkloadSet> GetAvailableWorkloadSets () => sdkProvider.GetAvailableWorkloadSets ();
		}
	}
}
