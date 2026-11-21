using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Android.MainBuildCache;
using NUnit.Framework;

namespace AzdoMainBuildCache.Tests;

[TestFixture]
public class SnapshotTests
{
	static readonly Dictionary<string, string> VersionProperties = new () {
		["XAVersionBranch"] = "main",
		["XAVersionHash"] = "0123456789",
		["XAVersionCommitCount"] = "42",
		["PackVersionCommitCount"] = "7",
	};
	string directory = "";

	[SetUp]
	public void SetUp ()
	{
		if (!OperatingSystem.IsMacOS ())
			Assert.Ignore ("The cache currently supports macOS only.");
		directory = Path.Combine (Path.GetTempPath (), "azdo-main-build-cache-tests", Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (directory);
	}

	[TearDown]
	public void TearDown ()
	{
		if (Directory.Exists (directory))
			Directory.Delete (directory, recursive: true);
	}

	[TestCase ("https://github.com/dotnet/android.git")]
	[TestCase ("ssh://git@github.com/dotnet/android.git")]
	[TestCase ("git@github.com:dotnet/android.git")]
	public void RecognizesGitHubRemotes (string remote)
	{
		Assert.That (Git.ParseRepository (remote), Is.EqualTo ("dotnet/android"));
	}

	[TestCase ("https://notgithub.com/dotnet/android")]
	[TestCase ("https://github.com.example.com/dotnet/android")]
	[TestCase ("https://github.com/dotnet/")]
	[TestCase ("git@github.com:../android")]
	[TestCase ("https://github.com/dotnet/android?token=invalid")]
	public void RejectsInvalidRepositoryRemotes (string remote)
	{
		Assert.Throws<InvalidOperationException> (() => Git.ParseRepository (remote));
	}

	[TestCase ("bin/Release/../../outside", null)]
	[TestCase ("bin/Release/link", "../../../../outside")]
	[TestCase ("bin/Release/link", "/tmp/outside")]
	public void RejectsArchiveTraversal (string name, string? link)
	{
		string archive = Path.Combine (directory, "outputs.tar.gz");
		using (FileStream stream = File.Create (archive))
		using (var gzip = new GZipStream (stream, CompressionLevel.Fastest))
		using (var writer = new TarWriter (gzip)) {
			var entry = new PaxTarEntry (link is null ? TarEntryType.RegularFile : TarEntryType.SymbolicLink, name);
			if (link is null)
				entry.DataStream = new MemoryStream (Encoding.UTF8.GetBytes ("unsafe"));
			else
				entry.LinkName = link;
			writer.WriteEntry (entry);
		}
		var manifest = new SnapshotManifest (4, "dotnet/android", new string ('a', 40), "Release", "macos",
			System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString ().ToLowerInvariant (),
			DateTimeOffset.UtcNow, Snapshot.HashFile (archive), ["bin/Release"],
			[new SourceFile ("tracked.txt", new string ('b', 64), DateTime.UtcNow.Ticks, false)], directory, null, VersionProperties);

		Assert.Throws<InvalidDataException> (() => Snapshot.Extract (directory, Path.Combine (directory, "tree"), manifest));
		Assert.That (File.Exists (Path.Combine (directory, "outside")), Is.False);
	}

	[TestCase (false, false)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	[TestCase (true, true)]
	public void RejectsSymbolicLinkTraversal (bool chainedLink, bool differentCase)
	{
		string archive = Path.Combine (directory, "outputs.tar.gz");
		using (FileStream stream = File.Create (archive))
		using (var gzip = new GZipStream (stream, CompressionLevel.Fastest))
		using (var writer = new TarWriter (gzip)) {
			writer.WriteEntry (new PaxTarEntry (TarEntryType.SymbolicLink, "bin/Release/a") { LinkName = ".." });
			string alias = differentCase ? "A" : "a";
			var entry = new PaxTarEntry (chainedLink ? TarEntryType.SymbolicLink : TarEntryType.RegularFile,
				chainedLink ? "bin/Release/b" : $"bin/Release/{alias}/payload");
			if (chainedLink)
				entry.LinkName = $"{alias}/../../outside";
			else
				entry.DataStream = new MemoryStream (Encoding.UTF8.GetBytes ("unsafe"));
			writer.WriteEntry (entry);
		}
		var manifest = new SnapshotManifest (4, "dotnet/android", new string ('a', 40), "Release", "macos",
			System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString ().ToLowerInvariant (),
			DateTimeOffset.UtcNow, Snapshot.HashFile (archive), ["bin/Release"],
			[new SourceFile ("tracked.txt", new string ('b', 64), DateTime.UtcNow.Ticks, false)], directory, null, VersionProperties);

		Assert.Throws<InvalidDataException> (() => Snapshot.Extract (directory, Path.Combine (directory, "tree"), manifest));
	}

	[Test]
	public async Task AppliesVerifiedInputsAndRejectsExistingOutputs ()
	{
		var fixture = await CreateFixture ();
		string tracked = Path.Combine (fixture.Target, "tracked.txt");
		File.WriteAllText (tracked, "changed");
		string added = Path.Combine (fixture.Target, "new.cs");
		File.WriteAllText (added, "class Added {}");
		SnapshotManifest manifest = await Snapshot.Read (fixture.Snapshot);
		string tree = Path.Combine (directory, "tree");
		Snapshot.Extract (fixture.Snapshot, tree, manifest);

		await Snapshot.Apply (fixture.Target, tree, manifest);

		Assert.That (File.ReadAllText (Path.Combine (fixture.Target, "bin/Release/product.bin")), Is.EqualTo ("product"));
		SourceFile unchanged = manifest.Sources.Single (source => source.Path == ".gitignore");
		Assert.That (File.GetLastWriteTimeUtc (Path.Combine (fixture.Target, unchanged.Path)),
			Is.EqualTo (new DateTime (unchanged.ModifiedUtcTicks, DateTimeKind.Utc)).Within (TimeSpan.FromSeconds (1)));
		Assert.That (File.GetLastWriteTimeUtc (tracked), Is.GreaterThan (manifest.CreatedUtc.UtcDateTime));
		Assert.That (File.GetLastWriteTimeUtc (added), Is.GreaterThan (manifest.CreatedUtc.UtcDateTime));
		var error = Assert.ThrowsAsync<InvalidOperationException> (() => Snapshot.Apply (fixture.Target, tree, manifest));
		Assert.That (error?.Message, Does.Contain ("refusing to overwrite"));
		Assert.That (File.ReadAllText (Path.Combine (fixture.Target, "bin/Release/product.bin")), Is.EqualTo ("product"));
	}

	[Test]
	public async Task RejectsDeletedInputsBeforeCopyingOutputs ()
	{
		var fixture = await CreateFixture ();
		File.Delete (Path.Combine (fixture.Target, "tracked.txt"));
		SnapshotManifest manifest = await Snapshot.Read (fixture.Snapshot);
		string tree = Path.Combine (directory, "tree");
		Snapshot.Extract (fixture.Snapshot, tree, manifest);

		var error = Assert.ThrowsAsync<InvalidOperationException> (() => Snapshot.Apply (fixture.Target, tree, manifest));
		Assert.That (error?.Message, Does.Contain ("missing"));
		Assert.That (Directory.Exists (Path.Combine (fixture.Target, "bin")), Is.False);
	}

	[Test]
	public async Task RejectsChangedBuildDefinitionsAndOverrides ()
	{
		var fixture = await CreateFixture ();
		SnapshotManifest manifest = await Snapshot.Read (fixture.Snapshot);
		string tree = Path.Combine (directory, "tree");
		Snapshot.Extract (fixture.Snapshot, tree, manifest);
		File.WriteAllText (Path.Combine (fixture.Target, "new.targets"), "<Project />");
		var error = Assert.ThrowsAsync<InvalidOperationException> (() => Snapshot.Apply (fixture.Target, tree, manifest));
		Assert.That (error?.Message, Does.Contain ("Build definitions"));
		File.Delete (Path.Combine (fixture.Target, "new.targets"));
		File.WriteAllText (Path.Combine (fixture.Target, "Configuration.Override.props"), "<Project />");
		error = Assert.ThrowsAsync<InvalidOperationException> (() => Snapshot.Apply (fixture.Target, tree, manifest));
		Assert.That (error?.Message, Does.Contain ("Configuration.Override.props"));
		Assert.That (Directory.Exists (Path.Combine (fixture.Target, "bin")), Is.False);
	}

	[Test]
	public async Task RejectsArchiveWithIncorrectChecksum ()
	{
		var fixture = await CreateFixture ();
		SnapshotManifest manifest = await Snapshot.Read (fixture.Snapshot);
		using (var stream = new FileStream (Path.Combine (fixture.Snapshot, "outputs.tar.gz"), FileMode.Append))
			stream.WriteByte (0);

		var error = Assert.Throws<InvalidDataException> (() => Snapshot.Extract (fixture.Snapshot, Path.Combine (directory, "tree"), manifest));
		Assert.That (error?.Message, Does.Contain ("SHA-256"));
	}

	[Test]
	public async Task ExcludesGradleUserStateFromOutputs ()
	{
		await CreateFixture ();
		string repo = Path.Combine (directory, "repo");
		string project = Path.Combine (repo, "bin/Release/project");
		Directory.CreateDirectory (Path.Combine (project, ".gradle/daemon"));
		File.WriteAllText (Path.Combine (project, ".gradle/daemon/daemon.log"), "local state");
		File.WriteAllText (Path.Combine (project, "product.bin"), "product");
		string snapshot = Path.Combine (directory, "without-gradle");
		await Snapshot.Create (repo, snapshot, "Release", VersionProperties);
		SnapshotManifest manifest = await Snapshot.Read (snapshot);
		string tree = Path.Combine (directory, "tree");

		Snapshot.Extract (snapshot, tree, manifest);

		Assert.That (Directory.Exists (Path.Combine (tree, "bin/Release/project/.gradle")), Is.False);
		Assert.That (File.ReadAllText (Path.Combine (tree, "bin/Release/project/product.bin")), Is.EqualTo ("product"));
	}

	[TestCase ("Sources")]
	[TestCase ("ArchiveHash")]
	[TestCase ("OutputRoots")]
	[TestCase ("SourceRoot")]
	[TestCase ("VersionProperties")]
	public async Task RejectsMissingManifestFields (string field)
	{
		var fixture = await CreateFixture ();
		string path = Path.Combine (fixture.Snapshot, "manifest.json");
		JsonNode manifest = JsonNode.Parse (await File.ReadAllTextAsync (path)) ?? throw new InvalidDataException ();
		manifest [field] = null;
		await File.WriteAllTextAsync (path, manifest.ToJsonString ());

		Assert.ThrowsAsync<InvalidDataException> (() => Snapshot.Read (fixture.Snapshot));
	}

	[Test]
	public async Task PreservesGeneratedInputsAndRelocatesPaths ()
	{
		var fixture = await CreateFixture ();
		string repo = Path.Combine (directory, "repo");
		string generated = "src/Mono.Android/Android.Runtime/JNIEnv.g.cs";
		Directory.CreateDirectory (Path.GetDirectoryName (Path.Combine (repo, generated)) ?? repo);
		File.WriteAllText (Path.Combine (repo, generated), "class Generated {}");
		await File.AppendAllTextAsync (Path.Combine (repo, ".gitignore"), "JNIEnv.g.cs\n");
		await Command.Checked (repo, "git", "add", ".gitignore");
		await Command.Checked (repo, "git", "-c", "user.name=Cache Test", "-c", "user.email=cache-test@example.invalid",
			"commit", "-m", "Ignore generated inputs", "-m", "Co-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>");
		await Command.Checked (fixture.Target, "git", "merge", "--ff-only", "main");
		string config = "src/Mono.Android/obj/Release/Mono.Android.GeneratedMSBuildEditorConfig.editorconfig";
		Directory.CreateDirectory (Path.GetDirectoryName (Path.Combine (repo, config)) ?? repo);
		File.WriteAllText (Path.Combine (repo, config), $"build_property.ProjectDir = {repo}/src/Mono.Android/\n");
		string imports = "src/Mono.Android/obj/Mono.Android.csproj.nuget.g.targets";
		File.WriteAllText (Path.Combine (repo, imports), "<Project />");
		string sourceLink = "src/Mono.Android/obj/Release/Mono.Android.sourcelink.json";
		string sourceLinkContent = $$$"""{"documents":{"{{{repo}}}/*":"https://example.invalid/revision/*"}}""";
		File.WriteAllText (Path.Combine (repo, sourceLink), sourceLinkContent);
		File.WriteAllText (Path.Combine (repo, "src/Mono.Android/obj/project.assets.json"), "{}");
		DateTime timestamp = new DateTime (2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks (1_234_567);
		File.SetLastWriteTimeUtc (Path.Combine (repo, generated), timestamp);
		File.SetLastWriteTimeUtc (Path.Combine (repo, config), timestamp);
		File.SetLastWriteTimeUtc (Path.Combine (repo, imports), timestamp);
		File.SetLastWriteTimeUtc (Path.Combine (repo, sourceLink), timestamp);
		string snapshot = Path.Combine (directory, "generated-snapshot");
		await Snapshot.Create (repo, snapshot, "Release", VersionProperties);
		SnapshotManifest manifest = await Snapshot.Read (snapshot);
		string tree = Path.Combine (directory, "tree");
		Snapshot.Extract (snapshot, tree, manifest);

		await Snapshot.Apply (fixture.Target, tree, manifest);

		Assert.That (File.ReadAllText (Path.Combine (fixture.Target, generated)), Is.EqualTo ("class Generated {}"));
		Assert.That (File.GetLastWriteTimeUtc (Path.Combine (fixture.Target, generated)),
			Is.EqualTo (timestamp));
		Assert.That (File.ReadAllText (Path.Combine (fixture.Target, config)),
			Is.EqualTo ($"build_property.ProjectDir = {fixture.Target}/src/Mono.Android/\n"));
		Assert.That (File.GetLastWriteTimeUtc (Path.Combine (fixture.Target, config)),
			Is.EqualTo (timestamp));
		Assert.That (File.ReadAllText (Path.Combine (fixture.Target, imports)), Is.EqualTo ("<Project />"));
		Assert.That (File.GetLastWriteTimeUtc (Path.Combine (fixture.Target, imports)),
			Is.EqualTo (timestamp));
		Assert.That (File.Exists (Path.Combine (fixture.Target, "src/Mono.Android/obj/project.assets.json")), Is.True);
		Assert.That (File.ReadAllText (Path.Combine (fixture.Target, sourceLink)),
			Is.EqualTo (sourceLinkContent.Replace (repo, fixture.Target)));
		Assert.That (File.GetLastWriteTimeUtc (Path.Combine (fixture.Target, sourceLink)),
			Is.EqualTo (timestamp));
		Assert.That (File.ReadAllText (Path.Combine (tree, config)), Does.Contain (repo));
	}

	[Test]
	public async Task RelocatesNativeBuildTreeAndWritesHook ()
	{
		var fixture = await CreateFixture ();
		string repo = Path.Combine (directory, "repo");
		string native = Path.Combine (repo, "src/native/obj/Release");
		Directory.CreateDirectory (native);
		File.WriteAllText (Path.Combine (native, "build.ninja"), $"build {repo}/src/native/host.o: cc {repo}/src/native/host.cc\n");
		File.WriteAllText (Path.Combine (native, ".ninja_log"), $"{repo}/src/native/host.o\n");
		File.WriteAllBytes (Path.Combine (native, "binary.o"), [.. Encoding.UTF8.GetBytes (repo), 0]);
		string inputs = Path.Combine (repo, "src/bundletool/obj/bundletool.inputs");
		Directory.CreateDirectory (Path.GetDirectoryName (inputs) ?? repo);
		File.WriteAllText (inputs, $"{repo}/bundletool.jar\n");
		DateTime timestamp = new DateTime (2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
		foreach (string file in Directory.EnumerateFiles (native).Append (inputs))
			File.SetLastWriteTimeUtc (file, timestamp);
		string snapshot = Path.Combine (directory, "native-snapshot");
		await Snapshot.Create (repo, snapshot, "Release", VersionProperties);
		SnapshotManifest manifest = await Snapshot.Read (snapshot);
		string tree = Path.Combine (directory, "tree");
		Snapshot.Extract (snapshot, tree, manifest);

		await Snapshot.Apply (fixture.Target, tree, manifest);
		string hook = Path.Combine (fixture.Target, "obj/azdo-main-build-cache/MainBuildCache.targets");

		string target = Path.Combine (fixture.Target, "src/native/obj/Release");
		Assert.That (File.ReadAllText (Path.Combine (target, "build.ninja")),
			Is.EqualTo ($"build {fixture.Target}/src/native/host.o: cc {fixture.Target}/src/native/host.cc\n"));
		Assert.That (File.GetLastWriteTimeUtc (Path.Combine (target, "build.ninja")), Is.EqualTo (timestamp));
		Assert.That (File.ReadAllText (Path.Combine (target, ".ninja_log")), Does.Contain (repo));
		Assert.That (File.ReadAllBytes (Path.Combine (target, "binary.o")), Is.EqualTo (File.ReadAllBytes (Path.Combine (native, "binary.o"))));
		Assert.That (File.ReadAllText (Path.Combine (fixture.Target, "src/bundletool/obj/bundletool.inputs")),
			Is.EqualTo ($"{fixture.Target}/bundletool.jar\n"));
		Assert.That (File.GetLastWriteTimeUtc (Path.Combine (fixture.Target, "src/bundletool/obj/bundletool.inputs")), Is.EqualTo (timestamp));
		Assert.That (File.ReadAllText (hook), Does.Contain ($"Replace('{fixture.Target}/', '{repo}/')"));
		Assert.That (File.ReadAllText (hook), Does.Contain ("<XAVersionBranch>main</XAVersionBranch>"));
		Assert.That (File.GetLastWriteTimeUtc (hook), Is.LessThan (timestamp));
	}

	[Test]
	public async Task PreservesExecutableOutputsAndSymbolicLinks ()
	{
		if (!OperatingSystem.IsMacOS ())
			throw new PlatformNotSupportedException ();
		await CreateFixture ();
		string repo = Path.Combine (directory, "repo");
		string product = Path.Combine (repo, "bin/Release/product.bin");
		File.SetUnixFileMode (product, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		File.CreateSymbolicLink (Path.Combine (repo, "bin/Release/link"), "product.bin");
		File.CreateSymbolicLink (Path.Combine (repo, "bin/Release/missing-link"), "missing");
		string snapshot = Path.Combine (directory, "links-snapshot");
		await Snapshot.Create (repo, snapshot, "Release", VersionProperties);
		SnapshotManifest manifest = await Snapshot.Read (snapshot);
		string tree = Path.Combine (directory, "tree");

		Snapshot.Extract (snapshot, tree, manifest);

		Assert.That (File.GetUnixFileMode (Path.Combine (tree, "bin/Release/product.bin")) & UnixFileMode.UserExecute,
			Is.EqualTo (UnixFileMode.UserExecute));
		Assert.That (new FileInfo (Path.Combine (tree, "bin/Release/link")).LinkTarget, Is.EqualTo ("product.bin"));
		Assert.That (new FileInfo (Path.Combine (tree, "bin/Release/missing-link")).LinkTarget, Is.EqualTo ("missing"));
	}

	[Test]
	public async Task RejectsOutputsNewerThanTheSnapshot ()
	{
		var fixture = await CreateFixture ();
		SnapshotManifest manifest = await Snapshot.Read (fixture.Snapshot);

		var error = Assert.Throws<InvalidDataException> (() =>
			Snapshot.Extract (fixture.Snapshot, Path.Combine (directory, "tree"), manifest with { CreatedUtc = manifest.CreatedUtc.AddDays (-1) }));
		Assert.That (error?.Message, Does.Contain ("newer"));
	}

	[Test]
	public async Task ExecutableBitChangeMarksInputAsChanged ()
	{
		if (!OperatingSystem.IsMacOS ())
			throw new PlatformNotSupportedException ();
		var fixture = await CreateFixture ();
		string tracked = Path.Combine (fixture.Target, "tracked.txt");
		File.SetUnixFileMode (tracked, File.GetUnixFileMode (tracked) | UnixFileMode.UserExecute);
		SnapshotManifest manifest = await Snapshot.Read (fixture.Snapshot);
		string tree = Path.Combine (directory, "tree");
		Snapshot.Extract (fixture.Snapshot, tree, manifest);

		await Snapshot.Apply (fixture.Target, tree, manifest);

		Assert.That (File.GetLastWriteTimeUtc (tracked), Is.GreaterThan (manifest.CreatedUtc.UtcDateTime));
		Assert.That (File.GetUnixFileMode (tracked) & UnixFileMode.UserExecute, Is.EqualTo (UnixFileMode.UserExecute));
	}

	[Test]
	public async Task RejectsUntrackedSubmoduleInputs ()
	{
		var fixture = await CreateFixture ();
		await Command.Checked (fixture.Target, "git", "-c", "protocol.file.allow=always", "submodule", "add",
			Path.Combine (directory, "repo"), "module");
		File.WriteAllText (Path.Combine (fixture.Target, "module", "untracked.cs"), "class Added {}");
		SnapshotManifest manifest = await Snapshot.Read (fixture.Snapshot);
		string tree = Path.Combine (directory, "tree");
		Snapshot.Extract (fixture.Snapshot, tree, manifest);

		var error = Assert.ThrowsAsync<InvalidOperationException> (() => Snapshot.Apply (fixture.Target, tree, manifest));
		Assert.That (error?.Message, Does.Contain ("Untracked files in a submodule"));
	}

	[Test]
	public async Task ConcurrentProcessesShareOneCacheEntry ()
	{
		var fixture = await CreateFixture ();
		string cache = Path.Combine (directory, "cache");
		string host = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		string tool = typeof (Snapshot).Assembly.Location;
		string [] targets = await AddTargets (fixture.Target, 4);
		var results = await Task.WhenAll (targets.Select (target =>
			Command.Run (target, "/usr/bin/env", $"AZDO_MAIN_BUILD_CACHE={cache}", host, tool, "apply", fixture.Snapshot)));

		foreach (var result in results)
			Assert.That (result.ExitCode, Is.Zero, result.Error);
		foreach (string target in targets)
			Assert.That (File.ReadAllText (Path.Combine (target, "bin/Release/product.bin")), Is.EqualTo ("product"));
		Assert.That (Directory.GetDirectories (cache, "local-*", SearchOption.AllDirectories), Has.Length.EqualTo (1));
	}

	[Test]
	public async Task ConcurrentProcessesDownloadRemoteArtifactOnce ()
	{
		if (!OperatingSystem.IsMacOS ())
			throw new PlatformNotSupportedException ();
		var fixture = await CreateFixture ();
		string fakeBin = Path.Combine (directory, "fake-bin");
		Directory.CreateDirectory (fakeBin);
		string az = Path.Combine (fakeBin, "az");
		File.WriteAllText (az, """
			#!/bin/sh
			set -eu
			if [ "$1" = devops ]; then
				printf '[{"id":123,"sourceVersion":"%s","repository":"dotnet/android"}]\n' "$AZDO_TEST_REVISION"
			elif [ "$1" = pipelines ] && [ "$4" = list ]; then
				printf '["azdo-main-build-cache-macos-%s-release"]\n' "$AZDO_TEST_ARCHITECTURE"
			elif [ "$1" = pipelines ] && [ "$4" = download ]; then
				destination=
				while [ "$#" -gt 0 ]; do
					if [ "$1" = --path ]; then
						shift
						destination=$1
						break
					fi
					shift
				done
				test -n "$destination"
				printf 'download\n' >> "$AZDO_TEST_DOWNLOADS"
				/bin/cp -R "$AZDO_TEST_SNAPSHOT/." "$destination"
			else
				exit 1
			fi
			""");
		File.SetUnixFileMode (az, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		string cache = Path.Combine (directory, "cache");
		string downloads = Path.Combine (directory, "downloads");
		string revision = await Git.Revision (fixture.Target);
		string architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString ().ToLowerInvariant ();
		string host = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		string tool = typeof (Snapshot).Assembly.Location;
		string [] targets = await AddTargets (fixture.Target, 4);
		var results = await Task.WhenAll (targets.Select (target =>
			Command.Run (target, "/usr/bin/env", $"PATH={fakeBin}:{Environment.GetEnvironmentVariable ("PATH")}",
				$"AZDO_TEST_REVISION={revision}", $"AZDO_TEST_ARCHITECTURE={architecture}",
				$"AZDO_TEST_DOWNLOADS={downloads}", $"AZDO_TEST_SNAPSHOT={fixture.Snapshot}",
				$"AZDO_MAIN_BUILD_CACHE={cache}", host, tool, "apply")));

		foreach (var result in results)
			Assert.That (result.ExitCode, Is.Zero, result.Error);
		foreach (string target in targets)
			Assert.That (File.ReadAllText (Path.Combine (target, "obj/azdo-main-build-cache/commit")).Trim (), Is.EqualTo (revision));
		Assert.That (File.ReadAllLines (downloads), Has.Length.EqualTo (1));
		Assert.That (Directory.GetDirectories (cache, "build-123", SearchOption.AllDirectories), Has.Length.EqualTo (1));
	}

	[Test]
	public async Task ConcurrentImportPublishesOnceAndPrunesOldEntries ()
	{
		var fixture = await CreateFixture ();
		string cache = Path.Combine (directory, "cache");
		await Task.WhenAll (Enumerable.Range (0, 4).Select (_ => Cache.Get (fixture.Target, cache, fixture.Snapshot, "Release")));
		Assert.That (Directory.GetDirectories (cache, "local-*", SearchOption.AllDirectories), Has.Length.EqualTo (1));

		string next = Path.Combine (directory, "next");
		Directory.CreateDirectory (next);
		File.Copy (Path.Combine (fixture.Snapshot, "outputs.tar.gz"), Path.Combine (next, "outputs.tar.gz"));
		SnapshotManifest manifest = await Snapshot.Read (fixture.Snapshot);
		await File.WriteAllTextAsync (Path.Combine (next, "manifest.json"),
			JsonSerializer.Serialize (manifest with { CreatedUtc = manifest.CreatedUtc.AddTicks (1) }));
		await Cache.Get (fixture.Target, cache, next, "Release");

		Assert.That (Directory.GetDirectories (cache, "local-*", SearchOption.AllDirectories), Has.Length.EqualTo (1));
	}

	async Task<(string Target, string Snapshot)> CreateFixture ()
	{
		string repo = Path.Combine (directory, "repo");
		Directory.CreateDirectory (repo);
		await Command.Checked (repo, "git", "init", "--initial-branch=main");
		await Command.Checked (repo, "git", "remote", "add", "origin", "https://github.com/dotnet/android.git");
		File.WriteAllText (Path.Combine (repo, ".gitignore"), "bin\nobj\n");
		File.WriteAllText (Path.Combine (repo, "tracked.txt"), "original");
		await Command.Checked (repo, "git", "add", ".gitignore", "tracked.txt");
		await Command.Checked (repo, "git", "-c", "user.name=Cache Test", "-c", "user.email=cache-test@example.invalid",
			"commit", "-m", "Create fixture", "-m", "Co-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>");
		string output = Path.Combine (repo, "bin/Release");
		Directory.CreateDirectory (output);
		File.WriteAllText (Path.Combine (output, "product.bin"), "product");
		string snapshot = Path.Combine (directory, "snapshot");
		await Snapshot.Create (repo, snapshot, "Release", VersionProperties);
		string target = Path.Combine (directory, "target");
		await Command.Checked (repo, "git", "worktree", "add", "--detach", target, "HEAD");
		return (target, snapshot);
	}

	async Task<string []> AddTargets (string repo, int count)
	{
		var targets = new string [count];
		for (int i = 0; i < count; i++) {
			targets [i] = Path.Combine (directory, $"target-{i}");
			await Command.Checked (repo, "git", "worktree", "add", "--detach", targets [i], "HEAD");
		}
		return targets;
	}
}
