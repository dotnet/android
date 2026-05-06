using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_GenerateCGManifest : Step
	{
		public Step_GenerateCGManifest ()
			: base ("Generate cgmanifest.json")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			var git                 = new GitRunner (context);
			var gitSubmoduleInfo    = await git.ConfigList (new[]{"--blob", "HEAD:.gitmodules"});
			var gitSubmoduleStatus  = await git.SubmoduleStatus ();
			var gitSubmodules       = GitSubmoduleInfo.GetGitSubmodules (gitSubmoduleInfo, gitSubmoduleStatus)
				.OrderBy (e => e.RepositoryUrl, StringComparer.OrdinalIgnoreCase);

			var jsonPath    = Path.Combine (Configurables.Paths.BuildBinDir, "cgmanifest.json");
			using var json  = File.CreateText (jsonPath);

			json.WriteLine ("{");
			json.WriteLine ("    \"$schema\": \"https://json.schemastore.org/component-detection-manifest.json\",");
			json.WriteLine ("    \"version\": 1,");
			json.WriteLine ("    \"registrations\": [");

			bool first = true;

			foreach (var entry in gitSubmodules) {
				if (first) {
					first = false;
				} else {
					json.WriteLine (",");
				}

				json.WriteLine ($"        {{");
				json.WriteLine ($"            \"component\": {{");
				json.WriteLine ($"                \"type\": \"git\",");
				json.WriteLine ($"                \"git\": {{");
				json.WriteLine ($"                    \"commitHash\": \"{entry.CommitHash}\",");
				json.WriteLine ($"                    \"repositoryUrl\": \"{entry.RepositoryUrl}\"");
				json.WriteLine ($"                }}");
				json.WriteLine ($"            }}");
				json.Write     ($"        }}");
			}

			json.WriteLine ();
			json.WriteLine ("    ]");
			json.WriteLine ("}");

			return true;
		}
	}

	sealed class GitSubmoduleInfo
	{
		public string Name {
			get {
				const string github = "github.com/";
				int i = RepositoryUrl.IndexOf (github, StringComparison.OrdinalIgnoreCase);
				if (i >= 0)
					return RepositoryUrl.Substring (i + github.Length);
				return RepositoryUrl;
			}
		}

		public string RepositoryUrl { get; private set; } = String.Empty;
		public string CommitHash { get; private set; } = String.Empty;
		public string LocalPath { get; private set; } = String.Empty;

		GitSubmoduleInfo ()
		{
		}

		const string Submodule = "submodule.external/";

		public static IEnumerable<GitSubmoduleInfo> GetGitSubmodules (List<string>? config, List<string>? submoduleStatus)
		{
			if (config == null) {
				yield break;
			}

			string? entryId = null;
			string? path = null;
			string? url = null;

			foreach (var line in config) {
				if (!line.StartsWith (Submodule, StringComparison.Ordinal))
					continue;

				string? id = GetSubmoduleId (line);
				if (id != entryId) {
					if (url != null && path != null)
						yield return CreateSubmoduleInfo (url, path, submoduleStatus);

					entryId = id;
					path    = null;
					url     = null;
				}

				const string Path = ".path=";
				const string Url  = ".url=";
				const string Git  = ".git";

				int pathIndex = line.IndexOf (Path, StringComparison.Ordinal);
				if (pathIndex > 0) {
					path = line.Substring (pathIndex + Path.Length);
					continue;
				}

				int urlIndex = line.IndexOf (Url, StringComparison.Ordinal);
				if (urlIndex > 0) {
					int start = urlIndex + Url.Length;
					int count = line.Length - start;
					if (line.EndsWith (Git, StringComparison.Ordinal))
						count -= Git.Length;
					url = line.Substring (start, count);
					continue;
				}
			}

			if (url != null && path != null)
				yield return CreateSubmoduleInfo (url, path, submoduleStatus);
		}

		static GitSubmoduleInfo CreateSubmoduleInfo (string url, string path, List<string>? submoduleStatus)
		{
			string commitHash = String.Empty;

			if (submoduleStatus != null) {
				foreach (var e in submoduleStatus) {
					int pi = e.IndexOf (path, StringComparison.OrdinalIgnoreCase);
					if (pi < 1 || e [pi - 1] != ' ')
						continue;
					commitHash = e.Substring (1, pi - 2);
					break;
				}
			}

			return new GitSubmoduleInfo {
				LocalPath     = path,
				RepositoryUrl = url,
				CommitHash    = commitHash,
			};
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
}
