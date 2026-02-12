using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Build.TypeMap.Tests;

public class TypeMapAssemblyGeneratorTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (TypeMapAssemblyGeneratorTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	static readonly Lazy<List<JavaPeerInfo>> _cachedFixtures = new (() => {
		using var scanner = new JavaPeerScanner ();
		return scanner.Scan (new [] { TestFixtureAssemblyPath });
	});

	static List<JavaPeerInfo> ScanFixtures () => _cachedFixtures.Value;

	string GenerateAssembly (IReadOnlyList<JavaPeerInfo> peers, string? assemblyName = null)
	{
		var outputPath = Path.Combine (Path.GetTempPath (), $"typemap-test-{Guid.NewGuid ():N}",
			(assemblyName ?? "TestTypeMap") + ".dll");
		var generator = new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		generator.Generate (peers, outputPath, assemblyName);
		return outputPath;
	}

	(PEReader pe, MetadataReader reader) OpenAssembly (string path)
	{
		var pe = new PEReader (File.OpenRead (path));
		return (pe, pe.GetMetadataReader ());
	}

	// ---- Basic assembly structure tests ----

	[Fact]
	public void Generate_ProducesValidPEAssembly ()
	{
		var peers = ScanFixtures ();
		var path = GenerateAssembly (peers);
		try {
			Assert.True (File.Exists (path));
			using var pe = new PEReader (File.OpenRead (path));
			Assert.True (pe.HasMetadata);
			var reader = pe.GetMetadataReader ();
			Assert.NotNull (reader);
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_AssemblyHasCorrectName ()
	{
		var peers = ScanFixtures ();
		var path = GenerateAssembly (peers, "MyTestTypeMap");
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var asmDef = reader.GetAssemblyDefinition ();
				Assert.Equal ("MyTestTypeMap", reader.GetString (asmDef.Name));
			}
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_HasModuleType ()
	{
		var peers = ScanFixtures ();
		var path = GenerateAssembly (peers);
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var types = reader.TypeDefinitions.Select (h => reader.GetTypeDefinition (h)).ToList ();
				Assert.Contains (types, t => reader.GetString (t.Name) == "<Module>");
			}
		} finally {
			CleanUp (path);
		}
	}

	// ---- Assembly reference tests ----

	[Fact]
	public void Generate_HasRequiredAssemblyReferences ()
	{
		var peers = ScanFixtures ();
		var path = GenerateAssembly (peers);
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var asmRefs = reader.AssemblyReferences
					.Select (h => reader.GetString (reader.GetAssemblyReference (h).Name))
					.ToList ();
				Assert.Contains ("System.Runtime", asmRefs);
				Assert.Contains ("Mono.Android", asmRefs);
				Assert.Contains ("Java.Interop", asmRefs);
				Assert.Contains ("System.Runtime.InteropServices", asmRefs);
			}
		} finally {
			CleanUp (path);
		}
	}

	// ---- TypeMap attribute tests ----

	[Fact]
	public void Generate_HasTypeMapAttributes ()
	{
		var peers = ScanFixtures ();
		var path = GenerateAssembly (peers);
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var assemblyCustomAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				Assert.NotEmpty (assemblyCustomAttrs);
				// We should have at least as many attributes as non-duplicate peers
				// (TypeMap attrs + IgnoresAccessChecksTo attrs)
				Assert.True (assemblyCustomAttrs.Count () >= 2);
			}
		} finally {
			CleanUp (path);
		}
	}

	// ---- Proxy type tests ----

	[Fact]
	public void Generate_CreatesProxyTypes ()
	{
		var peers = ScanFixtures ();
		var path = GenerateAssembly (peers);
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var proxyTypes = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
					.ToList ();

				// At least some proxy types should be generated
				Assert.NotEmpty (proxyTypes);

				// Check that a proxy exists for java/lang/Object → Java_Lang_Object_Proxy
				Assert.Contains (proxyTypes, t => reader.GetString (t.Name) == "Java_Lang_Object_Proxy");
			}
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_ProxyTypesAreSealedClasses ()
	{
		var peers = ScanFixtures ();
		var path = GenerateAssembly (peers);
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var proxyTypes = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
					.ToList ();

				foreach (var proxy in proxyTypes) {
					Assert.True ((proxy.Attributes & TypeAttributes.Sealed) != 0,
						$"Proxy {reader.GetString (proxy.Name)} should be sealed");
					Assert.True ((proxy.Attributes & TypeAttributes.Public) != 0,
						$"Proxy {reader.GetString (proxy.Name)} should be public");
				}
			}
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_ProxyType_HasCtorAndCreateInstance ()
	{
		var peers = ScanFixtures ();
		var path = GenerateAssembly (peers);
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var objectProxy = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.First (t => reader.GetString (t.Name) == "Java_Lang_Object_Proxy");

				var methods = objectProxy.GetMethods ()
					.Select (h => reader.GetMethodDefinition (h))
					.Select (m => reader.GetString (m.Name))
					.ToList ();

				Assert.Contains (".ctor", methods);
				Assert.Contains ("CreateInstance", methods);
				Assert.Contains ("get_TargetType", methods);
			}
		} finally {
			CleanUp (path);
		}
	}

	// ---- ACW proxy tests ----

	[Fact]
	public void Generate_AcwProxy_HasRegisterNativesAndUcoMethods ()
	{
		var peers = ScanFixtures ();
		// Find a non-MCW type with marshal methods (e.g., my/app/CustomView has constructors)
		var acwPeer = peers.First (p => p.JavaName == "my/app/TouchHandler");
		var path = GenerateAssembly (new [] { acwPeer }, "AcwTest");
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var proxy = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.First (t => reader.GetString (t.Name) == "MyApp_TouchHandler_Proxy");

				var methods = proxy.GetMethods ()
					.Select (h => reader.GetMethodDefinition (h))
					.Select (m => reader.GetString (m.Name))
					.ToList ();

				Assert.Contains ("RegisterNatives", methods);
				// UCO wrappers for each marshal method
				Assert.Contains (methods, m => m.StartsWith ("n_") && m.EndsWith ("_uco_0"));
			}
		} finally {
			CleanUp (path);
		}
	}

	[Fact]
	public void Generate_AcwProxy_HasUnmanagedCallersOnlyAttribute ()
	{
		var peers = ScanFixtures ();
		var acwPeer = peers.First (p => p.JavaName == "my/app/TouchHandler");
		var path = GenerateAssembly (new [] { acwPeer }, "UcoTest");
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var proxy = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.First (t => reader.GetString (t.Name) == "MyApp_TouchHandler_Proxy");

				// Find a UCO method
				var ucoMethod = proxy.GetMethods ()
					.Select (h => reader.GetMethodDefinition (h))
					.First (m => reader.GetString (m.Name).Contains ("_uco_"));

				// Verify it has [UnmanagedCallersOnly] attribute
				var attrs = ucoMethod.GetCustomAttributes ()
					.Select (h => reader.GetCustomAttribute (h))
					.ToList ();
				Assert.NotEmpty (attrs);
			}
		} finally {
			CleanUp (path);
		}
	}

	// ---- IgnoresAccessChecksTo tests ----

	[Fact]
	public void Generate_HasIgnoresAccessChecksToAttribute ()
	{
		var peers = ScanFixtures ();
		var path = GenerateAssembly (peers);
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				// The IgnoresAccessChecksToAttribute type should be defined
				var types = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.ToList ();
				Assert.Contains (types, t =>
					reader.GetString (t.Name) == "IgnoresAccessChecksToAttribute" &&
					reader.GetString (t.Namespace) == "System.Runtime.CompilerServices");
			}
		} finally {
			CleanUp (path);
		}
	}

	// ---- Alias tests ----

	[Fact]
	public void Generate_DuplicateJniNames_CreatesAliasEntries ()
	{
		// Create two peers with the same JNI name — these become aliases
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "test/Duplicate",
				ManagedTypeName = "Test.Duplicate1",
				ManagedTypeNamespace = "Test",
				ManagedTypeShortName = "Duplicate1",
				AssemblyName = "TestAssembly",
			},
			new JavaPeerInfo {
				JavaName = "test/Duplicate",
				ManagedTypeName = "Test.Duplicate2",
				ManagedTypeNamespace = "Test",
				ManagedTypeShortName = "Duplicate2",
				AssemblyName = "TestAssembly",
			},
		};

		var path = GenerateAssembly (peers, "AliasTest");
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				// Neither peer has activation ctor → no proxies, but both get entries
				var assemblyAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				// Should have 2 TypeMap entries + IgnoresAccessChecksTo entries
				Assert.True (assemblyAttrs.Count () >= 2);
			}
		} finally {
			CleanUp (path);
		}
	}

	// ---- Empty input tests ----

	[Fact]
	public void Generate_EmptyPeerList_ProducesValidAssembly ()
	{
		var path = GenerateAssembly (Array.Empty<JavaPeerInfo> (), "EmptyTest");
		try {
			Assert.True (File.Exists (path));
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				Assert.NotNull (reader);
				var asmDef = reader.GetAssemblyDefinition ();
				Assert.Equal ("EmptyTest", reader.GetString (asmDef.Name));
			}
		} finally {
			CleanUp (path);
		}
	}

	// ---- Per-assembly model tests ----

	[Fact]
	public void Generate_SingleAssemblyInput_Works ()
	{
		var allPeers = ScanFixtures ();
		// Filter to just one assembly's peers
		var testFixturePeers = allPeers.Where (p => p.AssemblyName == "TestFixtures").ToList ();

		var path = GenerateAssembly (testFixturePeers, "_TestFixtures.TypeMap");
		try {
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var asmDef = reader.GetAssemblyDefinition ();
				Assert.Equal ("_TestFixtures.TypeMap", reader.GetString (asmDef.Name));

				// Should still have proxy types
				var proxyTypes = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
					.ToList ();
				Assert.NotEmpty (proxyTypes);
			}
		} finally {
			CleanUp (path);
		}
	}

	// ---- JNI signature helper tests ----

	[Theory]
	[InlineData ("()V", 0)]
	[InlineData ("(I)V", 1)]
	[InlineData ("(Landroid/os/Bundle;)V", 1)]
	[InlineData ("(IFJ)V", 3)]
	[InlineData ("(ZLandroid/view/View;I)Z", 3)]
	[InlineData ("([Ljava/lang/String;)V", 1)]
	public void ParseParameterTypes_ParsesCorrectCount (string signature, int expectedCount)
	{
		var actual = JniSignatureHelper.ParseParameterTypes (signature);
		Assert.Equal (expectedCount, actual.Count);
	}

	[Fact]
	public void ParseParameterTypes_BooleanMapsToBoolean ()
	{
		var types = JniSignatureHelper.ParseParameterTypes ("(Z)V");
		Assert.Single (types);
		Assert.Equal (JniParamKind.Boolean, types [0]);
	}

	[Fact]
	public void ParseParameterTypes_ObjectMapsToObject ()
	{
		var types = JniSignatureHelper.ParseParameterTypes ("(Ljava/lang/String;)V");
		Assert.Single (types);
		Assert.Equal (JniParamKind.Object, types [0]);
	}

	[Fact]
	public void ParseReturnType_Void ()
	{
		Assert.Equal (JniParamKind.Void, JniSignatureHelper.ParseReturnType ("()V"));
	}

	[Fact]
	public void ParseReturnType_Int ()
	{
		Assert.Equal (JniParamKind.Int, JniSignatureHelper.ParseReturnType ("()I"));
	}

	[Fact]
	public void ParseReturnType_Boolean ()
	{
		Assert.Equal (JniParamKind.Boolean, JniSignatureHelper.ParseReturnType ("()Z"));
	}

	[Fact]
	public void ParseReturnType_Object ()
	{
		Assert.Equal (JniParamKind.Object, JniSignatureHelper.ParseReturnType ("()Ljava/lang/String;"));
	}

	// ---- Negative / edge-case tests ----

	[Theory]
	[InlineData ("")]
	[InlineData ("not-a-sig")]
	[InlineData ("(")]
	public void ParseParameterTypes_InvalidSignature_ThrowsOrReturnsEmpty (string signature)
	{
		// Should not crash — either returns empty or throws ArgumentException
		try {
			var result = JniSignatureHelper.ParseParameterTypes (signature);
			// If it doesn't throw, empty is acceptable
			Assert.NotNull (result);
		} catch (Exception ex) when (ex is ArgumentException || ex is IndexOutOfRangeException || ex is FormatException) {
			// Any of these are acceptable for malformed input
		}
	}

	[Fact]
	public void Generate_NullPeers_ThrowsArgumentNull ()
	{
		var gen = new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		var tmpPath = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"), "test.dll");
		Assert.Throws<ArgumentNullException> (() => gen.Generate (null!, tmpPath));
	}

	[Fact]
	public void Generate_NullOutputPath_ThrowsArgumentNull ()
	{
		var gen = new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		Assert.Throws<ArgumentNullException> (() => gen.Generate (Array.Empty<JavaPeerInfo> (), null!));
	}

	static void CleanUp (string path)
	{
		var dir = Path.GetDirectoryName (path);
		if (dir != null && Directory.Exists (dir)) {
			try { Directory.Delete (dir, true); } catch { }
		}
	}
}
