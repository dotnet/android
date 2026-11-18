// Hydrates a fresh macOS checkout from the bin/obj outputs of a scheduled Release
// main build, or creates such a snapshot in CI. Run via `make prepare
// USE_MAIN_BUILD_CACHE=1`; see .github/skills/azdo-main-build-cache/SKILL.md.
#:property EnableSourceLink=false
#:property EnableSourceControlManagerQueries=false
#:property PublishAot=false

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace Android.MainBuildCache;

static class Program
{
	const string Configuration = "Release";

	static async Task<int> Main (string [] args)
	{
		try {
			if (!OperatingSystem.IsMacOS ())
				throw new InvalidOperationException ("The main build cache currently supports macOS only.");
			string repo = await Git.Root (Environment.CurrentDirectory);
			switch (args) {
				case ["create", string output]:
					await Snapshot.Create (repo, output, Configuration, await Snapshot.QueryVersionProperties (repo, Configuration));
					return 0;
				case ["apply"]:
				case ["apply", _]:
					string root = Environment.GetEnvironmentVariable ("AZDO_MAIN_BUILD_CACHE") is { Length: > 0 } path
						? path
						: Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".azdo-main-build-cache");
					string entry = await Cache.Get (repo, Path.GetFullPath (root), args.Length == 2 ? Path.GetFullPath (args [1]) : null, Configuration);
					await Snapshot.Apply (repo, Path.Combine (entry, "tree"), await Snapshot.Read (entry));
					return 0;
				default:
					Console.Error.WriteLine ("""
						Usage, from the checkout root:
						  dotnet build-tools/scripts/main-build-cache.cs apply [SNAPSHOT]
						  dotnet build-tools/scripts/main-build-cache.cs create OUTPUT

						Without SNAPSHOT, apply downloads the latest compatible scheduled dotnet/android
						main build from Azure DevOps into $AZDO_MAIN_BUILD_CACHE (default
						~/.azdo-main-build-cache). apply requires a fresh checkout.
						""");
					return 2;
			}
		} catch (Exception e) {
			Console.Error.WriteLine (e);
			return 1;
		}
	}
}

static class Command
{
	public static async Task<(int ExitCode, string Output, string Error)> Run (string directory, string command, params string [] arguments)
	{
		var start = new ProcessStartInfo (command) {
			WorkingDirectory = directory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (string argument in arguments)
			start.ArgumentList.Add (argument);
		using Process process = Process.Start (start) ?? throw new InvalidOperationException ($"Could not start '{command}'.");
		Task<string> output = process.StandardOutput.ReadToEndAsync ();
		Task<string> error = process.StandardError.ReadToEndAsync ();
		await process.WaitForExitAsync ();
		return (process.ExitCode, await output, await error);
	}

	public static async Task<string> Checked (string directory, string command, params string [] arguments)
	{
		var result = await Run (directory, command, arguments);
		if (result.ExitCode != 0)
			throw new InvalidOperationException ($"'{command}' failed with exit code {result.ExitCode}: {result.Error.Trim ()}");
		return result.Output;
	}
}

static class Git
{
	public static async Task<string> Root (string directory) =>
		(await Command.Checked (directory, "git", "rev-parse", "--show-toplevel")).Trim ();

	public static async Task<string> Revision (string repo) =>
		(await Command.Checked (repo, "git", "rev-parse", "--verify", "HEAD")).Trim ();

	public static async Task<string> Repository (string repo)
	{
		string remote = (await Command.Checked (repo, "git", "remote", "get-url", "origin")).Trim ();
		return ParseRepository (remote);
	}

	internal static string ParseRepository (string remote)
	{
		string path;
		if (remote.StartsWith ("git@github.com:", StringComparison.OrdinalIgnoreCase)) {
			path = remote ["git@github.com:".Length..];
		} else if (Uri.TryCreate (remote, UriKind.Absolute, out Uri? uri) &&
			uri.Host.Equals ("github.com", StringComparison.OrdinalIgnoreCase) &&
			uri.Scheme is "https" or "ssh" && uri.Query.Length == 0 && uri.Fragment.Length == 0) {
			path = uri.AbsolutePath.TrimStart ('/');
		} else {
			throw new InvalidOperationException ("The origin remote must identify a GitHub repository.");
		}
		if (path.EndsWith (".git", StringComparison.OrdinalIgnoreCase))
			path = path [..^4];
		string [] parts = path.Split ('/');
		if (parts.Length != 2 || parts.Any (part => part.Length == 0 || part is "." or ".." ||
			part.Any (character => !char.IsAsciiLetterOrDigit (character) && character is not ('.' or '-' or '_'))))
			throw new InvalidOperationException ("Cannot determine the GitHub repository from the origin remote.");
		return path.ToLowerInvariant ();
	}

	public static async Task<string []> Files (string repo)
	{
		string output = await Command.Checked (repo, "git", "ls-files", "--cached", "--recurse-submodules", "-z");
		return output.Split ('\0', StringSplitOptions.RemoveEmptyEntries);
	}

	public static async Task RequireNoUntrackedSubmoduleFiles (string repo)
	{
		string output = await Command.Checked (repo, "git", "submodule", "foreach", "--quiet", "--recursive",
			"git ls-files --others --exclude-standard");
		if (!string.IsNullOrWhiteSpace (output))
			throw new InvalidOperationException ("Untracked files in a submodule require a clean build.");
	}

	public static async Task RequireAncestor (string repo, string revision)
	{
		var result = await Command.Run (repo, "git", "merge-base", "--is-ancestor", revision, "HEAD");
		if (result.ExitCode != 0)
			throw new InvalidOperationException ($"Snapshot commit {revision} is not a known ancestor of HEAD. Fetch the required history before using it. {result.Error.Trim ()}");
	}
}

sealed record Build (int Id, string SourceVersion, string Repository);

static class Cache
{
	static readonly JsonSerializerOptions JsonOptions = new () { PropertyNameCaseInsensitive = true };

	const string Organization = "https://dev.azure.com/dnceng-public";
	const string Project = "public";
	const int Pipeline = 333;

	// Returns the extracted cache entry, importing or downloading it at most once across concurrent callers.
	public static async Task<string> Get (string repo, string root, string? snapshot, string configuration)
	{
		string repository = await Git.Repository (repo);
		string architecture = RuntimeInformation.OSArchitecture.ToString ().ToLowerInvariant ();
		string cache = Path.Combine (root, repository, $"macos-{architecture}-{configuration.ToLowerInvariant ()}");
		Directory.CreateDirectory (cache);

		using FileStream cacheLock = await Lock (Path.Combine (cache, ".lock"));
		string entry;
		if (snapshot is not null) {
			SnapshotManifest manifest = await Snapshot.Read (snapshot);
			await CheckCompatible (repo, repository, manifest, null);
			string manifestHash = Snapshot.HashFile (Path.Combine (snapshot, "manifest.json"));
			entry = await Import (cache, $"local-{manifest.ArchiveHash}-{manifestHash [..16]}", snapshot, manifest);
		} else {
			entry = await Download (repo, repository, cache, architecture, configuration);
		}

		Prune (cache, entry);
		return entry;
	}

	static async Task<FileStream> Lock (string path)
	{
		const int MacOSResourceTemporarilyUnavailable = 35;
		bool reported = false;
		var wait = Stopwatch.StartNew ();
		while (true) {
			try {
				return new FileStream (path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
			} catch (IOException e) when (e.HResult == MacOSResourceTemporarilyUnavailable) {
				if (wait.Elapsed >= TimeSpan.FromHours (1))
					throw new IOException ($"Could not acquire the cache lock within one hour: {path}", e);
				if (!reported) {
					Console.Error.WriteLine ("Waiting for another main build cache operation.");
					reported = true;
				}
				await Task.Delay (250);
			}
		}
	}

	static async Task<string> Download (string repo, string repository, string cache, string architecture, string configuration)
	{
		string json = await Command.Checked (repo, "az", "devops", "invoke", "--area", "build", "--resource", "builds",
			"--organization", Organization, "--route-parameters", $"project={Project}", "--query-parameters",
			$"definitions={Pipeline}", "branchName=refs/heads/main", "reasonFilter=schedule", "statusFilter=completed",
			"resultFilter=succeeded", "$top=30", "queryOrder=finishTimeDescending", "api-version=7.1",
			"--query", "value[].{id:id,sourceVersion:sourceVersion,repository:repository.id}", "--only-show-errors", "-o", "json");
		Build [] builds = JsonSerializer.Deserialize<Build []> (json, JsonOptions) ?? throw new InvalidDataException ("Azure DevOps returned no build list.");
		string artifact = $"azdo-main-build-cache-macos-{architecture}-{configuration.ToLowerInvariant ()}";
		foreach (Build build in builds) {
			if (!string.Equals (build.Repository, repository, StringComparison.OrdinalIgnoreCase))
				continue;
			var ancestor = await Command.Run (repo, "git", "merge-base", "--is-ancestor", build.SourceVersion, "HEAD");
			if (ancestor.ExitCode != 0)
				continue;
			string entry = Path.Combine (cache, $"build-{build.Id}");
			if (Directory.Exists (entry)) {
				SnapshotManifest cached = await Snapshot.Read (entry);
				await CheckCompatible (repo, repository, cached, build.SourceVersion);
				return entry;
			}

			string list = await Command.Checked (repo, "az", "pipelines", "runs", "artifact", "list",
				"--run-id", build.Id.ToString (), "--organization", Organization, "--project", Project,
				"--query", "[].name", "--only-show-errors", "-o", "json");
			string [] names = JsonSerializer.Deserialize<string []> (list) ?? [];
			string? artifactName = names.FirstOrDefault (name => string.Equals (name, artifact, StringComparison.OrdinalIgnoreCase));
			if (artifactName is null)
				continue;

			string stage = Path.Combine (cache, ".staging-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (stage);
			try {
				string download = Path.Combine (stage, "download");
				Directory.CreateDirectory (download);
				Console.Error.WriteLine ($"Downloading build {build.Id} ({build.SourceVersion}).");
				await Command.Checked (repo, "az", "pipelines", "runs", "artifact", "download",
					"--run-id", build.Id.ToString (), "--artifact-name", artifactName, "--path", download,
					"--organization", Organization, "--project", Project, "--only-show-errors", "-o", "none");
				SnapshotManifest manifest = await Snapshot.Read (download);
				await CheckCompatible (repo, repository, manifest, build.SourceVersion);
				CompleteImport (stage, download, manifest);
				Directory.Move (stage, entry);
				return entry;
			} catch {
				if (Directory.Exists (stage))
					Directory.Delete (stage, recursive: true);
				throw;
			}
		}
		throw new InvalidOperationException ($"No compatible successful scheduled main build with artifact '{artifact}' was found among the 30 latest builds. Fetch main history if necessary.");
	}

	static async Task<string> Import (string cache, string name, string snapshot, SnapshotManifest manifest)
	{
		string entry = Path.Combine (cache, name);
		if (Directory.Exists (entry)) {
			SnapshotManifest cached = await Snapshot.Read (entry);
			if (!string.Equals (cached.ArchiveHash, manifest.ArchiveHash, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException ("Cached snapshot identity does not match the requested archive.");
			return entry;
		}
		string stage = Path.Combine (cache, ".staging-" + Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (stage);
		try {
			CompleteImport (stage, snapshot, manifest);
			Directory.Move (stage, entry);
			return entry;
		} catch {
			if (Directory.Exists (stage))
				Directory.Delete (stage, recursive: true);
			throw;
		}
	}

	static void CompleteImport (string stage, string snapshot, SnapshotManifest manifest)
	{
		Snapshot.Extract (snapshot, Path.Combine (stage, "tree"), manifest);
		File.Copy (Path.Combine (snapshot, "manifest.json"), Path.Combine (stage, "manifest.json"));
		string download = Path.Combine (stage, "download");
		if (Directory.Exists (download))
			Directory.Delete (download, recursive: true);
	}

	static async Task CheckCompatible (string repo, string repository, SnapshotManifest manifest, string? expectedRevision)
	{
		if (!string.Equals (manifest.Repository, repository, StringComparison.OrdinalIgnoreCase) ||
			(expectedRevision is not null && !string.Equals (manifest.Revision, expectedRevision, StringComparison.OrdinalIgnoreCase)))
			throw new InvalidDataException ("Snapshot repository or commit does not match the Azure DevOps build.");
		await Git.RequireAncestor (repo, manifest.Revision);
	}

	static void Prune (string cache, string keep)
	{
		foreach (string path in Directory.EnumerateDirectories (cache)) {
			string name = Path.GetFileName (path);
			if (path == keep || !(name.StartsWith ("build-", StringComparison.Ordinal) ||
				name.StartsWith ("local-", StringComparison.Ordinal) || name.StartsWith (".staging-", StringComparison.Ordinal)))
				continue;
			if ((File.GetAttributes (path) & FileAttributes.ReparsePoint) != 0)
				throw new InvalidDataException ($"Refusing to prune a symbolic link: {path}");
			Directory.Delete (path, recursive: true);
		}
	}
}

sealed record SourceFile (string Path, string Hash, long ModifiedUtcTicks, bool Executable);
sealed record SnapshotManifest (
	int Version,
	string Repository,
	string Revision,
	string Configuration,
	string OperatingSystem,
	string Architecture,
	DateTimeOffset CreatedUtc,
	string ArchiveHash,
	string [] OutputRoots,
	SourceFile [] Sources,
	string SourceRoot,
	string? NuGetPackagesRoot,
	Dictionary<string, string> VersionProperties);

static class Snapshot
{
	const string ManifestName = "manifest.json";
	const string ArchiveName = "outputs.tar.gz";
	static readonly JsonSerializerOptions JsonOptions = new () { WriteIndented = true };
	static readonly string [] GeneratedOutputs = [
		"src/Mono.Android/Android.Runtime/JNIEnv.g.cs",
		"src/Xamarin.Android.Build.Tasks/Xamarin.Android.Common.props",
		"src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.SupportedPlatforms.targets",
		"src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.BundledVersions.targets",
		"src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/Sdk/AutoImport.props",
		"src/Xamarin.Android.Build.Tasks/Xamarin.Android.BuildInfo.txt",
		"src/native/CMakePresets.json",
	];
	const string NativeOutput = "src/native/obj";
	// Makefile and dotnet-local.sh import the build hook from here.
	const string StateDirectory = "obj/azdo-main-build-cache";
	public const string HookName = "MainBuildCache.targets";
	// Computed by GetXAVersionInfo from the branch name and commit counts; they are stamped into
	// assemblies and the workload manifest path, so any new commit or branch would rebuild most of
	// the tree. Warm builds keep the snapshot's values.
	public static readonly string [] VersionPropertyNames = ["XAVersionBranch", "XAVersionHash", "XAVersionCommitCount", "PackVersionCommitCount"];

	public static async Task<Dictionary<string, string>> QueryVersionProperties (string repo, string configuration)
	{
		var arguments = new List<string> { "msbuild", "build-tools/create-packs/Microsoft.Android.Sdk.proj", "-nologo",
			"-t:GetXAVersionInfo", $"-p:Configuration={configuration}" };
		arguments.AddRange (VersionPropertyNames.Select (name => $"-getProperty:{name}"));
		string json = await Command.Checked (repo, Path.Combine (repo, "bin", configuration, "dotnet", "dotnet"), arguments.ToArray ());
		var properties = JsonDocument.Parse (json).RootElement.GetProperty ("Properties");
		return VersionPropertyNames.ToDictionary (name => name, name => properties.GetProperty (name).GetString () ?? "");
	}

	public static async Task Create (string repo, string output, string configuration, Dictionary<string, string> versionProperties)
	{
		if (configuration != "Release")
			throw new ArgumentException ("Only Release snapshots are currently supported.");
		RequireNoOverrides (repo);
		await Git.RequireNoUntrackedSubmoduleFiles (repo);
		output = Path.GetFullPath (output);
		if (output.StartsWith (Path.GetFullPath (repo).TrimEnd (Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
			throw new ArgumentException ("Snapshot output must be outside the source checkout.");
		string status = await Command.Checked (repo, "git", "status", "--porcelain", "--untracked-files=all");
		if (status.Length != 0)
			throw new InvalidOperationException ("Creating a snapshot requires a clean checkout, including submodules.");
		string repository = await Git.Repository (repo);
		string revision = await Git.Revision (repo);
		string [] files = await Git.Files (repo);
		var sources = new List<SourceFile> (files.Length);
		foreach (string file in files) {
			string path = Within (repo, file);
			RequireNoLinks (repo, file);
			if (!File.Exists (path))
				throw new FileNotFoundException ($"Tracked snapshot input is missing: {file}", path);
			sources.Add (new SourceFile (file, HashFile (path), File.GetLastWriteTimeUtc (path).Ticks, IsExecutable (path)));
		}

		string [] roots = FindOutputRoots (repo, configuration).ToArray ();
		if (!roots.Contains ($"bin/{configuration}", StringComparer.Ordinal))
			throw new InvalidOperationException ($"The completed {configuration} build output is missing.");

		Directory.CreateDirectory (output);
		if (Directory.EnumerateFileSystemEntries (output).Any ())
			throw new InvalidOperationException ($"Snapshot output directory must be empty: {output}");
		string archive = Path.Combine (output, ArchiveName);
		using (FileStream destination = File.Create (archive))
		using (var gzip = new GZipStream (destination, CompressionLevel.Fastest))
		using (var writer = new TarWriter (gzip, TarEntryFormat.Pax)) {
			foreach (string root in roots)
				WriteTree (writer, repo, root);
		}

		var manifest = new SnapshotManifest (
			4, repository, revision, configuration, "macos", RuntimeInformation.OSArchitecture.ToString ().ToLowerInvariant (),
			DateTimeOffset.UtcNow, HashFile (archive), roots, sources.ToArray (), Path.GetFullPath (repo).TrimEnd (Path.DirectorySeparatorChar), NuGetPackagesRoot (),
			versionProperties);
		await File.WriteAllTextAsync (Path.Combine (output, ManifestName), JsonSerializer.Serialize (manifest, JsonOptions));
		Console.WriteLine ($"Created main build cache snapshot for {manifest.Revision} in {output}.");
	}

	static IEnumerable<string> FindOutputRoots (string repo, string configuration)
	{
		foreach (string name in new [] { configuration, $"Build{configuration}", $"Test{configuration}" }) {
			string root = $"bin/{name}";
			if (Directory.Exists (Path.Combine (repo, root)))
				yield return root;
		}
		foreach (string root in FindProjectOutputs (repo, repo))
			yield return root;
		foreach (string file in GeneratedOutputs) {
			if (File.Exists (Within (repo, file))) {
				RequireNoLinks (repo, file);
				yield return file;
			}
		}
	}

	static IEnumerable<string> FindProjectOutputs (string repo, string directory)
	{
		foreach (string path in Directory.EnumerateDirectories (directory)) {
			string name = Path.GetFileName (path);
			string relative = Path.GetRelativePath (repo, path).Replace ('\\', '/');
			if ((File.GetAttributes (path) & FileAttributes.ReparsePoint) != 0)
				continue;
			if (name is ".git" or ".gradle" or ".nuget" or "packages" || relative == "external/android-api-docs")
				continue;
			if (name is "bin" or "obj") {
				if (directory != repo)
					yield return relative;
				continue;
			}
			foreach (string child in FindProjectOutputs (repo, path))
				yield return child;
		}
	}

	static void WriteTree (TarWriter writer, string repo, string relative)
	{
		if (!OperatingSystem.IsMacOS ())
			throw new PlatformNotSupportedException ("Only macOS is currently supported.");
		string path = Within (repo, relative);
		FileAttributes attributes = File.GetAttributes (path);
		if (ExcludeOutput (relative))
			return;
		if ((attributes & FileAttributes.ReparsePoint) != 0) {
			writer.WriteEntry (path, relative);
			return;
		}
		TarEntryType type = (attributes & FileAttributes.Directory) != 0 ? TarEntryType.Directory : TarEntryType.RegularFile;
		// TarWriter.WriteEntry(path, name) truncates Unix modification times to whole seconds.
		var entry = new PaxTarEntry (type, relative) {
			ModificationTime = File.GetLastWriteTimeUtc (path),
			Mode = File.GetUnixFileMode (path),
		};
		using FileStream? content = type == TarEntryType.RegularFile ? File.OpenRead (path) : null;
		if (content is not null)
			entry.DataStream = content;
		writer.WriteEntry (entry);
		if (type != TarEntryType.Directory)
			return;
		foreach (string child in Directory.EnumerateFileSystemEntries (path)) {
			string name = Path.GetFileName (child);
			WriteTree (writer, repo, relative + "/" + name);
		}
	}

	static bool ExcludeOutput (string path)
	{
		string name = Path.GetFileName (path);
		return name == ".gradle" ||
			name.EndsWith (".binlog", StringComparison.OrdinalIgnoreCase) ||
			name.EndsWith (".ProjectImports.zip", StringComparison.OrdinalIgnoreCase) ||
			name.EndsWith (".FileListAbsolute.txt", StringComparison.OrdinalIgnoreCase) ||
			name is "project.nuget.cache" or "project.packagespec.json" ||
			name.EndsWith (".nuget.dgspec.json", StringComparison.OrdinalIgnoreCase);
	}

	public static async Task<SnapshotManifest> Read (string directory)
	{
		string json = await File.ReadAllTextAsync (Path.Combine (directory, ManifestName));
		var manifest = JsonSerializer.Deserialize<SnapshotManifest> (json) ?? throw new InvalidDataException ("Snapshot manifest is empty.");
		if (manifest.Version != 4 || manifest.OperatingSystem != "macos" ||
			manifest.Architecture != RuntimeInformation.OSArchitecture.ToString ().ToLowerInvariant () ||
			manifest.Configuration != "Release" || string.IsNullOrWhiteSpace (manifest.Repository) ||
			manifest.CreatedUtc == default || manifest.OutputRoots is not { Length: > 0 } ||
			manifest.Sources is not { Length: > 0 } || !IsHex (manifest.ArchiveHash, 64) ||
			!(IsHex (manifest.Revision, 40) || IsHex (manifest.Revision, 64)) ||
			string.IsNullOrEmpty (manifest.SourceRoot) || !Path.IsPathFullyQualified (manifest.SourceRoot) ||
			manifest.SourceRoot == "/" || manifest.SourceRoot.EndsWith ('/') ||
			(manifest.NuGetPackagesRoot is not null && (!Path.IsPathFullyQualified (manifest.NuGetPackagesRoot) ||
				manifest.NuGetPackagesRoot == "/" || manifest.NuGetPackagesRoot.EndsWith ('/'))) ||
			manifest.VersionProperties is null || VersionPropertyNames.Any (name => !manifest.VersionProperties.TryGetValue (name, out string? value) ||
				string.IsNullOrEmpty (value) || value.Any (c => !char.IsAsciiLetterOrDigit (c) && c is not ('.' or '-' or '_' or '/'))))
			throw new InvalidDataException ("Snapshot format, platform, architecture, or configuration is incompatible.");
		var roots = new HashSet<string> (StringComparer.Ordinal);
		foreach (string? root in manifest.OutputRoots) {
			if (string.IsNullOrEmpty (root) || root.Contains ('\\') || root.Split ('/').Any (part => part is "." or ".." or ""))
				throw new InvalidDataException ($"Invalid output root: {root}");
			bool topLevel = root is "bin/Release" or "bin/BuildRelease" or "bin/TestRelease";
			bool projectOutput = !root.StartsWith ("bin/", StringComparison.Ordinal) &&
				(root.EndsWith ("/bin", StringComparison.Ordinal) || root.EndsWith ("/obj", StringComparison.Ordinal));
			if (!roots.Add (root) || !(topLevel || projectOutput || GeneratedOutputs.Contains (root, StringComparer.Ordinal)))
				throw new InvalidDataException ($"Invalid output root: {root}");
			Within (directory, root);
		}
		if (!roots.Contains ("bin/Release"))
			throw new InvalidDataException ("Snapshot does not contain the Release SDK output.");
		if (roots.Any (root => roots.Any (other => root != other && root.StartsWith (other + "/", StringComparison.Ordinal))))
			throw new InvalidDataException ("Snapshot output roots overlap.");
		var sources = new HashSet<string> (StringComparer.Ordinal);
		foreach (SourceFile? source in manifest.Sources) {
			if (source is null || string.IsNullOrEmpty (source.Path) || !sources.Add (source.Path) ||
				!IsHex (source.Hash, 64) || source.ModifiedUtcTicks <= 0 ||
				source.ModifiedUtcTicks > DateTime.MaxValue.Ticks || source.Path.Contains ('\\') ||
				source.Path.Split ('/').Any (part => part is "." or ".." or ""))
				throw new InvalidDataException ($"Invalid source entry: {source?.Path}");
			Within (directory, source.Path);
		}
		if (sources.Any (source => roots.Any (root => source == root || source.StartsWith (root + "/", StringComparison.Ordinal))))
			throw new InvalidDataException ("Snapshot outputs overlap tracked inputs.");
		return manifest;
	}

	static bool IsHex (string? value, int length) => value is not null && value.Length == length && value.All (Uri.IsHexDigit);

	public static void Extract (string directory, string destination, SnapshotManifest manifest)
	{
		string archive = Path.Combine (directory, ArchiveName);
		if (!string.Equals (HashFile (archive), manifest.ArchiveHash, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException ("Snapshot archive SHA-256 does not match the manifest.");
		Directory.CreateDirectory (destination);
		var seen = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		var links = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		using (FileStream source = File.OpenRead (archive))
		using (var gzip = new GZipStream (source, CompressionMode.Decompress))
		using (var reader = new TarReader (gzip)) {
			while (reader.GetNextEntry () is TarEntry entry) {
				string name = entry.Name.TrimEnd ('/');
				string path = Within (destination, name);
				if (name.Contains ('\\') || name.Split ('/').Any (part => part is "." or ".." or "") ||
					!manifest.OutputRoots.Any (root => name == root || name.StartsWith (root + "/", StringComparison.Ordinal)) || !seen.Add (name))
					throw new InvalidDataException ($"Unexpected or duplicate archive entry: {entry.Name}");
				if (entry.ModificationTime > manifest.CreatedUtc)
					throw new InvalidDataException ($"Archive entry is newer than its snapshot: {entry.Name}");
				if (entry.EntryType == TarEntryType.SymbolicLink) {
					if (Path.IsPathRooted (entry.LinkName) || entry.LinkName.Contains ('\\'))
						throw new InvalidDataException ($"Absolute archive link: {entry.Name}");
					string parent = Path.GetDirectoryName (path) ?? destination;
					string target = Path.GetFullPath (Path.Combine (parent, entry.LinkName));
					Within (destination, Path.GetRelativePath (destination, target));
					links.Add (name, entry.LinkName);
				} else if (entry.EntryType is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile)) {
					throw new InvalidDataException ($"Unsupported archive entry: {entry.Name} ({entry.EntryType})");
				}
			}
		}
		foreach (string name in seen) {
			for (int index = name.IndexOf ('/'); index >= 0; index = name.IndexOf ('/', index + 1)) {
				if (links.ContainsKey (name [..index]))
					throw new InvalidDataException ($"Archive entry is nested beneath a symbolic link: {name}");
			}
		}
		foreach (var link in links)
			RequireContainedLink (link.Key, link.Value, links);
		using (FileStream source = File.OpenRead (archive))
		using (var gzip = new GZipStream (source, CompressionMode.Decompress))
			TarFile.ExtractToDirectory (gzip, destination, overwriteFiles: false);
		foreach (string root in manifest.OutputRoots) {
			if (!Directory.Exists (Within (destination, root)) && !File.Exists (Within (destination, root)))
				throw new InvalidDataException ($"Snapshot output root is missing: {root}");
		}
	}

	static void RequireContainedLink (string name, string target, Dictionary<string, string> links)
	{
		var resolved = new List<string> ();
		var pending = new Queue<string> (name.Split ('/').SkipLast (1).Concat (target.Split ('/')));
		int expansions = 0;
		while (pending.TryDequeue (out string? part)) {
			if (part is "" or ".")
				continue;
			if (part == "..") {
				if (resolved.Count == 0)
					throw new InvalidDataException ($"Archive link escapes its root: {name}");
				resolved.RemoveAt (resolved.Count - 1);
				continue;
			}
			resolved.Add (part);
			string current = string.Join ('/', resolved);
			if (links.TryGetValue (current, out string? next)) {
				if (++expansions > 1024)
					throw new InvalidDataException ($"Archive link contains a cycle: {name}");
				resolved.RemoveAt (resolved.Count - 1);
				pending = new Queue<string> (next.Split ('/').Concat (pending));
			}
		}
	}

	public static async Task Apply (string repo, string tree, SnapshotManifest manifest)
	{
		RequireNoOverrides (repo);
		await Git.RequireNoUntrackedSubmoduleFiles (repo);
		if (!string.Equals (await Git.Repository (repo), manifest.Repository, StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException ("Snapshot belongs to a different repository.");
		await Git.RequireAncestor (repo, manifest.Revision);
		if (manifest.CreatedUtc > DateTimeOffset.UtcNow.AddMinutes (5))
			throw new InvalidOperationException ("Snapshot was created in the future; check the system clocks.");

		string [] files = await Git.Files (repo);
		var sources = manifest.Sources.ToDictionary (s => s.Path, StringComparer.Ordinal);
		var currentFiles = files.ToHashSet (StringComparer.Ordinal);
		if (sources.Keys.Any (path => !currentFiles.Contains (path)))
			throw new InvalidOperationException ("Tracked inputs were deleted since the snapshot; use a clean build.");
		string untracked = await Command.Checked (repo, "git", "ls-files", "--others", "--exclude-standard", "-z");
		string [] additions = untracked.Split ('\0', StringSplitOptions.RemoveEmptyEntries);

		var changed = new List<string> ();
		foreach (string file in files.Concat (additions)) {
			string path = Within (repo, file);
			RequireNoLinks (repo, file);
			if (!File.Exists (path))
				throw new InvalidOperationException ($"Input is missing: {file}");
			if (!sources.TryGetValue (file, out SourceFile? source) ||
				!string.Equals (HashFile (path), source.Hash, StringComparison.OrdinalIgnoreCase) || IsExecutable (path) != source.Executable)
				changed.Add (file);
		}
		string [] unsupported = changed.Where (file => IsBuildDefinition (file) || (!sources.ContainsKey (file) && Path.GetExtension (file) is not (".cs" or ".md"))).ToArray ();
		if (unsupported.Length > 0)
			throw new InvalidOperationException ($"Build definitions or unsupported added inputs differ from the snapshot; use a clean build:{Environment.NewLine}  {string.Join ($"{Environment.NewLine}  ", unsupported)}");
		foreach (string root in manifest.OutputRoots) {
			string destination = Within (repo, root);
			RequireNoLinks (repo, root);
			if (Directory.Exists (destination) || File.Exists (destination))
				throw new InvalidOperationException ($"Output already exists; refusing to overwrite: {destination}");
		}

		foreach (string root in manifest.OutputRoots) {
			string destination = Within (repo, root);
			Directory.CreateDirectory (Path.GetDirectoryName (destination) ?? repo);
			await Command.Checked (repo, "/bin/cp", "-cRp", Within (tree, root), destination);
		}
		RelocateGeneratedPaths (repo, manifest);
		var changedSet = changed.ToHashSet (StringComparer.Ordinal);
		DateTime changedTime = DateTime.UtcNow > manifest.CreatedUtc.UtcDateTime.AddSeconds (1) ? DateTime.UtcNow : manifest.CreatedUtc.UtcDateTime.AddSeconds (1);
		foreach (SourceFile source in manifest.Sources) {
			string path = Within (repo, source.Path);
			File.SetLastWriteTimeUtc (path, changedSet.Contains (source.Path) ? changedTime : new DateTime (source.ModifiedUtcTicks, DateTimeKind.Utc));
		}
		foreach (string file in changed.Where (file => !sources.ContainsKey (file)))
			File.SetLastWriteTimeUtc (Within (repo, file), changedTime);
		WriteHook (repo, manifest);
		File.WriteAllText (Path.Combine (repo, StateDirectory, "commit"), manifest.Revision + "\n");
		Console.WriteLine ($"Applied main build cache for {manifest.Revision}; {changed.Count} inputs differ.");
	}

	static string NuGetPackagesRoot () =>
		Path.GetFullPath (Environment.GetEnvironmentVariable ("NUGET_PACKAGES") is { Length: > 0 } packages
			? packages
			: Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".nuget", "packages"))
		.TrimEnd (Path.DirectorySeparatorChar);

	// The hook pins the snapshot's version properties, and fixes up compile fingerprints: the SDK
	// fingerprints compiler inputs by absolute path, so a relocated checkout rewrites every
	// *.CoreCompileInputs.cache and recompiles. The hook re-hashes the local inputs with the local
	// roots mapped back to the snapshot roots; only when that equals the restored snapshot hash (the
	// inputs differ solely by location) does it restore the fingerprint file's snapshot timestamp.
	static void WriteHook (string repo, SnapshotManifest manifest)
	{
		var mappings = new List<(string Local, string Snapshot)> {
			(Path.GetFullPath (repo).TrimEnd (Path.DirectorySeparatorChar), manifest.SourceRoot),
		};
		if (manifest.NuGetPackagesRoot is not null)
			mappings.Add ((NuGetPackagesRoot (), manifest.NuGetPackagesRoot));
		if (mappings.SelectMany (m => new [] { m.Local, m.Snapshot }).Any (path => path.IndexOfAny (['\'', '$', '@', '%', ';', '<', '>', '&', '"', '`']) >= 0))
			throw new InvalidOperationException ("Checkout or package paths contain characters unsupported by the fingerprint hook.");
		string replace = string.Concat (mappings.Where (m => m.Local != m.Snapshot).Select (m => $"->Replace('{m.Local}/', '{m.Snapshot}/')"));
		string version = string.Concat (VersionPropertyNames.Select (name => $"{Environment.NewLine}    <{name}>{manifest.VersionProperties [name]}</{name}>"));
		string path = Path.Combine (repo, StateDirectory, HookName);
		Directory.CreateDirectory (Path.GetDirectoryName (path) ?? repo);
		File.WriteAllText (path, $$"""
			<Project>
			  <PropertyGroup>{{version}}
			  </PropertyGroup>
			  <Target Name="_AzdoMainBuildCacheReadCompileFingerprint"
			      BeforeTargets="_GenerateCompileDependencyCache"
			      Condition="'$(DesignTimeBuild)' != 'true' and Exists('$(IntermediateOutputPath)$(MSBuildProjectFile).CoreCompileInputs.cache')">
			    <PropertyGroup>
			      <_AzdoMainBuildCacheFingerprintFile>$([System.IO.Path]::Combine('$(MSBuildProjectDirectory)', '$(IntermediateOutputPath)$(MSBuildProjectFile).CoreCompileInputs.cache'))</_AzdoMainBuildCacheFingerprintFile>
			      <_AzdoMainBuildCacheFingerprint>$([System.IO.File]::ReadAllText('$(_AzdoMainBuildCacheFingerprintFile)').Trim())</_AzdoMainBuildCacheFingerprint>
			      <_AzdoMainBuildCacheFingerprintTime>$([System.IO.File]::GetLastWriteTimeUtc('$(_AzdoMainBuildCacheFingerprintFile)').ToString('o'))</_AzdoMainBuildCacheFingerprintTime>
			    </PropertyGroup>
			  </Target>
			  <Target Name="_AzdoMainBuildCacheRestoreCompileFingerprint"
			      AfterTargets="_GenerateCompileDependencyCache"
			      Condition="'$(_AzdoMainBuildCacheFingerprint)' != '' and '$(_AzdoMainBuildCacheFingerprint)' != '$(CoreCompileDependencyHash)'">
			    <ItemGroup>
			      <_AzdoMainBuildCacheRelocated Include="@(CoreCompileCache{{replace}})" />
			    </ItemGroup>
			    <Hash ItemsToHash="@(_AzdoMainBuildCacheRelocated)" IgnoreCase="$([MSBuild]::ValueOrDefault('$(CoreCompileCacheIgnoreCase)', 'true'))">
			      <Output TaskParameter="HashResult" PropertyName="_AzdoMainBuildCacheRelocatedHash" />
			    </Hash>
			    <Touch Files="$(_AzdoMainBuildCacheFingerprintFile)" Time="$(_AzdoMainBuildCacheFingerprintTime)"
			        Condition="'$(_AzdoMainBuildCacheRelocatedHash)' == '$(_AzdoMainBuildCacheFingerprint)'" />
			    <ItemGroup>
			      <_AzdoMainBuildCacheRelocated Remove="@(_AzdoMainBuildCacheRelocated)" />
			    </ItemGroup>
			  </Target>
			</Project>
			""");
		// The hook joins $(MSBuildAllProjects), which every CoreCompile lists as an input; a fresh
		// mtime would make it newer than all restored outputs and force a full recompile.
		File.SetLastWriteTimeUtc (path, new DateTime (2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
	}

	static void RelocateGeneratedPaths (string repo, SnapshotManifest manifest)
	{
		var mappings = new List<(string From, string To)> {
			(manifest.SourceRoot + "/", Path.GetFullPath (repo).TrimEnd (Path.DirectorySeparatorChar) + "/"),
		};
		if (manifest.NuGetPackagesRoot is not null)
			mappings.Add ((manifest.NuGetPackagesRoot + "/", NuGetPackagesRoot () + "/"));
		var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
		// Generators such as NuGet restore and WriteLinesToFile(WriteOnlyWhenDifferent) rewrite their
		// outputs only when the content changes, so relocated outputs keep their snapshot timestamps
		// instead of invalidating every dependent target.
		var candidates = manifest.OutputRoots.Where (root => root.EndsWith ("/obj", StringComparison.Ordinal) && root != NativeOutput)
			.SelectMany (root => Directory.EnumerateFiles (Within (repo, root), "*", options))
			.Where (path =>
				path.EndsWith (".GeneratedMSBuildEditorConfig.editorconfig", StringComparison.Ordinal) ||
				path.EndsWith (".sourcelink.json", StringComparison.Ordinal) ||
				path.EndsWith ("/project.assets.json", StringComparison.Ordinal) ||
				path.EndsWith (".nuget.g.props", StringComparison.Ordinal) ||
				path.EndsWith (".nuget.g.targets", StringComparison.Ordinal) ||
				path.EndsWith (".inputs", StringComparison.Ordinal));
		// CMake build trees record absolute source and build paths in their text files. Ninja's binary
		// logs are left as-is: their stale paths only make ninja rebuild if native sources change.
		if (manifest.OutputRoots.Contains (NativeOutput, StringComparer.Ordinal))
			candidates = candidates.Concat (Directory.EnumerateFiles (Within (repo, NativeOutput), "*", options)
				.Where (path => Path.GetFileName (path) is not (".ninja_deps" or ".ninja_log")));
		candidates = candidates.Concat (manifest.OutputRoots.Where (root => GeneratedOutputs.Contains (root, StringComparer.Ordinal))
			.Select (root => Within (repo, root)));
		foreach (string path in candidates) {
			byte [] bytes = File.ReadAllBytes (path);
			if (bytes.AsSpan ().Contains ((byte) 0))
				continue;
			bool bom = bytes.AsSpan ().StartsWith ((ReadOnlySpan<byte>) [0xEF, 0xBB, 0xBF]);
			string content = new UTF8Encoding (false).GetString (bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
			string relocated = content;
			foreach (var (from, to) in mappings.Where (m => m.From != m.To))
				relocated = relocated.Replace (from, to, StringComparison.Ordinal);
			if (content == relocated)
				continue;
			DateTime timestamp = File.GetLastWriteTimeUtc (path);
			File.WriteAllText (path, relocated, new UTF8Encoding (bom));
			File.SetLastWriteTimeUtc (path, timestamp);
		}
	}

	static bool IsBuildDefinition (string path)
	{
		string name = Path.GetFileName (path);
		string extension = Path.GetExtension (path).ToLowerInvariant ();
		return name is "Makefile" or "CMakeLists.txt" or "NuGet.config" or "global.json" ||
			extension is ".props" or ".targets" or ".proj" or ".csproj" or ".sln" or ".slnx" or ".slnf" or ".mk" or ".cmake" or ".gradle" or ".kts" or ".json" or ".sh" or ".cmd" or ".bat" or ".yml" or ".yaml" or ".rsp";
	}

	static bool IsExecutable (string path)
	{
		if (!OperatingSystem.IsMacOS ())
			throw new PlatformNotSupportedException ("Only macOS is currently supported.");
		return (File.GetUnixFileMode (path) & UnixFileMode.UserExecute) != 0;
	}

	static void RequireNoOverrides (string repo)
	{
		if (File.Exists (Path.Combine (repo, "Configuration.Override.props")))
			throw new InvalidOperationException ("Configuration.Override.props requires a clean build.");
	}

	internal static string Within (string root, string relative)
	{
		if (Path.IsPathRooted (relative))
			throw new InvalidDataException ($"Absolute path is not permitted: {relative}");
		string fullRoot = Path.GetFullPath (root).TrimEnd (Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string full = Path.GetFullPath (Path.Combine (fullRoot, relative));
		if (!full.StartsWith (fullRoot, StringComparison.Ordinal))
			throw new InvalidDataException ($"Path escapes its root: {relative}");
		return full;
	}

	static void RequireNoLinks (string root, string relative)
	{
		string current = Path.GetFullPath (root);
		foreach (string part in relative.Split ('/')) {
			current = Path.Combine (current, part);
			if ((File.Exists (current) || Directory.Exists (current)) &&
				(File.GetAttributes (current) & FileAttributes.ReparsePoint) != 0)
				throw new InvalidDataException ($"A source or destination path contains a symbolic link: {relative}");
		}
	}

	internal static string HashFile (string path)
	{
		using FileStream file = File.OpenRead (path);
		return Convert.ToHexString (SHA256.HashData (file));
	}
}
