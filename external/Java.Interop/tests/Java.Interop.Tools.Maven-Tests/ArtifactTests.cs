using System;
using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven_Tests;

public class ArtifactTests
{
	[TestCase ("com.google.guava:guava:31.1-jre", "com.google.guava", "guava", "31.1-jre")]
	[TestCase ("androidx.core:core:1.9.0", "androidx.core", "core", "1.9.0")]
	[TestCase ("a:b:1", "a", "b", "1")]
	[TestCase ("group_1-x.y:artifact-id_2:1.0.0-SNAPSHOT", "group_1-x.y", "artifact-id_2", "1.0.0-SNAPSHOT")]
	public void TryParse_Valid (string value, string groupId, string artifactId, string version)
	{
		Assert.IsTrue (Artifact.TryParse (value, out var artifact));
		Assert.AreEqual (groupId, artifact!.GroupId);
		Assert.AreEqual (artifactId, artifact.Id);
		Assert.AreEqual (version, artifact.Version);
		Assert.AreEqual ($"{groupId}:{artifactId}", artifact.ArtifactString);
		Assert.AreEqual (value, artifact.VersionedArtifactString);
		Assert.AreEqual (value, artifact.ToString ());
	}

	[TestCase ("com.google.guava:guava:31.1-jre", "com.google.guava", "guava", "31.1-jre")]
	public void Parse_Valid (string value, string groupId, string artifactId, string version)
	{
		var artifact = Artifact.Parse (value);
		Assert.AreEqual (groupId, artifact.GroupId);
		Assert.AreEqual (artifactId, artifact.Id);
		Assert.AreEqual (version, artifact.Version);
	}

	[TestCase ("")]
	[TestCase ("foo")]
	[TestCase ("foo:bar")]
	[TestCase ("a:b:c:d")]
	[TestCase ("::")]
	[TestCase ("a::1")]
	[TestCase (":b:1")]
	[TestCase ("a:b:")]
	[TestCase ("  :b:1")]
	[TestCase ("a b:c:1")]
	[TestCase ("a:b c:1")]
	[TestCase ("a:b:1 0")]
	[TestCase ("a/b:c:1")]
	[TestCase ("a:b@c:1")]
	[TestCase ("a:b!:1")]
	[TestCase ("a:b:c:d:e:f:g")]
	[TestCase ("../a:b:1")]
	[TestCase ("a:../b:1")]
	[TestCase ("a:b:../1")]
	[TestCase ("a:b:1.0/../")]
	[TestCase ("a/../b:c:1")]
	[TestCase ("..\\a:b:1")]
	[TestCase ("a:b:..\\1.0")]
	public void TryParse_Invalid (string value)
	{
		Assert.IsFalse (Artifact.TryParse (value, out var artifact));
		Assert.IsNull (artifact);
	}

	[Test]
	public void TryParse_Null ()
	{
		Assert.IsFalse (Artifact.TryParse (null!, out var artifact));
		Assert.IsNull (artifact);
	}

	[TestCase ("")]
	[TestCase ("foo")]
	[TestCase ("a:b c:1")]
	public void Parse_Invalid_Throws (string value)
	{
		Assert.Throws<ArgumentException> (() => Artifact.Parse (value));
	}

	[Test]
	public void Ctor_Valid ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		Assert.AreEqual ("com.example", artifact.GroupId);
		Assert.AreEqual ("lib", artifact.Id);
		Assert.AreEqual ("1.0.0", artifact.Version);
	}

	[TestCase (null, "lib", "1.0")]
	[TestCase ("com.example", null, "1.0")]
	[TestCase ("com.example", "lib", null)]
	public void Ctor_Null_Throws (string? groupId, string? artifactId, string? version)
	{
		Assert.Throws<ArgumentNullException> (() => new Artifact (groupId!, artifactId!, version!));
	}

	[TestCase ("", "lib", "1.0")]
	[TestCase ("   ", "lib", "1.0")]
	[TestCase ("a b", "lib", "1.0")]
	[TestCase ("a/b", "lib", "1.0")]
	[TestCase ("a@b", "lib", "1.0")]
	[TestCase ("com.example", "", "1.0")]
	[TestCase ("com.example", "   ", "1.0")]
	[TestCase ("com.example", "li b", "1.0")]
	[TestCase ("com.example", "lib!", "1.0")]
	[TestCase ("com.example", "lib", "   ")]
	[TestCase ("com.example", "lib", "1 0")]
	[TestCase ("com.example", "lib", "1:0")]
	[TestCase ("../com.example", "lib", "1.0")]
	[TestCase ("com.example", "../lib", "1.0")]
	[TestCase ("com.example", "lib", "../1.0")]
	[TestCase ("com.example", "lib", "1.0/../")]
	[TestCase ("com.example", "lib", "..\\1.0")]
	[TestCase ("com/example", "lib", "1.0")]
	public void Ctor_Invalid_Throws (string groupId, string artifactId, string version)
	{
		Assert.Throws<ArgumentException> (() => new Artifact (groupId, artifactId, version));
	}

	[Test]
	public void Ctor_AllowsEmptyVersion ()
	{
		// Empty version is permitted in the constructor to support partial
		// coordinates produced from POM XML where <version> is omitted and
		// inherited from a parent POM.
		var artifact = new Artifact ("com.example", "lib", "");
		Assert.AreEqual ("", artifact.Version);
	}
}
