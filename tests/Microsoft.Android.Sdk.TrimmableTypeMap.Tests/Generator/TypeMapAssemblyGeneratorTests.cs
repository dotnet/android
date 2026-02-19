using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class TypeMapAssemblyGeneratorTests : FixtureTestBase
{
	static string GenerateAssembly (IReadOnlyList<JavaPeerInfo> peers, string outputDir, string? assemblyName = null)
	{
		var outputPath = Path.Combine (outputDir, (assemblyName ?? "TestTypeMap") + ".dll");
		var generator = new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		generator.Generate (peers, outputPath, assemblyName);
		return outputPath;
	}

	static (PEReader pe, MetadataReader reader) OpenAssembly (string path)
	{
		var pe = new PEReader (File.OpenRead (path));
		return (pe, pe.GetMetadataReader ());
	}

	static List<string> GetMemberRefNames (MetadataReader reader) =>
		Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
			.Select (m => reader.GetString (m.Name))
			.ToList ();

	static List<string> GetTypeRefNames (MetadataReader reader) =>
		reader.TypeReferences
			.Select (h => reader.GetTypeReference (h))
			.Select (t => reader.GetString (t.Name))
			.ToList ();

	public class BasicAssemblyStructure : IDisposable
	{
		readonly string _outputDir = CreateTempDir ();
		public void Dispose () => DeleteTempDir (_outputDir);

		[Fact]
		public void Generate_ProducesValidPEAssembly ()
		{
			var peers = ScanFixtures ();
			var path = GenerateAssembly (peers, _outputDir);
			Assert.True (File.Exists (path));
			using var pe = new PEReader (File.OpenRead (path));
			Assert.True (pe.HasMetadata);
			var reader = pe.GetMetadataReader ();
			Assert.NotNull (reader);
		}

	}

	public class AssemblyReference : IDisposable
	{
		readonly string _outputDir = CreateTempDir ();
		public void Dispose () => DeleteTempDir (_outputDir);

		[Fact]
		public void Generate_HasRequiredAssemblyReferences ()
		{
			var peers = ScanFixtures ();
			var path = GenerateAssembly (peers, _outputDir);
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
		}

	}

	public class ProxyType : IDisposable
	{
		readonly string _outputDir = CreateTempDir ();
		public void Dispose () => DeleteTempDir (_outputDir);

		[Fact]
		public void Generate_CreatesProxyTypes ()
		{
			var peers = ScanFixtures ();
			var path = GenerateAssembly (peers, _outputDir);
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var proxyTypes = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
					.ToList ();

				Assert.NotEmpty (proxyTypes);
				Assert.Contains (proxyTypes, t => reader.GetString (t.Name) == "Java_Lang_Object_Proxy");
			}
		}

		[Fact]
		public void Generate_ProxyType_HasCtorAndCreateInstance ()
		{
			var peers = ScanFixtures ();
			var path = GenerateAssembly (peers, _outputDir);
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
		}

	}

public class AcwProxy : IDisposable
{
readonly string _outputDir = CreateTempDir ();
public void Dispose () => DeleteTempDir (_outputDir);

[Fact]
public void Generate_AcwProxy_HasRegisterNativesAndUcoMethods ()
{
var peers = ScanFixtures ();
var acwPeer = peers.First (p => p.JavaName == "my/app/TouchHandler");
var path = GenerateAssembly (new [] { acwPeer }, _outputDir, "AcwTest");
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
Assert.Contains (methods, m => m.StartsWith ("n_") && m.EndsWith ("_uco_0"));
}
}

[Fact]
public void Generate_AcwProxy_HasUnmanagedCallersOnlyAttribute ()
{
var peers = ScanFixtures ();
var acwPeer = peers.First (p => p.JavaName == "my/app/TouchHandler");
var path = GenerateAssembly (new [] { acwPeer }, _outputDir, "UcoTest");
var (pe, reader) = OpenAssembly (path);
using (pe) {
var proxy = reader.TypeDefinitions
.Select (h => reader.GetTypeDefinition (h))
.First (t => reader.GetString (t.Name) == "MyApp_TouchHandler_Proxy");

var ucoMethod = proxy.GetMethods ()
.Select (h => reader.GetMethodDefinition (h))
.First (m => reader.GetString (m.Name).Contains ("_uco_"));

var attrNames = ucoMethod.GetCustomAttributes ()
.Select (h => reader.GetCustomAttribute (h))
.Select (a => {
var ctorHandle = (MemberReferenceHandle) a.Constructor;
var ctor = reader.GetMemberReference (ctorHandle);
var typeRef = reader.GetTypeReference ((TypeReferenceHandle) ctor.Parent);
return $"{reader.GetString (typeRef.Namespace)}.{reader.GetString (typeRef.Name)}";
})
.ToList ();
Assert.Contains ("System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute", attrNames);
}
}

}

public class IgnoresAccessChecksTo : IDisposable
	{
		readonly string _outputDir = CreateTempDir ();
		public void Dispose () => DeleteTempDir (_outputDir);

		[Fact]
		public void Generate_HasIgnoresAccessChecksToAttribute ()
		{
			var peers = ScanFixtures ();
			var path = GenerateAssembly (peers, _outputDir);
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var types = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.ToList ();
				Assert.Contains (types, t =>
					reader.GetString (t.Name) == "IgnoresAccessChecksToAttribute" &&
					reader.GetString (t.Namespace) == "System.Runtime.CompilerServices");
			}
		}

	}

	public class Alias : IDisposable
	{
		readonly string _outputDir = CreateTempDir ();
		public void Dispose () => DeleteTempDir (_outputDir);

		static List<JavaPeerInfo> MakeDuplicateAliasPeers () => new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "test/Duplicate",
				ManagedTypeName = "Test.Duplicate1",
				ManagedTypeNamespace = "Test",
				ManagedTypeShortName = "Duplicate1",
				AssemblyName = "TestAssembly",
				ActivationCtor = new ActivationCtorInfo {
					DeclaringTypeName = "Test.Duplicate1",
					DeclaringAssemblyName = "TestAssembly",
					Style = ActivationCtorStyle.XamarinAndroid,
				},
			},
			new JavaPeerInfo {
				JavaName = "test/Duplicate",
				ManagedTypeName = "Test.Duplicate2",
				ManagedTypeNamespace = "Test",
				ManagedTypeShortName = "Duplicate2",
				AssemblyName = "TestAssembly",
			},
		};

		[Fact]
		public void Generate_DuplicateJniNames_CreatesAliasEntries ()
		{
			var peers = MakeDuplicateAliasPeers ();
			var path = GenerateAssembly (peers, _outputDir, "AliasTest");
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var assemblyAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				Assert.True (assemblyAttrs.Count () >= 3);
			}
		}

		[Fact]
		public void Generate_DuplicateJniNames_EmitsTypeMapAssociationAttribute ()
		{
			var peers = MakeDuplicateAliasPeers ();
			var path = GenerateAssembly (peers, _outputDir, "AliasAssocTest");
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var memberRefs = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
					.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
					.Where (m => reader.GetString (m.Name) == ".ctor")
					.ToList ();

				var typeNames = GetTypeRefNames (reader);
				Assert.Contains ("TypeMapAssociationAttribute", typeNames);
			}
		}

	}

	public class EmptyInput : IDisposable
	{
		readonly string _outputDir = CreateTempDir ();
		public void Dispose () => DeleteTempDir (_outputDir);

		[Fact]
		public void Generate_EmptyPeerList_ProducesValidAssembly ()
		{
			var path = GenerateAssembly (Array.Empty<JavaPeerInfo> (), _outputDir, "EmptyTest");
			Assert.True (File.Exists (path));
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				Assert.NotNull (reader);
				var asmDef = reader.GetAssemblyDefinition ();
				Assert.Equal ("EmptyTest", reader.GetString (asmDef.Name));
			}
		}

	}

public class JniSignatureHelperTests
{

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

[Theory]
[InlineData ("(Z)V", JniParamKind.Boolean)]
[InlineData ("(Ljava/lang/String;)V", JniParamKind.Object)]
public void ParseParameterTypes_SingleParam_MapsToCorrectKind (string signature, JniParamKind expectedKind)
{
var types = JniSignatureHelper.ParseParameterTypes (signature);
Assert.Single (types);
Assert.Equal (expectedKind, types [0]);
}

[Theory]
[InlineData ("()V", JniParamKind.Void)]
[InlineData ("()I", JniParamKind.Int)]
[InlineData ("()Z", JniParamKind.Boolean)]
[InlineData ("()Ljava/lang/String;", JniParamKind.Object)]
public void ParseReturnType_MapsToCorrectKind (string signature, JniParamKind expectedKind)
{
Assert.Equal (expectedKind, JniSignatureHelper.ParseReturnType (signature));
}

}

public class NegativeEdgeCase
{

[Fact]
public void ParseParameterTypes_EmptyString_ReturnsEmptyList ()
{
Assert.Empty (JniSignatureHelper.ParseParameterTypes (""));
}

[Fact]
public void ParseParameterTypes_InvalidSignature_Throws ()
{
Assert.ThrowsAny<ArgumentException> (() => JniSignatureHelper.ParseParameterTypes ("not-a-sig"));
}

[Fact]
public void ParseParameterTypes_UnterminatedSignature_ReturnsEmptyList ()
{
Assert.Empty (JniSignatureHelper.ParseParameterTypes ("("));
}

}

public class CreateInstancePaths : IDisposable
	{
		readonly string _outputDir = CreateTempDir ();
		public void Dispose () => DeleteTempDir (_outputDir);

		[Fact]
		public void Generate_SimpleActivity_UsesGetUninitializedObject ()
		{
			var peers = ScanFixtures ();
			var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");
			Assert.NotNull (simpleActivity.ActivationCtor);
			Assert.NotEqual (simpleActivity.ManagedTypeName, simpleActivity.ActivationCtor.DeclaringTypeName);

			var path = GenerateAssembly (new [] { simpleActivity }, _outputDir, "InheritedCtorTest");
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var typeNames = GetTypeRefNames (reader);
				Assert.Contains ("RuntimeHelpers", typeNames);

				var memberNames = GetMemberRefNames (reader);
				Assert.DoesNotContain ("CreateManagedPeer", memberNames);
				Assert.Contains ("GetUninitializedObject", memberNames);
			}
		}

		[Fact]
		public void Generate_LeafCtor_DoesNotUseCreateManagedPeer ()
		{
			var peers = ScanFixtures ();
			// ClickableView has its own (IntPtr, JniHandleOwnership) ctor
			var clickableView = peers.First (p => p.JavaName == "my/app/ClickableView");
			Assert.NotNull (clickableView.ActivationCtor);
			Assert.Equal (clickableView.ManagedTypeName, clickableView.ActivationCtor.DeclaringTypeName);

			var path = GenerateAssembly (new [] { clickableView }, _outputDir, "LeafCtorTest");
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var memberNames = GetMemberRefNames (reader);
				Assert.DoesNotContain ("CreateManagedPeer", memberNames);

				var ctorRefs = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
					.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
					.Where (m => reader.GetString (m.Name) == ".ctor")
					.ToList ();
				Assert.True (ctorRefs.Count >= 2, "Should have ctor refs for proxy base + target type");
			}
		}

		[Fact]
		public void Generate_GenericType_ThrowsNotSupportedException ()
		{
			var peers = ScanFixtures ();
			var generic = peers.First (p => p.JavaName == "my/app/GenericHolder");
			Assert.True (generic.IsGenericDefinition);

			var path = GenerateAssembly (new [] { generic }, _outputDir, "GenericTest");
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var typeNames = GetTypeRefNames (reader);
				Assert.Contains ("NotSupportedException", typeNames);
			}
		}

	}

	public class IgnoresAccessChecksToForBaseCtor : IDisposable
	{
		readonly string _outputDir = CreateTempDir ();
		public void Dispose () => DeleteTempDir (_outputDir);

		[Fact]
		public void Generate_InheritedCtor_IncludesBaseCtorAssembly ()
		{
			// SimpleActivity inherits activation ctor from Activity — both in TestFixtures
			// but the generated assembly is "IgnoresAccessTest", so TestFixtures must be
			// in IgnoresAccessChecksTo
			var peers = ScanFixtures ();
			var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");

			var path = GenerateAssembly (new [] { simpleActivity }, _outputDir, "IgnoresAccessTest");
			var (pe, reader) = OpenAssembly (path);
			using (pe) {
				var ignoresAttrType = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.FirstOrDefault (t => reader.GetString (t.Name) == "IgnoresAccessChecksToAttribute");
				Assert.True (ignoresAttrType.Attributes != 0, "IgnoresAccessChecksToAttribute should be defined");

				var assemblyAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				var attrBlobs = new List<string> ();
				foreach (var attrHandle in assemblyAttrs) {
					var attr = reader.GetCustomAttribute (attrHandle);
					var blob = reader.GetBlobBytes (attr.Value);
					var blobStr = System.Text.Encoding.UTF8.GetString (blob);
					attrBlobs.Add (blobStr);
				}
				// Activity is in TestFixtures, so IgnoresAccessChecksTo must include TestFixtures
				Assert.Contains (attrBlobs, b => b.Contains ("TestFixtures"));
			}
		}

	}

}