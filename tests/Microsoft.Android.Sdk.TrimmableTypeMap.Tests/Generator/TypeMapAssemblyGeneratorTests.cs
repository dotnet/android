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
	static MemoryStream GenerateAssembly (IReadOnlyList<JavaPeerInfo> peers, string? assemblyName = null)
	{
		var stream = new MemoryStream ();
		var generator = new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		generator.Generate (peers, stream, assemblyName ?? "TestTypeMap");
		stream.Position = 0;
		return stream;
	}

	static (PEReader pe, MetadataReader reader) OpenAssembly (Stream stream)
	{
		var pe = new PEReader (stream);
		return (pe, pe.GetMetadataReader ());
	}

	public class BasicAssemblyStructure : FixtureTestBase
	{

		[Fact]
		public void Generate_ProducesValidPEAssembly ()
		{
			var peers = ScanFixtures ();
			using var stream = GenerateAssembly (peers);
			var (pe, reader) = OpenAssembly (stream);
			using (pe) {
				Assert.True (pe.HasMetadata);
				Assert.NotNull (reader);
			}
		}

	}

	public class AssemblyReference : FixtureTestBase
	{

		[Fact]
		public void Generate_HasRequiredAssemblyReferences ()
		{
			var peers = ScanFixtures ();
			using var stream = GenerateAssembly (peers);
			var (pe, reader) = OpenAssembly (stream);
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

	public class ProxyType : FixtureTestBase
	{

		[Fact]
		public void Generate_CreatesProxyTypes ()
		{
			var peers = ScanFixtures ();
			using var stream = GenerateAssembly (peers);
			var (pe, reader) = OpenAssembly (stream);
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
			using var stream = GenerateAssembly (peers);
			var (pe, reader) = OpenAssembly (stream);
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

	public class AcwProxy : FixtureTestBase
	{

		[Fact]
		public void Generate_AcwProxy_HasRegisterNativesAndUcoMethods ()
		{
			var peers = ScanFixtures ();
			var acwPeer = peers.First (p => p.JavaName == "my/app/TouchHandler");
			using var stream = GenerateAssembly (new [] { acwPeer }, "AcwTest");
			var (pe, reader) = OpenAssembly (stream);
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
			using var stream = GenerateAssembly (new [] { acwPeer }, "UcoTest");
			var (pe, reader) = OpenAssembly (stream);
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

	public class IgnoresAccessChecksTo : FixtureTestBase
	{

		[Fact]
		public void Generate_HasIgnoresAccessChecksToAttribute ()
		{
			var peers = ScanFixtures ();
			using var stream = GenerateAssembly (peers);
			var (pe, reader) = OpenAssembly (stream);
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

	public class Alias : FixtureTestBase
	{

		static List<JavaPeerInfo> MakeDuplicateAliasPeers () => new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "test/Duplicate",
				CompatJniName = "test/Duplicate",
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
				CompatJniName = "test/Duplicate",
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
			using var stream = GenerateAssembly (peers, "AliasTest");
			var (pe, reader) = OpenAssembly (stream);
			using (pe) {
				var assemblyAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				Assert.True (assemblyAttrs.Count () >= 3);
			}
		}

		[Fact]
		public void Generate_DuplicateJniNames_EmitsTypeMapAssociationAttribute ()
		{
			var peers = MakeDuplicateAliasPeers ();
			using var stream = GenerateAssembly (peers, "AliasAssocTest");
			var (pe, reader) = OpenAssembly (stream);
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

	public class EmptyInput : FixtureTestBase
	{

		[Fact]
		public void Generate_EmptyPeerList_ProducesValidAssembly ()
		{
			using var stream = GenerateAssembly ([], "EmptyTest");
			var (pe, reader) = OpenAssembly (stream);
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
		[InlineData ("(Z)V", 1)]    // JniParamKind.Boolean
		[InlineData ("(Ljava/lang/String;)V", 9)]  // JniParamKind.Object
		public void ParseParameterTypes_SingleParam_MapsToCorrectKind (string signature, int expectedKind)
		{
			var types = JniSignatureHelper.ParseParameterTypes (signature);
			Assert.Single (types);
			Assert.Equal ((JniParamKind) expectedKind, types [0]);
		}

		[Theory]
		[InlineData ("()V", 0)]    // JniParamKind.Void
		[InlineData ("()I", 5)]    // JniParamKind.Int
		[InlineData ("()Z", 1)]    // JniParamKind.Boolean
		[InlineData ("()Ljava/lang/String;", 9)]  // JniParamKind.Object
		public void ParseReturnType_MapsToCorrectKind (string signature, int expectedKind)
		{
			Assert.Equal ((JniParamKind) expectedKind, JniSignatureHelper.ParseReturnType (signature));
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

	public class CreateInstancePaths : FixtureTestBase
	{

		[Fact]
		public void Generate_SimpleActivity_UsesGetUninitializedObject ()
		{
			var peers = ScanFixtures ();
			var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");
			Assert.NotNull (simpleActivity.ActivationCtor);
			Assert.NotEqual (simpleActivity.ManagedTypeName, simpleActivity.ActivationCtor.DeclaringTypeName);

			using var stream = GenerateAssembly (new [] { simpleActivity }, "InheritedCtorTest");
			var (pe, reader) = OpenAssembly (stream);
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

			using var stream = GenerateAssembly (new [] { clickableView }, "LeafCtorTest");
			var (pe, reader) = OpenAssembly (stream);
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

			using var stream = GenerateAssembly (new [] { generic }, "GenericTest");
			var (pe, reader) = OpenAssembly (stream);
			using (pe) {
				var typeNames = GetTypeRefNames (reader);
				Assert.Contains ("NotSupportedException", typeNames);
			}
		}

	}

	public class IgnoresAccessChecksToForBaseCtor : FixtureTestBase
	{

		[Fact]
		public void Generate_InheritedCtor_IncludesBaseCtorAssembly ()
		{
			// SimpleActivity inherits activation ctor from Activity — both in TestFixtures
			// but the generated assembly is "IgnoresAccessTest", so TestFixtures must be
			// in IgnoresAccessChecksTo
			var peers = ScanFixtures ();
			var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");

			using var stream = GenerateAssembly (new [] { simpleActivity }, "IgnoresAccessTest");
			var (pe, reader) = OpenAssembly (stream);
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

	public class EmitterBehavior
	{

		[Fact]
		public void Emit_CalledTwice_Throws ()
		{
			var model = ModelBuilder.Build ([], "Double.dll", "Double");
			var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
			emitter.Emit (model, new MemoryStream ());
			// MetadataBuilder.AddAssembly throws on second call (only one assembly definition per PE)
			Assert.ThrowsAny<Exception> (() => emitter.Emit (model, new MemoryStream ()));
		}

		[Fact]
		public void EmitBody_ILCallbackCallsAddMemberRef_SignatureNotCorrupted ()
		{
			// Regression test: EmitBody uses shared _sigBlob for the method signature.
			// If the emitIL callback calls AddMemberRef (which also uses _sigBlob),
			// the method signature must not be corrupted.
			var pe = new PEAssemblyBuilder (new Version (11, 0, 0, 0));
			pe.EmitPreamble ("SigTest", "SigTest.dll");

			var objectRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef,
				pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("Object"));

			// <Module> already defined; add a type to host the method
			pe.Metadata.AddTypeDefinition (
				System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class,
				pe.Metadata.GetOrAddString ("Test"),
				pe.Metadata.GetOrAddString ("MyType"),
				objectRef,
				MetadataTokens.FieldDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.Field) + 1),
				MetadataTokens.MethodDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.MethodDef) + 1));

			// EmitBody with an IL callback that calls AddMemberRef (clearing _sigBlob)
			pe.EmitBody ("TestMethod",
				MethodAttributes.Public | MethodAttributes.Static,
				sig => sig.MethodSignature ().Parameters (1,
					rt => rt.Void (),
					p => p.AddParameter ().Type ().Int32 ()),
				encoder => {
					// This AddMemberRef call clears and repopulates _sigBlob
					pe.AddMemberRef (objectRef, ".ctor",
						s => s.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }));
					encoder.OpCode (ILOpCode.Ret);
				});

			// If the sig blob was corrupted, the PE metadata will have a wrong signature.
			// Write and read back to verify.
			var stream = new MemoryStream ();
			pe.WritePE (stream);
			stream.Position = 0;

			using var peReader = new PEReader (stream);
			var reader = peReader.GetMetadataReader ();
			var methods = reader.TypeDefinitions
				.SelectMany (h => reader.GetTypeDefinition (h).GetMethods ())
				.Select (h => reader.GetMethodDefinition (h))
				.ToList ();

			var testMethod = methods.First (m => reader.GetString (m.Name) == "TestMethod");
			var sig = testMethod.DecodeSignature (SignatureTypeProvider.Instance, null);
			var paramType = Assert.Single (sig.ParameterTypes);
			Assert.Equal ("System.Int32", paramType);
		}

	}

	public class DeterminismTests : FixtureTestBase
	{

		[Fact]
		public void Generate_DifferentContent_ProducesDifferentMVIDs ()
		{
			var peer1 = MakePeerWithActivation ("test/TypeA", "Test.TypeA", "TestAsm");
			var peer2 = MakePeerWithActivation ("test/TypeB", "Test.TypeB", "TestAsm");

			using var stream1 = GenerateAssembly (new [] { peer1 }, "SameName");
			using var stream2 = GenerateAssembly (new [] { peer2 }, "SameName");

			var (pe1, reader1) = OpenAssembly (stream1);
			var (pe2, reader2) = OpenAssembly (stream2);
			using (pe1)
			using (pe2) {
				var mvid1 = reader1.GetGuid (reader1.GetModuleDefinition ().Mvid);
				var mvid2 = reader2.GetGuid (reader2.GetModuleDefinition ().Mvid);
				Assert.NotEqual (mvid1, mvid2);
			}
		}

		[Fact]
		public void Generate_IdenticalContent_ProducesIdenticalMVIDs ()
		{
			var peer = MakePeerWithActivation ("test/TypeA", "Test.TypeA", "TestAsm");

			using var stream1 = GenerateAssembly (new [] { peer }, "SameName");
			using var stream2 = GenerateAssembly (new [] { peer }, "SameName");

			var (pe1, reader1) = OpenAssembly (stream1);
			var (pe2, reader2) = OpenAssembly (stream2);
			using (pe1)
			using (pe2) {
				var mvid1 = reader1.GetGuid (reader1.GetModuleDefinition ().Mvid);
				var mvid2 = reader2.GetGuid (reader2.GetModuleDefinition ().Mvid);
				Assert.Equal (mvid1, mvid2);
			}
		}

	}

	public class JavaInteropActivation : FixtureTestBase
	{

		[Fact]
		public void Generate_JiStyleCtor_EmitsJavaInteropActivation ()
		{
			var peers = ScanFixtures ();
			var jiPeer = peers.First (p => p.JavaName == "my/app/JiStylePeer");
			Assert.NotNull (jiPeer.ActivationCtor);
			Assert.Equal (ActivationCtorStyle.JavaInterop, jiPeer.ActivationCtor.Style);

			using var stream = GenerateAssembly (new [] { jiPeer }, "JiStyleTest");
			var (pe, reader) = OpenAssembly (stream);
			using (pe) {
				// JI-style activation should emit JniObjectReference and JniObjectReferenceOptions type refs
				var typeNames = GetTypeRefNames (reader);
				Assert.Contains ("JniObjectReference", typeNames);
				Assert.Contains ("JniObjectReferenceOptions", typeNames);

				// The proxy still exists (with a TargetType property)
				var proxyTypes = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
					.ToList ();
				Assert.Single (proxyTypes);
			}
		}

		[Fact]
		public void Generate_JiStyleCtor_FirstParamIsByRef ()
		{
			var peers = ScanFixtures ();
			var jiPeer = peers.First (p => p.JavaName == "my/app/JiStylePeer");
			Assert.NotNull (jiPeer.ActivationCtor);
			Assert.Equal (ActivationCtorStyle.JavaInterop, jiPeer.ActivationCtor.Style);

			using var stream = GenerateAssembly (new [] { jiPeer }, "JiByRefTest");
			var (pe, reader) = OpenAssembly (stream);
			using (pe) {
				// Find the .ctor member reference whose parent type is the JI peer's declaring type
				var ctorRefs = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
					.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
					.Where (m => reader.GetString (m.Name) == ".ctor")
					.ToList ();

				// Decode each .ctor signature and find the JI-style one (2 params, first is byref JniObjectReference)
				bool foundByRefCtor = false;
				foreach (var ctor in ctorRefs) {
					var sig = ctor.DecodeMethodSignature (SignatureTypeProvider.Instance, null);
					if (sig.ParameterTypes.Length == 2 &&
						sig.ParameterTypes [0].Contains ("JniObjectReference")) {
						// The byref encoding should produce "Java.Interop.JniObjectReference&"
						Assert.True (sig.ParameterTypes [0].EndsWith ("&"),
							$"JI-style .ctor first param must be byref, got: {sig.ParameterTypes [0]}");
						foundByRefCtor = true;
					}
				}
				Assert.True (foundByRefCtor, "Expected to find a .ctor with byref JniObjectReference parameter");
			}
		}

		[Fact]
		public void Generate_JiStyleCtor_EmitsDeleteRefCall ()
		{
			var peers = ScanFixtures ();
			var jiPeer = peers.First (p => p.JavaName == "my/app/JiStylePeer");

			using var stream = GenerateAssembly (new [] { jiPeer }, "JiDeleteRefTest");
			var (pe, reader) = OpenAssembly (stream);
			using (pe) {
				// The JI-style activation path must emit a call to JNIEnv.DeleteRef(IntPtr, JniHandleOwnership)
				// to match the legacy TypeManager.CreateProxy behavior.
				var memberRefs = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
					.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
					.ToList ();

				var deleteRefRef = memberRefs.FirstOrDefault (m => reader.GetString (m.Name) == "DeleteRef");
				Assert.True (!deleteRefRef.Equals (default (MemberReference)),
					"JI-style activation must emit a DeleteRef member reference for JNI handle cleanup");

				// Verify it's on the JNIEnv type
				var parentTypeRef = reader.GetTypeReference ((TypeReferenceHandle) deleteRefRef.Parent);
				Assert.Equal ("JNIEnv", reader.GetString (parentTypeRef.Name));
				Assert.Equal ("Android.Runtime", reader.GetString (parentTypeRef.Namespace));
			}
		}

	}

}