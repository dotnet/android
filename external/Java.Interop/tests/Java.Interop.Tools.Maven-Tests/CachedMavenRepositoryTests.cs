using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Java.Interop.Tools.Maven.Models;
using Java.Interop.Tools.Maven.Repositories;

namespace Java.Interop.Tools.Maven_Tests;

public class CachedMavenRepositoryTests
{
	string cache_dir = "";

	[SetUp]
	public void SetUp ()
	{
		cache_dir = Path.Combine (Path.GetTempPath (), "Java.Interop.Tools.Maven-Tests", Path.GetRandomFileName ());
		Directory.CreateDirectory (cache_dir);
	}

	[TearDown]
	public void TearDown ()
	{
		if (Directory.Exists (cache_dir))
			Directory.Delete (cache_dir, recursive: true);
	}

	[Test]
	public void GetArtifactFilePath_HappyPath_ReturnsExpectedLayout ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		var inner = new StubRepository ("central", artifact, "lib-1.0.0.jar", new byte [] { 1, 2, 3 });
		var cache = new CachedMavenRepository (cache_dir, inner);

		var expected = Path.GetFullPath (Path.Combine (cache_dir, "central", "com.example", "lib", "1.0.0", "lib-1.0.0.jar"));
		var actual = cache.GetArtifactFilePath (artifact, "lib-1.0.0.jar");

		Assert.AreEqual (expected, actual);
		Assert.IsFalse (File.Exists (actual), "GetArtifactFilePath must not create/download the file.");
	}

	[Test]
	public void TryGetFilePath_HappyPath_DownloadsAndReturnsExpectedPath ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		var content = new byte [] { 1, 2, 3 };
		var inner = new StubRepository ("central", artifact, "lib-1.0.0.jar", content);
		var cache = new CachedMavenRepository (cache_dir, inner);

		var expected = Path.GetFullPath (Path.Combine (cache_dir, "central", "com.example", "lib", "1.0.0", "lib-1.0.0.jar"));

		Assert.IsTrue (cache.TryGetFilePath (artifact, "lib-1.0.0.jar", out var path));
		Assert.AreEqual (expected, path);
		Assert.IsTrue (File.Exists (path));
		CollectionAssert.AreEqual (content, File.ReadAllBytes (path!));
	}

	[Test]
	public void GetArtifactFilePath_RelativeFilename_Throws ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		var inner = new ThrowingRepository ("central");
		var cache = new CachedMavenRepository (cache_dir, inner);

		var artifact_dir = Path.GetDirectoryName (cache.GetArtifactFilePath (artifact, "anchor.jar"))!;
		var outside = Path.Combine (Path.GetDirectoryName (cache_dir)!, Path.GetFileName (cache_dir) + "-sibling", "relative.jar");
		var malicious = Path.GetRelativePath (artifact_dir, outside);

		Assert.Throws<InvalidOperationException> (() => cache.GetArtifactFilePath (artifact, malicious));
	}

	[Test]
	public void TryGetFilePath_RelativeFilename_Throws ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		var inner = new ThrowingRepository ("central");
		var cache = new CachedMavenRepository (cache_dir, inner);

		var artifact_dir = Path.GetDirectoryName (cache.GetArtifactFilePath (artifact, "anchor.jar"))!;
		var outside = Path.Combine (Path.GetDirectoryName (cache_dir)!, Path.GetFileName (cache_dir) + "-sibling", "relative.jar");
		var malicious = Path.GetRelativePath (artifact_dir, outside);

		Assert.Throws<InvalidOperationException> (() => cache.TryGetFilePath (artifact, malicious, out _));
		Assert.AreEqual (0, inner.CallCount, "Inner repository must not be consulted for an escaping path.");
	}

	[Test]
	public void TryGetFile_RelativeFilename_Throws ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		var inner = new ThrowingRepository ("central");
		var cache = new CachedMavenRepository (cache_dir, inner);

		var artifact_dir = Path.GetDirectoryName (cache.GetArtifactFilePath (artifact, "anchor.jar"))!;
		var outside = Path.Combine (Path.GetDirectoryName (cache_dir)!, Path.GetFileName (cache_dir) + "-sibling", "relative.jar");
		var malicious = Path.GetRelativePath (artifact_dir, outside);

		Assert.Throws<InvalidOperationException> (() => cache.TryGetFile (artifact, malicious, out _));
		Assert.AreEqual (0, inner.CallCount, "Inner repository must not be consulted for an escaping path.");
	}

	[Test]
	public void GetFilePathAsync_RelativeFilename_Throws ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		var inner = new ThrowingRepository ("central");
		var cache = new CachedMavenRepository (cache_dir, inner);

		var artifact_dir = Path.GetDirectoryName (cache.GetArtifactFilePath (artifact, "anchor.jar"))!;
		var outside = Path.Combine (Path.GetDirectoryName (cache_dir)!, Path.GetFileName (cache_dir) + "-sibling", "relative.jar");
		var malicious = Path.GetRelativePath (artifact_dir, outside);

		Assert.ThrowsAsync<InvalidOperationException> (async () =>
			await cache.GetFilePathAsync (artifact, malicious, CancellationToken.None));
		Assert.AreEqual (0, inner.CallCount, "Inner repository must not be consulted for an escaping path.");
	}

	[Test]
	public void GetArtifactFilePath_RelativeRepositoryName_Throws ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		var inner = new ThrowingRepository (Path.Combine ("..", Path.GetFileName (cache_dir) + "-sibling"));
		var cache = new CachedMavenRepository (cache_dir, inner);

		Assert.Throws<InvalidOperationException> (() => cache.GetArtifactFilePath (artifact, "lib-1.0.0.jar"));
	}

	[Test]
	public void GetArtifactFilePath_SiblingPrefixCacheDirectory_Throws ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		var sibling = cache_dir + "-sibling";
		var repo_name = Path.GetRelativePath (cache_dir, sibling);
		var inner = new ThrowingRepository (repo_name);
		var cache = new CachedMavenRepository (cache_dir, inner);

		Assert.Throws<InvalidOperationException> (() => cache.GetArtifactFilePath (artifact, "lib-1.0.0.jar"));
	}

	sealed class StubRepository : IMavenRepository
	{
		readonly Artifact expected;
		readonly string expected_filename;
		readonly byte [] content;

		public StubRepository (string name, Artifact expected, string filename, byte [] content)
		{
			Name = name;
			this.expected = expected;
			this.expected_filename = filename;
			this.content = content;
		}

		public string Name { get; }

		public bool TryGetFile (Artifact artifact, string filename, [NotNullWhen (true)] out Stream? stream)
		{
			if (artifact.GroupId == expected.GroupId && artifact.Id == expected.Id && artifact.Version == expected.Version && filename == expected_filename) {
				stream = new MemoryStream (content);
				return true;
			}
			stream = null;
			return false;
		}
	}

	sealed class ThrowingRepository : IMavenRepository
	{
		public ThrowingRepository (string name)
		{
			Name = name;
		}

		public string Name { get; }

		public int CallCount { get; private set; }

		public bool TryGetFile (Artifact artifact, string filename, [NotNullWhen (true)] out Stream? stream)
		{
			CallCount++;
			throw new InvalidOperationException ("Inner repository should not be consulted when the resolved path escapes the cache directory.");
		}
	}
}
