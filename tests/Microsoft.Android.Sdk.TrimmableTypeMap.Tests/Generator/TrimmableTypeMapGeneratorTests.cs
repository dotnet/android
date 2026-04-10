using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class TrimmableTypeMapGeneratorTests : FixtureTestBase
{
	readonly List<string> logMessages = new ();

	sealed class TestTrimmableTypeMapLogger (List<string> logMessages) : ITrimmableTypeMapLogger
	{
		public void LogNoJavaPeerTypesFound () =>
			logMessages.Add ("No Java peer types found, skipping typemap generation.");
		public void LogJavaPeerScanInfo (int assemblyCount, int peerCount) =>
			logMessages.Add ($"Scanned {assemblyCount} assemblies, found {peerCount} Java peer types.");
		public void LogGeneratingJcwFilesInfo (int jcwPeerCount, int totalPeerCount) =>
			logMessages.Add ($"Generating JCW files for {jcwPeerCount} types (filtered from {totalPeerCount} total).");
		public void LogDeferredRegistrationTypesInfo (int typeCount) =>
			logMessages.Add ($"Found {typeCount} Application/Instrumentation types for deferred registration.");
		public void LogGeneratedTypeMapAssemblyInfo (string assemblyName, int typeCount) =>
			logMessages.Add ($"  {assemblyName}: {typeCount} types");
		public void LogGeneratedRootTypeMapInfo (int assemblyReferenceCount) =>
			logMessages.Add ($"  Root: {assemblyReferenceCount} per-assembly refs");
		public void LogGeneratedTypeMapAssembliesInfo (int assemblyCount) =>
			logMessages.Add ($"Generated {assemblyCount} typemap assemblies.");
		public void LogGeneratedJcwFilesInfo (int sourceCount) =>
			logMessages.Add ($"Generated {sourceCount} JCW Java source files.");
	}

	[Fact]
	public void Execute_EmptyAssemblyList_ReturnsEmptyResults ()
	{
		var result = CreateGenerator ().Execute ([], new Version (11, 0), new HashSet<string> ());
		Assert.Empty (result.GeneratedAssemblies);
		Assert.Empty (result.GeneratedJavaSources);
		Assert.Empty (result.AllPeers);
		Assert.Contains (logMessages, m => m.Contains ("No Java peer types found"));
	}

	[Fact]
	public void Execute_AssemblyWithNoPeers_ReturnsEmpty ()
	{
		// Use the test assembly itself — it has no [Register] types
		var testAssemblyPath = typeof (TrimmableTypeMapGeneratorTests).Assembly.Location;
		using var peReader = new PEReader (File.OpenRead (testAssemblyPath));
		var result = CreateGenerator ().Execute (
			new List<(string, PEReader)> { ("TestAssembly", peReader) },
			new Version (11, 0),
			new HashSet<string> ());
		Assert.Empty (result.GeneratedAssemblies);
		Assert.Empty (result.GeneratedJavaSources);
		Assert.Contains (logMessages, m => m.Contains ("No Java peer types found"));
	}

	[Fact]
	public void Execute_WithTestFixtures_ProducesOutputs ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var result = CreateGenerator ().Execute (new List<(string, PEReader)> { ("TestFixtures", peReader) }, new Version (11, 0), new HashSet<string> ());
		Assert.NotEmpty (result.GeneratedAssemblies);
		Assert.NotEmpty (result.GeneratedJavaSources);
		Assert.Contains (result.GeneratedAssemblies, a => a.Name == "_Microsoft.Android.TypeMaps");
		Assert.Contains (result.GeneratedAssemblies, a => a.Name == "_TestFixtures.TypeMap");
	}

	[Fact]
	public void Execute_CollectsDeferredRegistrationTypes_ForConcreteApplicationAndInstrumentation ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var result = CreateGenerator ().Execute (new List<(string, PEReader)> { ("TestFixtures", peReader) }, new Version (11, 0), new HashSet<string> ());

		Assert.Contains ("my.app.MyApplication", result.ApplicationRegistrationTypes);
		Assert.Contains ("my.app.MyInstrumentation", result.ApplicationRegistrationTypes);
		Assert.DoesNotContain ("my.app.BaseApplication", result.ApplicationRegistrationTypes);
		Assert.DoesNotContain ("my.app.BaseInstrumentation", result.ApplicationRegistrationTypes);
		Assert.DoesNotContain ("my.app.IntermediateInstrumentation", result.ApplicationRegistrationTypes);
	}

	[Fact]
	public void Execute_NullAssemblyList_Throws ()
	{
		IReadOnlyList<(string Name, PEReader Reader)>? n = null;
#pragma warning disable CS8604
		Assert.Throws<ArgumentNullException> (() => CreateGenerator ().Execute (n, new Version (11, 0), new HashSet<string> ()));
#pragma warning restore CS8604
	}

	[Fact]
	public void Execute_GeneratedAssembliesAreValidPE ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var result = CreateGenerator ().Execute (new List<(string, PEReader)> { ("TestFixtures", peReader) }, new Version (11, 0), new HashSet<string> ());
		foreach (var assembly in result.GeneratedAssemblies) {
			assembly.Content.Position = 0;
			using var vr = new PEReader (assembly.Content, PEStreamOptions.LeaveOpen);
			var md = vr.GetMetadataReader ();
			Assert.Equal (assembly.Name, md.GetString (md.GetAssemblyDefinition ().Name));
		}
	}

	[Fact]
	public void Execute_JavaSourcesHaveCorrectStructure ()
	{
		using var peReader = CreateTestFixturePEReader ();
		var result = CreateGenerator ().Execute (new List<(string, PEReader)> { ("TestFixtures", peReader) }, new Version (11, 0), new HashSet<string> ());
		foreach (var source in result.GeneratedJavaSources)
			Assert.Contains ("class ", source.Content);
	}

	TrimmableTypeMapGenerator CreateGenerator () => new (new TestTrimmableTypeMapLogger (logMessages));

	static PEReader CreateTestFixturePEReader ()
	{
		var dir = Path.GetDirectoryName (typeof (FixtureTestBase).Assembly.Location)
			?? throw new InvalidOperationException ("Cannot determine test assembly directory");
		return new PEReader (File.OpenRead (Path.Combine (dir, "TestFixtures.dll")));
	}
}
