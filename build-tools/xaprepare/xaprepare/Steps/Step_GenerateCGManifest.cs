using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Xamarin.Android.Prepare
{
	partial class Step_GenerateCGManifest : Step
	{
		static readonly HashSet<string> DevelopmentDependencies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
			"7-Zip.CommandLine",
			"Kajabity.Tools.Java",
			"Mono.Linq.Expressions",
			"mono/debugger-libs",
			"Newtonsoft.Json",
			"System.CommandLine",
			"Xamarin.Forms",
			"xunit",
			"xunit.abstractions",
			"xunit.analyzers",
			"xunit.assert",
			"xunit.core",
			"xunit.extensibility.core",
			"xunit.extensibility.execution",
			"xunit.runner.utility",

			// ???
			"Microsoft.Build",
			"Microsoft.Build.Framework",
			"Microsoft.Build.Tasks.Core",
			"Microsoft.Build.Utilities.Core",
			"Microsoft.VisualStudio.CoreUtility",
			"Microsoft.VisualStudio.Imaging",
			"Microsoft.VisualStudio.OLE.Interop",
			"Microsoft.VisualStudio.Shell.15.0",
			"Microsoft.VisualStudio.Shell.Framework",
			"Microsoft.VisualStudio.Shell.Interop",
			"Microsoft.VisualStudio.Shell.Interop.8.0",
			"Microsoft.VisualStudio.Shell.Interop.9.0",
			"Microsoft.VisualStudio.TextManager.Interop",
			"Microsoft.VisualStudio.TextManager.Interop.8.0",
			"Microsoft.VisualStudio.Threading",
			"Microsoft.VisualStudio.Utilities",
			"Microsoft.VisualStudio.Validation",
			"Microsoft.VSSDK.BuildTools",
		};

		public Step_GenerateCGManifest ()
			: base ("Generate CGManifest.json")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			var nugets              = MSBuildPackageReferenceInfo.GetPackageReferences ();
			var git                 = new GitRunner (context);
			var gitSubmoduleInfo    = await git.ConfigList (new[]{"--blob", "HEAD:.gitmodules"});
			var gitSubmoduleStatus  = await git.SubmoduleStatus ();
			var gitSubmodules       = GitSubmoduleInfo.GetGitSubmodules (gitSubmoduleInfo, gitSubmoduleStatus);

			var cgManifestEntries   = ((IEnumerable<CGManifestEntry>) nugets).Concat (gitSubmodules)
				.OrderBy (e => e.Name);

			var jsonPath    = Path.Combine (Configurables.Paths.BuildBinDir, "CGManifest.json");
			using var json  = File.CreateText (jsonPath);

			json.WriteLine ("{");
			json.WriteLine ("    \"$schema\": \"https://json.schemastore.org/component-detection-manifest.json\",");
			json.WriteLine ("    \"version\": 1,");
			json.WriteLine ("    \"registrations\": [");

			var properties = new Dictionary<string, string> ();

			bool first = true;

			foreach (var entry in cgManifestEntries) {
				if (first) {
					first   = false;
				} else {
					json.WriteLine (",");
				}

				WriteComponent (json, entry, properties);
			}

			json.WriteLine ();
			json.WriteLine ("    ]");
			json.WriteLine ("}");

			return true;
		}

		void WriteComponent (TextWriter json, CGManifestEntry entry, Dictionary<string, string> properties)
		{
			string dev = DevelopmentDependencies.Contains (entry.Name)
				? "true"
				: "false";

			properties.Clear ();
			entry.FillComponentProperties (properties);

			json.WriteLine ($"        {{");
			json.WriteLine ($"            \"component\": {{");
			json.WriteLine ($"                \"type\": \"{entry.Type.ToLowerInvariant ()}\",");
			json.WriteLine ($"                \"{entry.Type}\": {{");
			bool firstProp = true;
			foreach (var key in properties.Keys.OrderBy (p => p, StringComparer.OrdinalIgnoreCase)) {
				var value = properties [key];
				if (firstProp) {
					firstProp = false;
				} else {
					json.WriteLine (",");
				}
				json.Write ($"                    \"{key}\": \"{value}\"");
			}
			json.WriteLine ();
			json.WriteLine ($"                }}");
			json.WriteLine ($"            }},");
			json.WriteLine ($"            \"developmentDependency\":{dev}");
			json.Write     ($"        }}");
		}
	}

	abstract class CGManifestEntry {

		public  abstract    string  Name {get;}
		public  abstract    string  Type {get;}

		public  abstract    void    FillComponentProperties (Dictionary<string, string> properties);

		protected CGManifestEntry ()
		{
		}
	}

	sealed class GitSubmoduleInfo : CGManifestEntry {

		public  override    string      Name {
			get {
				const string github = "github.com/";
				int i = RepositoryUrl.IndexOf (github, StringComparison.OrdinalIgnoreCase);
				if (i >= 0)
					return RepositoryUrl.Substring (i + github.Length);
				return RepositoryUrl;
			}
		}

		public  override    string      Type    => "git";

		public  string      RepositoryUrl   {get; private set;} = String.Empty;
		public  string      CommitHash      {get; private set;} = String.Empty;
		public  string      LocalPath       {get; private set;} = String.Empty;

		GitSubmoduleInfo ()
		{
		}

		public override void FillComponentProperties (Dictionary<string, string> properties)
		{
			properties ["repositoryUrl"]    = RepositoryUrl;
			properties ["commitHash"]       = CommitHash;
		}

		const string Submodule = "submodule.external/";

		public static IEnumerable<GitSubmoduleInfo> GetGitSubmodules (List<string>? config, List<string>? submoduleStatus)
		{
			if (config == null) {
				yield return CreateEmptySubmoduleInfo ();
			}

			string? entryId = null;
			string? path = null;
			string? url = null;

			foreach (var line in config!) {
				if (!line.StartsWith (Submodule, StringComparison.Ordinal))
					continue;

				string? id = GetSubmoduleId (line);
				if (id != entryId) {
					if (url != null && path != null)
						yield return CreateSubmoduleInfo (url, path);

					entryId = id;
					path    = null;
					url     = null;
				}

				const string Path   = ".path=";
				const string Url    = ".url=";
				const string Git    = ".git";

				int pathIndex   = line.IndexOf (Path, StringComparison.Ordinal);
				if (pathIndex > 0) {
					path        = line.Substring (pathIndex + Path.Length);
					continue;
				}

				int urlIndex    = line.IndexOf (Url, StringComparison.Ordinal);
				if (urlIndex > 0) {
					int start   = urlIndex + Url.Length;
					int count   = line.Length - start;
					if (line.EndsWith (Git, StringComparison.Ordinal))
						count -= Git.Length;
					url         = line.Substring (start, count);
					continue;
				}
			}

			if (url != null && path != null)
				yield return CreateSubmoduleInfo (url, path);

			GitSubmoduleInfo CreateSubmoduleInfo (string url, string path)
			{
				if (submoduleStatus == null) {
					return CreateEmptySubmoduleInfo ();
				}

				string? hash = null;
				foreach (var e in submoduleStatus) {
					int pi  = e.IndexOf (path, StringComparison.OrdinalIgnoreCase);
					if (pi < 1 || e [pi - 1] != ' ')
						continue;
					hash    = e.Substring (1, pi - 2);
					break;
				}
				return new GitSubmoduleInfo () {
					LocalPath       = path,
					RepositoryUrl   = url,
					CommitHash      = hash ?? String.Empty,
				};
			}

			GitSubmoduleInfo CreateEmptySubmoduleInfo ()
			{
				return new GitSubmoduleInfo {
					RepositoryUrl = String.Empty,
					CommitHash = String.Empty,
				};
			}
		}

		static string? GetSubmoduleId (string line)
		{
			int eq = line.IndexOf ('=');
			if (eq < 0)
				return null;
			int lastDot = line.LastIndexOf ('.', eq);
			return line.Substring (Submodule.Length, lastDot - Submodule.Length);
		}
	}

	sealed class MSBuildPackageReferenceInfo : CGManifestEntry {

		string name;

		public  override    string      Name    => name;
		public  override    string      Type    => "nuget";

		public  string      Version {get; private set;}

		MSBuildPackageReferenceInfo (string name, string version)
		{
			this.name = name;
			Version = version;
		}

		public override void FillComponentProperties (Dictionary<string, string> properties)
		{
			properties ["name"]     = Name;
			properties ["version"]  = Version;
		}

		static  readonly    XNamespace      MSBuildXmlns    = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");

		public static IEnumerable<MSBuildPackageReferenceInfo> GetPackageReferences ()
		{
			var files = Directory.EnumerateFiles (BuildPaths.XamarinAndroidSourceRoot, "*.csproj", SearchOption.AllDirectories)
				.Concat (Directory.EnumerateFiles (BuildPaths.XamarinAndroidSourceRoot, "*.targets", SearchOption.AllDirectories))
				.Concat (Directory.EnumerateFiles (BuildPaths.XamarinAndroidSourceRoot, "*.projitems", SearchOption.AllDirectories))
				;
			var packages    = new Dictionary<string, HashSet<string>> ();
			var versions    = new Dictionary<string, HashSet<string>> ();
			foreach (var file in files) {
				var contents            = File.ReadAllText (file);
				if (contents.IndexOf ("PackageReference", StringComparison.Ordinal) < 0 ||
						contents.IndexOf ("PropertyGroup", StringComparison.Ordinal) < 0)
					continue;
				var proj                = XDocument.Parse (contents);
				var packageReferences   = proj.Elements (MSBuildXmlns + "Project")
					.Elements (MSBuildXmlns + "ItemGroup")
					.Elements (MSBuildXmlns + "PackageReference");
				foreach (var packageReference in packageReferences) {
					var name    = (string?) packageReference.Attribute ("Include");
					var version = (string?) packageReference.Attribute ("Version") ??
						packageReference.Element (MSBuildXmlns+"Version")?.Value;
					if (name == null || version == null)
						continue;
					if (!packages.TryGetValue (name, out var v)) {
						packages.Add (name, v = new HashSet<string> ());
					}
					v.Add (version);
				}
				var properties  = proj.Elements (MSBuildXmlns + "Project")
					.Elements (MSBuildXmlns + "PropertyGroup")
					.Elements ();
				foreach (var property in properties) {
					var name = $"$({property.Name.LocalName})";
					if (!property.Name.LocalName.EndsWith ("Version", StringComparison.Ordinal))
						continue;
					if (!versions.TryGetValue (name, out var v)) {
						versions.Add (name, v = new HashSet<string> ());
					}
					v.Add (property.Value.Trim ());
				}
			}

			foreach (var package in packages) {
				string name = package.Key;
				foreach (var version in package.Value) {
					if (version.Length > 0 && version [0] != '$') {
						yield return new MSBuildPackageReferenceInfo (name, version);
						continue;
					}
					if (!versions.TryGetValue (version, out var propertyVersions))
						continue;
					foreach (var propertyVersion in propertyVersions) {
						yield return new MSBuildPackageReferenceInfo (name, propertyVersion);
					}
				}
			}
		}
	}
}
