using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class TrimmableTypeMapGeneratorTests : FixtureTestBase, IDisposable
{
	readonly string testDir;
	readonly List<string> logMessages = new ();

	public TrimmableTypeMapGeneratorTests ()
	{
		testDir = Path.Combine (Path.GetTempPath (), "TrimmableTypeMapGeneratorTests", Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (testDir);
	}

	public void Dispose ()
	{
		if (Directory.Exists (testDir)) {
			Directory.Delete (testDir, recursive: true);
		}
	}

	[Fact]
	public void Execute_EmptyAssemblyList_ReturnsEmptyResults ()
	{
		var generator = CreateGenerator ();
		var result = generator.Execute (
			Array.Empty<string> (),
			Path.Combine (testDir, "typemap"),
			Path.Combine (testDir, "java"),
			new Version (11, 0),
			new HashSet<string> ());

		Assert.Empty (result.GeneratedAssemblies);
		Assert.Empty (result.GeneratedJavaFiles);
		Assert.Empty (result.AllPeers);
		Assert.Contains (logMessages, m => m.Contains ("No Java peer types found"));
	}

	[Fact]
	public void Execute_WithTestFixtures_ProducesOutputs ()
	{
		var assemblyPath = GetTestFixtureAssemblyPath ();
		var outputDir = Path.Combine (testDir, "typemap");
		var javaDir = Path.Combine (testDir, "java");

		var generator = CreateGenerator ();
		var result = generator.Execute (
			new [] { assemblyPath },
			outputDir,
			javaDir,
			new Version (11, 0),
			new HashSet<string> ());

		Assert.NotEmpty (result.GeneratedAssemblies);
		Assert.NotEmpty (result.GeneratedJavaFiles);
		Assert.NotEmpty (result.AllPeers);

		Assert.Contains (result.GeneratedAssemblies, p => p.Contains ("_Microsoft.Android.TypeMaps.dll"));
		Assert.Contains (result.GeneratedAssemblies, p => p.Contains ("_TestFixtures.TypeMap.dll"));

		foreach (var assembly in result.GeneratedAssemblies) {
			Assert.True (File.Exists (assembly), $"Generated assembly should exist: {assembly}");
		}
		foreach (var javaFile in result.GeneratedJavaFiles) {
			Assert.True (File.Exists (javaFile), $"Generated Java file should exist: {javaFile}");
		}
	}

	[Fact]
	public void Execute_SecondRun_SkipsUpToDateAssemblies ()
	{
		var assemblyPath = GetTestFixtureAssemblyPath ();
		var outputDir = Path.Combine (testDir, "typemap");
		var javaDir = Path.Combine (testDir, "java");
		var args = new object [] { assemblyPath, outputDir, javaDir };

		// First run
		var generator1 = CreateGenerator ();
		var result1 = generator1.Execute (
			new [] { assemblyPath }, outputDir, javaDir,
			new Version (11, 0), new HashSet<string> ());

		var typeMapPath = result1.GeneratedAssemblies.First (p => p.Contains ("_TestFixtures.TypeMap.dll"));
		var firstWriteTime = File.GetLastWriteTimeUtc (typeMapPath);

		// Second run with fresh log
		logMessages.Clear ();
		var generator2 = CreateGenerator ();
		generator2.Execute (
			new [] { assemblyPath }, outputDir, javaDir,
			new Version (11, 0), new HashSet<string> ());

		var secondWriteTime = File.GetLastWriteTimeUtc (typeMapPath);
		Assert.Equal (firstWriteTime, secondWriteTime);
		Assert.Contains (logMessages, m => m.Contains ("up to date"));
	}

	[Fact]
	public void Execute_SourceTouched_RegeneratesAssembly ()
	{
		var originalPath = GetTestFixtureAssemblyPath ();
		var tempDir = Path.Combine (testDir, "assemblies");
		Directory.CreateDirectory (tempDir);
		var assemblyPath = Path.Combine (tempDir, "TestFixtures.dll");
		File.Copy (originalPath, assemblyPath);

		var outputDir = Path.Combine (testDir, "typemap");
		var javaDir = Path.Combine (testDir, "java");

		// First run
		var generator1 = CreateGenerator ();
		var result1 = generator1.Execute (
			new [] { assemblyPath }, outputDir, javaDir,
			new Version (11, 0), new HashSet<string> ());

		var typeMapPath = result1.GeneratedAssemblies.First (p => p.Contains ("_TestFixtures.TypeMap.dll"));
		var firstWriteTime = File.GetLastWriteTimeUtc (typeMapPath);

		// Touch the source assembly
		File.SetLastWriteTimeUtc (assemblyPath, DateTime.UtcNow.AddSeconds (1));

		// Second run
		var generator2 = CreateGenerator ();
		var result2 = generator2.Execute (
			new [] { assemblyPath }, outputDir, javaDir,
			new Version (11, 0), new HashSet<string> ());

		var secondWriteTime = File.GetLastWriteTimeUtc (typeMapPath);
		Assert.True (secondWriteTime > firstWriteTime,
			"Typemap assembly should be regenerated when source is touched.");
	}

	[Fact]
	public void Execute_NullAssemblyPaths_Throws ()
	{
		var generator = CreateGenerator ();
		Assert.Throws<ArgumentNullException> (() => generator.Execute (
			null!, Path.Combine (testDir, "out"), Path.Combine (testDir, "java"),
			new Version (11, 0), new HashSet<string> ()));
	}

	TrimmableTypeMapGenerator CreateGenerator ()
	{
		return new TrimmableTypeMapGenerator (msg => logMessages.Add (msg));
	}

	static string GetTestFixtureAssemblyPath ()
	{
		var testAssemblyDir = Path.GetDirectoryName (typeof (FixtureTestBase).Assembly.Location)
			?? throw new InvalidOperationException ("Cannot determine test assembly directory");
		var path = Path.Combine (testAssemblyDir, "TestFixtures.dll");
		Assert.True (File.Exists (path), $"TestFixtures.dll not found at {path}");
		return path;
	}
}
