using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for TrimmableTypeMapGenerator — the core logic for per-assembly scanning
/// and output generation. These tests verify the full pipeline without MSBuild.
/// </summary>
public class TrimmableTypeMapGeneratorTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (TrimmableTypeMapGeneratorTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	TrimmableTypeMapResult RunGenerator (string tempDir)
	{
		return TrimmableTypeMapGenerator.Generate (
			inputAssembly: TestFixtureAssemblyPath,
			referenceAssemblies: null,
			typeMapOutputDirectory: Path.Combine (tempDir, "typemaps"),
			javaSourceOutputDirectory: Path.Combine (tempDir, "java"),
			acwMapOutputPath: Path.Combine (tempDir, "acw-map.TestFixtures.txt"),
			componentDataOutputPath: Path.Combine (tempDir, "TestFixtures.componentdata"),
			systemRuntimeVersion: new Version (10, 0, 0));
	}

	[Fact]
	public void Generate_Succeeds ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), "gttm_test_" + Guid.NewGuid ().ToString ("N"));
		try {
			var result = RunGenerator (tempDir);
			Assert.NotNull (result);
		} finally {
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, true);
		}
	}

	[Fact]
	public void Generate_CreatesTypeMapAssembly ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), "gttm_test_" + Guid.NewGuid ().ToString ("N"));
		try {
			var result = RunGenerator (tempDir);

			Assert.True (File.Exists (result.TypeMapAssemblyPath), "TypeMap assembly should be generated");
			Assert.EndsWith (".TypeMap.dll", result.TypeMapAssemblyPath);
		} finally {
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, true);
		}
	}

	[Fact]
	public void Generate_CreatesJavaSources ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), "gttm_test_" + Guid.NewGuid ().ToString ("N"));
		try {
			var result = RunGenerator (tempDir);

			Assert.NotEmpty (result.GeneratedJavaSources);

			// All generated files should exist
			foreach (var path in result.GeneratedJavaSources) {
				Assert.True (File.Exists (path), $"Generated Java source should exist: {path}");
				Assert.EndsWith (".java", path);
			}
		} finally {
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, true);
		}
	}

	[Fact]
	public void Generate_CreatesAcwMap ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), "gttm_test_" + Guid.NewGuid ().ToString ("N"));
		try {
			var acwMapPath = Path.Combine (tempDir, "acw-map.TestFixtures.txt");
			RunGenerator (tempDir);

			Assert.True (File.Exists (acwMapPath), "ACW map should be generated");

			var content = File.ReadAllText (acwMapPath);
			Assert.Contains ("MyApp.MainActivity", content);
			Assert.Contains ("my.app.MainActivity", content);
		} finally {
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, true);
		}
	}

	[Fact]
	public void Generate_CreatesComponentData ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), "gttm_test_" + Guid.NewGuid ().ToString ("N"));
		try {
			var componentDataPath = Path.Combine (tempDir, "TestFixtures.componentdata");
			RunGenerator (tempDir);

			Assert.True (File.Exists (componentDataPath), "Component data should be generated");

			var componentData = ComponentDataSerializer.Deserialize (componentDataPath);
			Assert.NotEmpty (componentData);
			Assert.Contains (componentData, d => d.FullName == "MyApp.MainActivity");
			Assert.Contains (componentData, d => d.FullName == "MyApp.DeepLinkActivity");
		} finally {
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, true);
		}
	}

	[Fact]
	public void Generate_CreatesOutputDirectories ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), "gttm_test_" + Guid.NewGuid ().ToString ("N"));
		try {
			var typeMapDir = Path.Combine (tempDir, "typemaps");
			var javaDir = Path.Combine (tempDir, "java");

			Assert.False (Directory.Exists (typeMapDir));
			Assert.False (Directory.Exists (javaDir));

			RunGenerator (tempDir);

			Assert.True (Directory.Exists (typeMapDir));
			Assert.True (Directory.Exists (javaDir));
		} finally {
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, true);
		}
	}

	[Fact]
	public void Generate_AcwMapHasSemicolonSeparatedLines ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), "gttm_test_" + Guid.NewGuid ().ToString ("N"));
		try {
			var acwMapPath = Path.Combine (tempDir, "acw-map.TestFixtures.txt");
			RunGenerator (tempDir);

			var lines = File.ReadAllLines (acwMapPath)
				.Where (l => !string.IsNullOrWhiteSpace (l))
				.ToArray ();

			Assert.NotEmpty (lines);

			foreach (var line in lines) {
				Assert.Contains (';', line);
				var parts = line.Split (';');
				Assert.Equal (2, parts.Length);
				Assert.False (string.IsNullOrEmpty (parts [0]), $"Left side should not be empty: {line}");
				Assert.False (string.IsNullOrEmpty (parts [1]), $"Right side should not be empty: {line}");
			}

			// Lines should come in groups of 3 per type
			Assert.Equal (0, lines.Length % 3);
		} finally {
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, true);
		}
	}

	[Fact]
	public void Generate_ComponentDataContainsAllComponentKinds ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), "gttm_test_" + Guid.NewGuid ().ToString ("N"));
		try {
			var componentDataPath = Path.Combine (tempDir, "TestFixtures.componentdata");
			RunGenerator (tempDir);

			var componentData = ComponentDataSerializer.Deserialize (componentDataPath);

			var kinds = componentData.Select (d => d.ComponentKind).Distinct ().ToHashSet ();
			Assert.Contains (ManifestComponentKind.Activity, kinds);
			Assert.Contains (ManifestComponentKind.Service, kinds);
			Assert.Contains (ManifestComponentKind.BroadcastReceiver, kinds);
			Assert.Contains (ManifestComponentKind.ContentProvider, kinds);
			Assert.Contains (ManifestComponentKind.Application, kinds);
			Assert.Contains (ManifestComponentKind.Instrumentation, kinds);
		} finally {
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, true);
		}
	}

	[Fact]
	public void Generate_JavaSourcesMatchExpectedTypes ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), "gttm_test_" + Guid.NewGuid ().ToString ("N"));
		try {
			var result = RunGenerator (tempDir);

			// Should have a .java file for MainActivity
			var javaFiles = result.GeneratedJavaSources.Select (Path.GetFileName).ToHashSet ();
			Assert.Contains ("MainActivity.java", javaFiles);
		} finally {
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, true);
		}
	}
}
