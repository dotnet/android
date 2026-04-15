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
	static MemoryStream GenerateAssembly (IReadOnlyList<JavaPeerInfo> peers, string assemblyName = "TestTypeMap")
	{
		var stream = new MemoryStream ();
		var generator = new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0));
		generator.Generate (peers, stream, assemblyName);
		stream.Position = 0;
		return stream;
	}

	[Fact]
	public void Generate_ProducesValidPEAssembly ()
	{
		var peers = ScanFixtures ();
		using var stream = GenerateAssembly (peers);
		using var pe = new PEReader (stream);
		Assert.True (pe.HasMetadata);
		var reader = pe.GetMetadataReader ();
		Assert.NotNull (reader);
	}

	[Fact]
	public void Generate_HasRequiredAssemblyReferences ()
	{
		var peers = ScanFixtures ();
		using var stream = GenerateAssembly (peers);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var asmRefs = reader.AssemblyReferences
			.Select (h => reader.GetString (reader.GetAssemblyReference (h).Name))
			.ToList ();
		Assert.Contains ("System.Runtime", asmRefs);
		Assert.Contains ("Mono.Android", asmRefs);
		Assert.Contains ("Java.Interop", asmRefs);
		Assert.Contains ("System.Runtime.InteropServices", asmRefs);
	}

	[Fact]
	public void Generate_CreatesProxyTypes ()
	{
		var peers = ScanFixtures ();
		using var stream = GenerateAssembly (peers);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var proxyTypes = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
			.ToList ();

		Assert.NotEmpty (proxyTypes);
		Assert.Contains (proxyTypes, t => reader.GetString (t.Name) == "Java_Lang_Object_Proxy");
	}

	[Fact]
	public void Generate_ProxyType_HasCtorAndCreateInstance ()
	{
		var peers = ScanFixtures ();
		using var stream = GenerateAssembly (peers);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var objectProxy = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.First (t => reader.GetString (t.Name) == "Java_Lang_Object_Proxy");

		var methods = objectProxy.GetMethods ()
			.Select (h => reader.GetMethodDefinition (h))
			.Select (m => reader.GetString (m.Name))
			.ToList ();

		Assert.Contains (".ctor", methods);
		Assert.Contains ("CreateInstance", methods);
	}

	[Fact]
	public void Generate_ProxyType_UsesGenericJavaPeerProxyBase ()
	{
		var peers = ScanFixtures ();
		using var stream = GenerateAssembly (peers);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var proxyTypes = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
			.ToList ();

		Assert.NotEmpty (proxyTypes);
		Assert.All (proxyTypes, proxyType => {
			Assert.Equal (HandleKind.TypeSpecification, proxyType.BaseType.Kind);

			var baseTypeSpec = reader.GetTypeSpecification ((TypeSpecificationHandle) proxyType.BaseType);
			var baseTypeName = baseTypeSpec.DecodeSignature (SignatureTypeProvider.Instance, genericContext: null);

			Assert.StartsWith ("Java.Interop.JavaPeerProxy`1<", baseTypeName, StringComparison.Ordinal);
		});

		var objectProxy = proxyTypes.First (t => reader.GetString (t.Name) == "Java_Lang_Object_Proxy");
		var objectProxyBaseType = reader.GetTypeSpecification ((TypeSpecificationHandle) objectProxy.BaseType);
		Assert.Equal ("Java.Interop.JavaPeerProxy`1<Java.Lang.Object>",
			objectProxyBaseType.DecodeSignature (SignatureTypeProvider.Instance, genericContext: null));
	}

	[Fact]
	public void Generate_HasIgnoresAccessChecksToAttribute ()
	{
		var peers = ScanFixtures ();
		using var stream = GenerateAssembly (peers);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var types = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.ToList ();
		Assert.Contains (types, t =>
			reader.GetString (t.Name) == "IgnoresAccessChecksToAttribute" &&
			reader.GetString (t.Namespace) == "System.Runtime.CompilerServices");
	}

	[Fact]
	public void Generate_DuplicateJniNames_CreatesAliasEntriesAndAssociationAttribute ()
	{
		var peers = new List<JavaPeerInfo> {
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

		using var stream = GenerateAssembly (peers, "AliasTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var assemblyAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
		Assert.True (assemblyAttrs.Count () >= 3);

		var typeNames = GetTypeRefNames (reader);
		Assert.Contains (typeNames, name => name.StartsWith ("TypeMapAssociationAttribute", StringComparison.Ordinal));
	}

	[Fact]
	public void Generate_EmptyPeerList_ProducesValidAssembly ()
	{
		using var stream = GenerateAssembly ([], "EmptyTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		Assert.NotNull (reader);
		var asmDef = reader.GetAssemblyDefinition ();
		Assert.Equal ("EmptyTest", reader.GetString (asmDef.Name));
	}

	[Fact]
	public void Generate_SimpleActivity_UsesSharedActivationHelper ()
	{
		var peers = ScanFixtures ();
		var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");
		Assert.NotNull (simpleActivity.ActivationCtor);
		Assert.NotEqual (simpleActivity.ManagedTypeName, simpleActivity.ActivationCtor.DeclaringTypeName);

		using var stream = GenerateAssembly (new [] { simpleActivity }, "InheritedCtorTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var memberNames = GetMemberRefNames (reader);
		Assert.DoesNotContain ("CreateManagedPeer", memberNames);
		Assert.Contains ("CreateUninitializedInstance", memberNames);
		Assert.DoesNotContain ("GetUninitializedObject", memberNames);
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
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var memberNames = GetMemberRefNames (reader);
		Assert.DoesNotContain ("CreateManagedPeer", memberNames);

		var ctorRefs = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
			.Where (m => reader.GetString (m.Name) == ".ctor")
			.ToList ();
		Assert.True (ctorRefs.Count >= 2, "Should have ctor refs for proxy base + target type");
	}

	[Fact]
	public void Generate_InheritedCtor_UcoUsesGuardAndInlinedActivation ()
	{
		var peers = ScanFixtures ();
		var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");
		Assert.NotNull (simpleActivity.ActivationCtor);
		Assert.NotEqual (simpleActivity.ManagedTypeName, simpleActivity.ActivationCtor.DeclaringTypeName);

		using var stream = GenerateAssembly (new [] { simpleActivity }, "InheritedCtorUcoTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var memberNames = GetMemberRefNames (reader);

		Assert.Contains ("get_WithinNewObjectScope", memberNames);
		Assert.Contains ("CreateUninitializedInstance", memberNames);
		Assert.DoesNotContain ("GetUninitializedObject", memberNames);
		Assert.DoesNotContain ("ActivateInstance", memberNames);
		Assert.DoesNotContain ("ActivatePeerFromJavaConstructor", memberNames);
	}

	[Fact]
	public void Generate_GenericType_ThrowsNotSupportedException ()
	{
		var peers = ScanFixtures ();
		var generic = peers.First (p => p.JavaName == "my/app/GenericHolder");
		Assert.True (generic.IsGenericDefinition);

		using var stream = GenerateAssembly (new [] { generic }, "GenericTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var typeNames = GetTypeRefNames (reader);
		var generatedTypeNames = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.Select (t => reader.GetString (t.Name))
			.ToList ();
		Assert.Contains ("NotSupportedException", typeNames);
		Assert.Contains ("MyApp_Generic_GenericHolder_1_Proxy", generatedTypeNames);
		Assert.DoesNotContain (generatedTypeNames, name => name.Contains ('`'));
	}

	[Fact]
	public void Generate_InheritedCtor_IncludesBaseCtorAssembly ()
	{
		// SimpleActivity inherits activation ctor from Activity — both in TestFixtures
		// but the generated assembly is "IgnoresAccessTest", so TestFixtures must be
		// in IgnoresAccessChecksTo
		var peers = ScanFixtures ();
		var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");

		using var stream = GenerateAssembly (new [] { simpleActivity }, "IgnoresAccessTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
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

	[Fact]
	public void Generate_JiStyleCtor_EmitsJavaInteropActivation ()
	{
		var peers = ScanFixtures ();
		var jiPeer = peers.First (p => p.JavaName == "my/app/JiStylePeer");
		Assert.NotNull (jiPeer.ActivationCtor);
		Assert.Equal (ActivationCtorStyle.JavaInterop, jiPeer.ActivationCtor.Style);

		using var stream = GenerateAssembly (new [] { jiPeer }, "JiStyleTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

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

	[Fact]
	public void Generate_JiStyleCtor_FirstParamIsByRef ()
	{
		var peers = ScanFixtures ();
		var jiPeer = peers.First (p => p.JavaName == "my/app/JiStylePeer");
		Assert.Equal (ActivationCtorStyle.JavaInterop, jiPeer.ActivationCtor!.Style);

		using var stream = GenerateAssembly (new [] { jiPeer }, "JiByRefTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

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

	[Fact]
	public void Generate_JiStyleCtor_EmitsDeleteRefCall ()
	{
		var peers = ScanFixtures ();
		var jiPeer = peers.First (p => p.JavaName == "my/app/JiStylePeer");

		using var stream = GenerateAssembly (new [] { jiPeer }, "JiDeleteRefTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		// The JI-style activation path must emit a call to JNIEnv.DeleteRef(IntPtr, JniHandleOwnership)
		// to match the legacy TypeManager.CreateProxy behavior.
		var memberRefs = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
			.ToList ();

		var deleteRefRef = memberRefs.FirstOrDefault (m => reader.GetString (m.Name) == "DeleteRef");
		Assert.True (!deleteRefRef.Equals (default (MemberReference)),
			"JI-style activation must emit a DeleteRef member reference for JNI handle cleanup");

		// Verify it's on the JNIEnv type
		var parentTypeRef = reader.GetTypeReference ((TypeReferenceHandle)deleteRefRef.Parent);
		Assert.Equal ("JNIEnv", reader.GetString (parentTypeRef.Name));
		Assert.Equal ("Android.Runtime", reader.GetString (parentTypeRef.Namespace));
	}

	[Fact]
	public void Generate_DifferentContent_ProducesDifferentMVIDs ()
	{
		var peer1 = MakePeerWithActivation ("test/TypeA", "Test.TypeA", "TestAsm");
		var peer2 = MakePeerWithActivation ("test/TypeB", "Test.TypeB", "TestAsm");

		using var stream1 = GenerateAssembly (new [] { peer1 }, "SameName");
		using var stream2 = GenerateAssembly (new [] { peer2 }, "SameName");

		using var pe1 = new PEReader (stream1);
		using var pe2 = new PEReader (stream2);
		var mvid1 = pe1.GetMetadataReader ().GetGuid (pe1.GetMetadataReader ().GetModuleDefinition ().Mvid);
		var mvid2 = pe2.GetMetadataReader ().GetGuid (pe2.GetMetadataReader ().GetModuleDefinition ().Mvid);

		Assert.NotEqual (mvid1, mvid2);
	}

	[Fact]
	public void Generate_IdenticalContent_ProducesIdenticalMVIDs ()
	{
		var peer = MakePeerWithActivation ("test/TypeA", "Test.TypeA", "TestAsm");

		using var stream1 = GenerateAssembly (new [] { peer }, "SameName");
		using var stream2 = GenerateAssembly (new [] { peer }, "SameName");

		using var pe1 = new PEReader (stream1);
		using var pe2 = new PEReader (stream2);
		var mvid1 = pe1.GetMetadataReader ().GetGuid (pe1.GetMetadataReader ().GetModuleDefinition ().Mvid);
		var mvid2 = pe2.GetMetadataReader ().GetGuid (pe2.GetMetadataReader ().GetModuleDefinition ().Mvid);

		Assert.Equal (mvid1, mvid2);
	}

	[Fact]
	public void Generate_AcwProxy_HasRegisterNativesAndUcoMethods ()
	{
		var peers = ScanFixtures ();
		var acwPeer = peers.First (p => p.JavaName == "my/app/MainActivity");
		Assert.False (acwPeer.DoNotGenerateAcw);
		Assert.True (acwPeer.MarshalMethods.Count > 0, "ACW peer should have marshal methods");

		using var stream = GenerateAssembly (new [] { acwPeer }, "AcwTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var proxyType = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.Single (t =>
				reader.GetString (t.Namespace) == "_TypeMap.Proxies" &&
				reader.GetString (t.Name) == "MyApp_MainActivity_Proxy");
		var proxyMethodNames = proxyType.GetMethods ()
			.Select (h => reader.GetMethodDefinition (h))
			.Select (m => reader.GetString (m.Name))
			.ToList ();
		Assert.Contains ("RegisterNatives", proxyMethodNames);
		Assert.Contains (proxyMethodNames, name => name.Contains ("_uco_"));

		var privateImplDetailsType = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.Single (t => reader.GetString (t.Name) == "<PrivateImplementationDetails>");
		var privateImplMethodNames = privateImplDetailsType.GetMethods ()
			.Select (h => reader.GetMethodDefinition (h))
			.Select (m => reader.GetString (m.Name))
			.ToList ();
		Assert.DoesNotContain ("RegisterNatives", privateImplMethodNames);
	}

	[Fact]
	public void Generate_AcwProxy_HasUnmanagedCallersOnlyAttribute ()
	{
		var peers = ScanFixtures ();
		var acwPeer = peers.First (p => p.JavaName == "my/app/MainActivity");

		using var stream = GenerateAssembly (new [] { acwPeer }, "UcoTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var typeNames = GetTypeRefNames (reader);
		Assert.Contains ("UnmanagedCallersOnlyAttribute", typeNames);

		// Verify UCO wrapper methods exist — they should have names like n_<method>_uco_<index>
		var methodDefs = reader.MethodDefinitions
			.Select (h => reader.GetMethodDefinition (h))
			.Select (m => reader.GetString (m.Name))
			.ToList ();
		Assert.Contains (methodDefs, name => name.Contains ("_uco_"));
	}

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

	[Fact]
	public void Generate_AcwProxy_UsesJniNativeMethodDirectly ()
	{
		var peers = ScanFixtures ();
		var acwPeer = peers.First (p => p.JavaName == "my/app/MainActivity");

		using var stream = GenerateAssembly (new [] { acwPeer }, "DirectRegisterTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var memberNames = GetMemberRefNames (reader);
		var typeNames = GetTypeRefNames (reader);

		// Should reference JniNativeMethod and RegisterNatives directly
		Assert.Contains ("JniNativeMethod", typeNames);
		Assert.Contains ("Types", typeNames); // JniEnvironment.Types nested type
		Assert.Contains ("RegisterNatives", memberNames);
		Assert.Contains ("get_PeerReference", memberNames);

		// Should NOT reference the old RegisterMethod helper
		Assert.DoesNotContain ("RegisterMethod", memberNames);
	}

	[Fact]
	public void Generate_AcwProxy_HasPrivateImplementationDetails ()
	{
		var peers = ScanFixtures ();
		var acwPeer = peers.First (p => p.JavaName == "my/app/MainActivity");

		using var stream = GenerateAssembly (new [] { acwPeer }, "PrivImplTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var typeDefNames = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.Select (t => reader.GetString (t.Name))
			.ToList ();

		Assert.Contains ("<PrivateImplementationDetails>", typeDefNames);
	}

	[Fact]
	public void Generate_MultipleAcwProxies_DeduplicatesUtf8Strings ()
	{
		var peers = ScanFixtures ();
		// Get all ACW peers — they likely share signatures like "()V"
		var acwPeers = peers.Where (p => !p.DoNotGenerateAcw && p.MarshalMethods.Count > 0).ToList ();
		Assert.True (acwPeers.Count >= 2, "Need at least 2 ACW peers to test deduplication");

		using var stream = GenerateAssembly (acwPeers, "DedupTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		// Count fields with HasFieldRVA — these are our UTF-8 RVA fields.
		// With deduplication, common strings like "()V" should appear only once.
		var rvaFields = reader.FieldDefinitions
			.Select (h => reader.GetFieldDefinition (h))
			.Where (f => (f.Attributes & FieldAttributes.HasFieldRVA) != 0)
			.ToList ();

		// Collect all JNI method names and signatures from the ACW peers
		var allStrings = acwPeers
			.SelectMany (p => p.MarshalMethods)
			.SelectMany (m => new [] { m.JniName, m.JniSignature })
			.ToList ();
		var uniqueStrings = allStrings.Distinct ().Count ();

		// With dedup, RVA field count should equal unique string count, not total string count.
		// Also include constructor registrations (nctor_*), so use <= for a safe assertion.
		Assert.True (rvaFields.Count <= uniqueStrings + acwPeers.Count * 2,
			$"Expected at most {uniqueStrings + acwPeers.Count * 2} RVA fields (unique strings + ctor names/sigs), " +
			$"but found {rvaFields.Count}. Deduplication may not be working.");

		// The key assertion: fewer RVA fields than total strings means dedup is working
		if (allStrings.Count > uniqueStrings) {
			Assert.True (rvaFields.Count < allStrings.Count,
				$"Expected fewer RVA fields ({rvaFields.Count}) than total strings ({allStrings.Count}) due to deduplication");
		}
	}
}
