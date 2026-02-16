using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class AcwMapWriterTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (AcwMapWriterTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	List<JavaPeerInfo> ScanFixtures ()
	{
		using var scanner = new JavaPeerScanner ();
		return scanner.Scan (new [] { TestFixtureAssemblyPath });
	}

	[Fact]
	public void CreateEntries_ExcludesDoNotGenerateAcwTypes ()
	{
		var peers = ScanFixtures ();
		var assemblyName = "TestFixtures";
		var entries = AcwMapWriter.CreateEntries (peers, assemblyName);

		// MCW types (DoNotGenerateAcw = true) should not appear
		Assert.DoesNotContain (entries, e => e.JavaKey == "java.lang.Object");
		Assert.DoesNotContain (entries, e => e.JavaKey == "android.app.Activity");
		Assert.DoesNotContain (entries, e => e.JavaKey == "android.app.Service");
	}

	[Fact]
	public void CreateEntries_IncludesUserTypes ()
	{
		var peers = ScanFixtures ();
		var assemblyName = "TestFixtures";
		var entries = AcwMapWriter.CreateEntries (peers, assemblyName);

		// User-defined types without DoNotGenerateAcw should appear
		Assert.Contains (entries, e => e.ManagedKey == "MyApp.MainActivity");
		Assert.Contains (entries, e => e.ManagedKey == "MyApp.MyHelper");
	}

	[Fact]
	public void CreateEntries_FormatsJavaKeyWithDots ()
	{
		var peers = ScanFixtures ();
		var assemblyName = "TestFixtures";
		var entries = AcwMapWriter.CreateEntries (peers, assemblyName);

		var mainActivity = entries.First (e => e.ManagedKey == "MyApp.MainActivity");
		Assert.Equal ("my.app.MainActivity", mainActivity.JavaKey);
	}

	[Fact]
	public void CreateEntries_FormatsPartialAssemblyQualifiedName ()
	{
		var peers = ScanFixtures ();
		var assemblyName = "TestFixtures";
		var entries = AcwMapWriter.CreateEntries (peers, assemblyName);

		var mainActivity = entries.First (e => e.ManagedKey == "MyApp.MainActivity");
		Assert.Equal ("MyApp.MainActivity, TestFixtures", mainActivity.PartialAssemblyQualifiedName);
	}

	[Fact]
	public void CreateEntries_FiltersToSpecifiedAssembly ()
	{
		var peers = ScanFixtures ();
		var entries = AcwMapWriter.CreateEntries (peers, "NonExistentAssembly");

		Assert.Empty (entries);
	}

	[Fact]
	public void WriteMap_SortsByManagedKey ()
	{
		var entries = new List<AcwMapEntry> {
			new AcwMapEntry { JavaKey = "z.Z", ManagedKey = "Z.Z", PartialAssemblyQualifiedName = "Z.Z, A", CompatJniName = "z.Z", AssemblyName = "A" },
			new AcwMapEntry { JavaKey = "a.A", ManagedKey = "A.A", PartialAssemblyQualifiedName = "A.A, A", CompatJniName = "a.A", AssemblyName = "A" },
			new AcwMapEntry { JavaKey = "m.M", ManagedKey = "M.M", PartialAssemblyQualifiedName = "M.M, A", CompatJniName = "m.M", AssemblyName = "A" },
		};

		using var sw = new StringWriter ();
		AcwMapWriter.WriteMap (entries, sw);
		var lines = sw.ToString ().Split ('\n', System.StringSplitOptions.RemoveEmptyEntries);

		// First entry (sorted) should be A.A
		Assert.StartsWith ("A.A, A;", lines [0]);
	}

	[Fact]
	public void WriteMap_ProducesThreeLinesPerEntry ()
	{
		var entries = new List<AcwMapEntry> {
			new AcwMapEntry { JavaKey = "my.Type", ManagedKey = "My.Type", PartialAssemblyQualifiedName = "My.Type, Asm", CompatJniName = "my.Type", AssemblyName = "Asm" },
		};

		using var sw = new StringWriter ();
		AcwMapWriter.WriteMap (entries, sw);
		var lines = sw.ToString ().Split ('\n', System.StringSplitOptions.RemoveEmptyEntries);

		Assert.Equal (3, lines.Length);
		Assert.Equal ("My.Type, Asm;my.Type", lines [0]);
		Assert.Equal ("My.Type;my.Type", lines [1]);
		Assert.Equal ("my.Type;my.Type", lines [2]);
	}

	[Fact]
	public void WriteMap_HandlesEmptyInput ()
	{
		using var sw = new StringWriter ();
		var result = AcwMapWriter.WriteMap (new List<AcwMapEntry> (), sw);
		Assert.Empty (sw.ToString ());
		Assert.False (result.HasErrors);
		Assert.False (result.HasWarnings);
	}

	[Fact]
	public void WriteMap_DetectsXA4215_DuplicateJavaKey ()
	{
		var entries = new List<AcwMapEntry> {
			new AcwMapEntry { JavaKey = "dup.Type", ManagedKey = "A.Type", PartialAssemblyQualifiedName = "A.Type, AsmA", CompatJniName = "dup.Type", AssemblyName = "AsmA" },
			new AcwMapEntry { JavaKey = "dup.Type", ManagedKey = "B.Type", PartialAssemblyQualifiedName = "B.Type, AsmB", CompatJniName = "dup.Type", AssemblyName = "AsmB" },
		};

		using var sw = new StringWriter ();
		var result = AcwMapWriter.WriteMap (entries, sw);

		Assert.True (result.HasErrors);
		Assert.True (result.JavaConflicts.ContainsKey ("dup.Type"));
	}

	[Fact]
	public void WriteMap_DetectsXA4214_DuplicateManagedKey ()
	{
		var entries = new List<AcwMapEntry> {
			new AcwMapEntry { JavaKey = "a.Type", ManagedKey = "Dup.Type", PartialAssemblyQualifiedName = "Dup.Type, AsmA", CompatJniName = "a.Type", AssemblyName = "AsmA" },
			new AcwMapEntry { JavaKey = "b.Type", ManagedKey = "Dup.Type", PartialAssemblyQualifiedName = "Dup.Type, AsmB", CompatJniName = "b.Type", AssemblyName = "AsmB" },
		};

		using var sw = new StringWriter ();
		var result = AcwMapWriter.WriteMap (entries, sw);

		Assert.True (result.HasWarnings);
		Assert.True (result.ManagedConflicts.ContainsKey ("Dup.Type"));
	}

	[Fact]
	public void WriteMap_DuplicateWithinSameAssembly_NoConflict ()
	{
		var entries = new List<AcwMapEntry> {
			new AcwMapEntry { JavaKey = "dup.Type", ManagedKey = "A.Type", PartialAssemblyQualifiedName = "A.Type, Asm", CompatJniName = "dup.Type", AssemblyName = "Asm" },
			new AcwMapEntry { JavaKey = "dup.Type", ManagedKey = "B.Type", PartialAssemblyQualifiedName = "B.Type, Asm", CompatJniName = "dup.Type", AssemblyName = "Asm" },
		};

		using var sw = new StringWriter ();
		var result = AcwMapWriter.WriteMap (entries, sw);

		// Same assembly duplicates are not cross-assembly conflicts
		Assert.False (result.HasErrors);
	}

	[Fact]
	public void WriteMapToFile_WritesOnlyWhenChanged ()
	{
		var entries = new List<AcwMapEntry> {
			new AcwMapEntry { JavaKey = "my.Type", ManagedKey = "My.Type", PartialAssemblyQualifiedName = "My.Type, Asm", CompatJniName = "my.Type", AssemblyName = "Asm" },
		};

		var tempFile = Path.GetTempFileName ();
		try {
			AcwMapWriter.WriteMapToFile (entries, tempFile);
			var firstWriteTime = File.GetLastWriteTimeUtc (tempFile);

			// Wait a bit and write again with same content
			System.Threading.Thread.Sleep (50);
			AcwMapWriter.WriteMapToFile (entries, tempFile);
			var secondWriteTime = File.GetLastWriteTimeUtc (tempFile);

			// File should not have been rewritten
			Assert.Equal (firstWriteTime, secondWriteTime);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void WriteMapToFile_NoOutputOnXA4215 ()
	{
		var entries = new List<AcwMapEntry> {
			new AcwMapEntry { JavaKey = "dup.Type", ManagedKey = "A.Type", PartialAssemblyQualifiedName = "A.Type, AsmA", CompatJniName = "dup.Type", AssemblyName = "AsmA" },
			new AcwMapEntry { JavaKey = "dup.Type", ManagedKey = "B.Type", PartialAssemblyQualifiedName = "B.Type, AsmB", CompatJniName = "dup.Type", AssemblyName = "AsmB" },
		};

		var tempFile = Path.Combine (Path.GetTempPath (), "acw-map-test-" + System.Guid.NewGuid () + ".txt");
		try {
			var result = AcwMapWriter.WriteMapToFile (entries, tempFile);
			Assert.True (result.HasErrors);
			Assert.False (File.Exists (tempFile));
		} finally {
			if (File.Exists (tempFile))
				File.Delete (tempFile);
		}
	}
}
