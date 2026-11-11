using System;
using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven_Tests;

[TestClass]
public class ArtifactTests
{
	[TestMethod]
	[DataRow ("com.google.guava:guava:31.1-jre", "com.google.guava", "guava", "31.1-jre")]
	[DataRow ("androidx.core:core:1.9.0", "androidx.core", "core", "1.9.0")]
	[DataRow ("a:b:1", "a", "b", "1")]
	[DataRow ("group_1-x.y:artifact-id_2:1.0.0-SNAPSHOT", "group_1-x.y", "artifact-id_2", "1.0.0-SNAPSHOT")]
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

	[TestMethod]
	[DataRow ("com.google.guava:guava:31.1-jre", "com.google.guava", "guava", "31.1-jre")]
	public void Parse_Valid (string value, string groupId, string artifactId, string version)
	{
		var artifact = Artifact.Parse (value);
		Assert.AreEqual (groupId, artifact.GroupId);
		Assert.AreEqual (artifactId, artifact.Id);
		Assert.AreEqual (version, artifact.Version);
	}

	[TestMethod]
	[DataRow ("")]
	[DataRow ("foo")]
	[DataRow ("foo:bar")]
	[DataRow ("a:b:c:d")]
	[DataRow ("::")]
	[DataRow ("a::1")]
	[DataRow (":b:1")]
	[DataRow ("a:b:")]
	[DataRow ("  :b:1")]
	[DataRow ("a b:c:1")]
	[DataRow ("a:b c:1")]
	[DataRow ("a:b:1 0")]
	[DataRow ("a/b:c:1")]
	[DataRow ("a:b@c:1")]
	[DataRow ("a:b!:1")]
	[DataRow ("a:b:c:d:e:f:g")]
	[DataRow ("../a:b:1")]
	[DataRow ("a:../b:1")]
	[DataRow ("a:b:../1")]
	[DataRow ("a:b:1.0/../")]
	[DataRow ("a/../b:c:1")]
	[DataRow ("..\\a:b:1")]
	[DataRow ("a:b:..\\1.0")]
	public void TryParse_Invalid (string value)
	{
		Assert.IsFalse (Artifact.TryParse (value, out var artifact));
		Assert.IsNull (artifact);
	}

	[TestMethod]
	public void TryParse_Null ()
	{
		Assert.IsFalse (Artifact.TryParse (null!, out var artifact));
		Assert.IsNull (artifact);
	}

	[TestMethod]
	[DataRow ("")]
	[DataRow ("foo")]
	[DataRow ("a:b c:1")]
	public void Parse_Invalid_Throws (string value)
	{
		Assert.ThrowsExactly<ArgumentException> (() => Artifact.Parse (value));
	}

	[TestMethod]
	public void Ctor_Valid ()
	{
		var artifact = new Artifact ("com.example", "lib", "1.0.0");
		Assert.AreEqual ("com.example", artifact.GroupId);
		Assert.AreEqual ("lib", artifact.Id);
		Assert.AreEqual ("1.0.0", artifact.Version);
	}

	[TestMethod]
	[DataRow (null, "lib", "1.0")]
	[DataRow ("com.example", null, "1.0")]
	[DataRow ("com.example", "lib", null)]
	public void Ctor_Null_Throws (string? groupId, string? artifactId, string? version)
	{
		Assert.ThrowsExactly<ArgumentNullException> (() => new Artifact (groupId!, artifactId!, version!));
	}

	[TestMethod]
	[DataRow ("", "lib", "1.0")]
	[DataRow ("   ", "lib", "1.0")]
	[DataRow ("a b", "lib", "1.0")]
	[DataRow ("a/b", "lib", "1.0")]
	[DataRow ("a@b", "lib", "1.0")]
	[DataRow ("com.example", "", "1.0")]
	[DataRow ("com.example", "   ", "1.0")]
	[DataRow ("com.example", "li b", "1.0")]
	[DataRow ("com.example", "lib!", "1.0")]
	[DataRow ("com.example", "lib", "   ")]
	[DataRow ("com.example", "lib", "1 0")]
	[DataRow ("com.example", "lib", "1:0")]
	[DataRow ("../com.example", "lib", "1.0")]
	[DataRow ("com.example", "../lib", "1.0")]
	[DataRow ("com.example", "lib", "../1.0")]
	[DataRow ("com.example", "lib", "1.0/../")]
	[DataRow ("com.example", "lib", "..\\1.0")]
	[DataRow ("com/example", "lib", "1.0")]
	public void Ctor_Invalid_Throws (string groupId, string artifactId, string version)
	{
		Assert.ThrowsExactly<ArgumentException> (() => new Artifact (groupId, artifactId, version));
	}

	[TestMethod]
	public void Ctor_AllowsEmptyVersion ()
	{
		// Empty version is permitted in the constructor to support partial
		// coordinates produced from POM XML where <version> is omitted and
		// inherited from a parent POM.
		var artifact = new Artifact ("com.example", "lib", "");
		Assert.AreEqual ("", artifact.Version);
	}
}
