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

	static MethodDefinitionHandle FindMethodDefinition (MetadataReader reader, string methodName) =>
		reader.MethodDefinitions.First (h => reader.GetString (reader.GetMethodDefinition (h).Name) == methodName);

	static List<MemberReferenceHandle> FindCtorMemberRefs (MetadataReader reader, string parentNamespace, string parentName, params string [] parameterTypes) =>
		Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (MetadataTokens.MemberReferenceHandle)
			.Where (h => {
				var member = reader.GetMemberReference (h);
				if (reader.GetString (member.Name) != ".ctor" || member.Parent.Kind != HandleKind.TypeReference)
					return false;

				var parent = reader.GetTypeReference ((TypeReferenceHandle) member.Parent);
				if (reader.GetString (parent.Namespace) != parentNamespace || reader.GetString (parent.Name) != parentName)
					return false;

				var signature = member.DecodeMethodSignature (SignatureTypeProvider.Instance, null);
				return signature.ParameterTypes.SequenceEqual (parameterTypes);
			})
			.ToList ();

	static MemberReferenceHandle FindCtorMemberRef (MetadataReader reader, string parentNamespace, string parentName, params string [] parameterTypes) =>
		FindCtorMemberRefs (reader, parentNamespace, parentName, parameterTypes).First ();

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
			switch (proxyType.BaseType.Kind) {
			case HandleKind.TypeSpecification:
				// Non-generic target types derive from the closed `JavaPeerProxy<T>`.
				var baseTypeSpec = reader.GetTypeSpecification ((TypeSpecificationHandle) proxyType.BaseType);
				var baseTypeName = baseTypeSpec.DecodeSignature (SignatureTypeProvider.Instance, genericContext: null);
				Assert.StartsWith ("Java.Interop.JavaPeerProxy`1<", baseTypeName, StringComparison.Ordinal);
				break;
			case HandleKind.TypeReference:
				// Open generic target types derive from the non-generic `JavaPeerProxy`.
				var baseTypeRef = reader.GetTypeReference ((TypeReferenceHandle) proxyType.BaseType);
				Assert.Equal ("Java.Interop", reader.GetString (baseTypeRef.Namespace));
				Assert.Equal ("JavaPeerProxy", reader.GetString (baseTypeRef.Name));
				break;
			default:
				Assert.Fail ($"Unexpected BaseType handle kind: {proxyType.BaseType.Kind}");
				break;
			}
		});

		var objectProxy = proxyTypes.First (t => reader.GetString (t.Name) == "Java_Lang_Object_Proxy");
		var objectProxyBaseType = reader.GetTypeSpecification ((TypeSpecificationHandle) objectProxy.BaseType);
		Assert.Equal ("Java.Interop.JavaPeerProxy`1<Java.Lang.Object>",
			objectProxyBaseType.DecodeSignature (SignatureTypeProvider.Instance, genericContext: null));
	}

	// Regression test: every generated proxy type must carry a custom attribute whose
	// constructor points at the proxy's own TypeDefinitionHandle (either as a MemberRef
	// parented on the TypeDef, or as a MethodDefinition on the TypeDef). This is how
	// JavaPeerProxy instances are resolved at runtime via
	// type.GetCustomAttribute<JavaPeerProxy>() — losing the self-application means the
	// runtime can't construct the proxy. This has regressed twice; keep it covered.
	[Fact]
	public void Generate_ProxyType_IsSelfAppliedAsCustomAttribute ()
	{
		var peers = ScanFixtures ();
		using var stream = GenerateAssembly (peers);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var proxyTypeHandles = reader.TypeDefinitions
			.Where (h => reader.GetString (reader.GetTypeDefinition (h).Namespace) == "_TypeMap.Proxies")
			.ToList ();

		Assert.NotEmpty (proxyTypeHandles);

		foreach (var proxyHandle in proxyTypeHandles) {
			var proxy = reader.GetTypeDefinition (proxyHandle);
			var proxyName = reader.GetString (proxy.Name);

			bool selfApplied = false;
			foreach (var caHandle in proxy.GetCustomAttributes ()) {
				var ca = reader.GetCustomAttribute (caHandle);

				switch (ca.Constructor.Kind) {
				case HandleKind.MemberReference:
					var ctorRef = reader.GetMemberReference ((MemberReferenceHandle) ca.Constructor);
					if (ctorRef.Parent.Kind == HandleKind.TypeDefinition &&
						(TypeDefinitionHandle) ctorRef.Parent == proxyHandle) {
						selfApplied = true;
					}
					break;
				case HandleKind.MethodDefinition:
					var ctorDef = reader.GetMethodDefinition ((MethodDefinitionHandle) ca.Constructor);
					if (ctorDef.GetDeclaringType () == proxyHandle) {
						selfApplied = true;
					}
					break;
				}

				if (selfApplied) {
					break;
				}
			}

			Assert.True (selfApplied,
				$"Proxy type '{proxyName}' is missing its self-applied custom attribute. " +
				"Every proxy must carry itself as a [JavaPeerProxy] attribute so the runtime " +
				"can instantiate it via Type.GetCustomAttribute<JavaPeerProxy> ().");
		}
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
	public void Generate_InheritedCtor_ReferencesGuardAndActivationCtor ()
	{
		var peers = ScanFixtures ();
		var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");
		Assert.NotNull (simpleActivity.ActivationCtor);
		Assert.NotEqual (simpleActivity.ManagedTypeName, simpleActivity.ActivationCtor.DeclaringTypeName);

		using var stream = GenerateAssembly (new [] { simpleActivity }, "InheritedCtorUcoTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var memberNames = GetMemberRefNames (reader);

		Assert.Contains ("ShouldSkipActivation", memberNames);
		Assert.Contains ("GetUninitializedObject", memberNames);
		Assert.DoesNotContain ("Invoke", memberNames);
		Assert.DoesNotContain ("ActivateInstance", memberNames);
		Assert.DoesNotContain ("ActivatePeerFromJavaConstructor", memberNames);

		Assert.NotEmpty (FindCtorMemberRefs (reader, "Android.App", "Activity",
			"System.IntPtr", "Android.Runtime.JniHandleOwnership"));
		var nctorMethodHandle = FindNctorUcoMethod (reader);
		Assert.False (nctorMethodHandle.IsNil, "SimpleActivity should have a nctor_*_uco method");
	}

	[Fact]
	public void Generate_InheritedJavaInteropCtor_ReferencesActivationCtor ()
	{
		var peer = MakeAcwPeer ("test/JiInheritedTarget", "Test.JiInheritedTarget", "TestAsm") with {
			ActivationCtor = new ActivationCtorInfo {
				DeclaringTypeName = "Test.JiInheritedBase",
				DeclaringAssemblyName = "TestAsm",
				Style = ActivationCtorStyle.JavaInterop,
			},
		};

		using var stream = GenerateAssembly (new [] { peer }, "InheritedJiCtorInlineTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var typeNames = GetTypeRefNames (reader);
		Assert.DoesNotContain ("MethodBase", typeNames);

		var memberNames = GetMemberRefNames (reader);
		Assert.Contains ("GetUninitializedObject", memberNames);
		Assert.DoesNotContain ("Invoke", memberNames);

		Assert.NotEmpty (FindCtorMemberRefs (reader, "Test", "JiInheritedBase",
			"Java.Interop.JniObjectReference&", "Java.Interop.JniObjectReferenceOptions"));
		var nctorMethodHandle = FindNctorUcoMethod (reader);
		Assert.False (nctorMethodHandle.IsNil, "The ACW peer should have a nctor_*_uco method");
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
	public void Generate_JiStyleInvoker_FirstParamIsByRef ()
	{
		var peer = MakeInterfacePeer ("test/IJiInvoker", "Test.IJiInvoker", "TestAsm", "Test.IJiInvokerInvoker") with {
			InvokerActivationCtorStyle = ActivationCtorStyle.JavaInterop,
		};

		using var stream = GenerateAssembly (new [] { peer }, "JiInvokerByRefTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var ctorRefs = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
			.Where (m => reader.GetString (m.Name) == ".ctor")
			.ToList ();

		bool foundByRefCtor = false;
		foreach (var ctor in ctorRefs) {
			var sig = ctor.DecodeMethodSignature (SignatureTypeProvider.Instance, null);
			if (sig.ParameterTypes.Length == 2 &&
				sig.ParameterTypes [0].Contains ("JniObjectReference")) {
				Assert.True (sig.ParameterTypes [0].EndsWith ("&"),
					$"JI-style invoker .ctor first param must be byref, got: {sig.ParameterTypes [0]}");
				foundByRefCtor = true;
			}
		}

		Assert.True (foundByRefCtor, "Expected to find a JI-style invoker .ctor with byref JniObjectReference parameter");
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
	[InlineData (1, 0x05)]  // Boolean → byte (unsigned) for JNI ABI
	[InlineData (2, 0x04)]  // Byte → sbyte
	[InlineData (3, 0x03)]  // Char → char
	[InlineData (4, 0x06)]  // Short → int16
	[InlineData (5, 0x08)]  // Int → int32
	[InlineData (6, 0x0A)]  // Long → int64
	[InlineData (7, 0x0C)]  // Float → float32
	[InlineData (8, 0x0D)]  // Double → float64
	[InlineData (9, 0x18)]  // Object → IntPtr
	public void EncodeClrType_ProducesCorrectPrimitiveTypeCode (int kindValue, byte expectedCode)
	{
		var kind = (JniParamKind) kindValue;
		var blob = new BlobBuilder ();
		JniSignatureHelper.EncodeClrType (new SignatureTypeEncoder (blob), kind);
		Assert.Equal (expectedCode, blob.ToArray () [0]);
	}

	[Theory]
	[InlineData (1, 0x04)]  // Boolean → sbyte — matches MCW n_* callbacks
	[InlineData (2, 0x04)]  // Byte → sbyte
	[InlineData (3, 0x03)]  // Char → char
	[InlineData (4, 0x06)]  // Short → int16
	[InlineData (5, 0x08)]  // Int → int32
	[InlineData (6, 0x0A)]  // Long → int64
	[InlineData (7, 0x0C)]  // Float → float32
	[InlineData (8, 0x0D)]  // Double → float64
	[InlineData (9, 0x18)]  // Object → IntPtr
	public void EncodeClrTypeForCallback_ProducesCorrectPrimitiveTypeCode (int kindValue, byte expectedCode)
	{
		var kind = (JniParamKind) kindValue;
		var blob = new BlobBuilder ();
		JniSignatureHelper.EncodeClrTypeForCallback (new SignatureTypeEncoder (blob), kind);
		Assert.Equal (expectedCode, blob.ToArray () [0]);
	}

	[Fact]
	public void EncodeClrType_Boolean_DiffersFromCallback ()
	{
		var ucoBlob = new BlobBuilder ();
		JniSignatureHelper.EncodeClrType (new SignatureTypeEncoder (ucoBlob), JniParamKind.Boolean);

		var cbBlob = new BlobBuilder ();
		JniSignatureHelper.EncodeClrTypeForCallback (new SignatureTypeEncoder (cbBlob), JniParamKind.Boolean);

		var ucoBytes = ucoBlob.ToArray ();
		var cbBytes = cbBlob.ToArray ();
		Assert.NotEqual (ucoBytes, cbBytes);
		Assert.Equal (0x05, ucoBytes [0]);  // byte (unsigned)
		Assert.Equal (0x04, cbBytes [0]);    // sbyte (signed)
	}

	[Fact]
	public void EncodeClrType_Void_Throws ()
	{
		var blob = new BlobBuilder ();
		Assert.ThrowsAny<ArgumentException> (() =>
			JniSignatureHelper.EncodeClrType (new SignatureTypeEncoder (blob), JniParamKind.Void));
	}

	[Fact]
	public void EncodeClrTypeForCallback_Void_Throws ()
	{
		var blob = new BlobBuilder ();
		Assert.ThrowsAny<ArgumentException> (() =>
			JniSignatureHelper.EncodeClrTypeForCallback (new SignatureTypeEncoder (blob), JniParamKind.Void));
	}

	[Theory]
	[InlineData (2)]  // Byte
	[InlineData (3)]  // Char
	[InlineData (4)]  // Short
	[InlineData (5)]  // Int
	[InlineData (6)]  // Long
	[InlineData (7)]  // Float
	[InlineData (8)]  // Double
	[InlineData (9)]  // Object
	public void EncodeClrType_NonBooleanTypes_IdenticalToCallback (int kindValue)
	{
		var kind = (JniParamKind) kindValue;
		var ucoBlob = new BlobBuilder ();
		JniSignatureHelper.EncodeClrType (new SignatureTypeEncoder (ucoBlob), kind);

		var cbBlob = new BlobBuilder ();
		JniSignatureHelper.EncodeClrTypeForCallback (new SignatureTypeEncoder (cbBlob), kind);

		Assert.Equal (ucoBlob.ToArray (), cbBlob.ToArray ());
	}

	[Fact]
	public void Generate_UcoMethod_BooleanReturn_WrapperUsesByte_CallbackUsesSByte ()
	{
		// Regression test: the UCO wrapper must use byte (unsigned, JNI ABI) for boolean,
		// but the callback MemberRef must use sbyte (signed, MCW convention).
		// A mismatch caused ILLink to fail resolving the member reference and trim n_* methods.
		var peer = FindFixtureByJavaName ("my/app/TouchHandler");
		using var stream = GenerateAssembly (new [] { peer }, "BoolReturnTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		// Find the UCO wrapper method for onTouch (returns Z → boolean)
		var ucoMethod = reader.MethodDefinitions
			.Select (h => reader.GetMethodDefinition (h))
			.First (m => reader.GetString (m.Name).Contains ("onTouch") &&
			             reader.GetString (m.Name).Contains ("_uco_"));
		var ucoSig = ucoMethod.DecodeSignature (SignatureTypeProvider.Instance, null);
		Assert.Equal ("System.Byte", ucoSig.ReturnType);

		// Find the callback MemberRef that the UCO wrapper calls (n_OnTouch on the TouchHandler type)
		var callbackRef = FindCallbackMemberRef (reader, "n_OnTouch");
		var callbackSig = callbackRef.DecodeMethodSignature (SignatureTypeProvider.Instance, null);
		Assert.Equal ("System.SByte", callbackSig.ReturnType);
	}

	[Fact]
	public void Generate_UcoMethod_BooleanParam_WrapperUsesByte_CallbackUsesSByte ()
	{
		// Regression test: boolean parameters must also use the correct encoding.
		var peer = FindFixtureByJavaName ("my/app/TouchHandler");
		using var stream = GenerateAssembly (new [] { peer }, "BoolParamTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		// Find the UCO wrapper for onFocusChange (takes Z as 3rd param → boolean parameter)
		var ucoMethod = reader.MethodDefinitions
			.Select (h => reader.GetMethodDefinition (h))
			.First (m => reader.GetString (m.Name).Contains ("onFocusChange") &&
			             reader.GetString (m.Name).Contains ("_uco_"));
		var ucoSig = ucoMethod.DecodeSignature (SignatureTypeProvider.Instance, null);
		// Params: IntPtr (jnienv), IntPtr (self), IntPtr (View object), byte (boolean)
		Assert.Equal ("System.Byte", ucoSig.ParameterTypes.Last ());

		// Find the callback MemberRef
		var callbackRef = FindCallbackMemberRef (reader, "n_OnFocusChange");
		var callbackSig = callbackRef.DecodeMethodSignature (SignatureTypeProvider.Instance, null);
		Assert.Equal ("System.SByte", callbackSig.ParameterTypes.Last ());
	}

	[Fact]
	public void Generate_UcoMethod_HasCatchRegionWithoutFinally ()
	{
		var peer = FindFixtureByJavaName ("my/app/TouchHandler");
		using var stream = GenerateAssembly (new [] { peer }, "UcoLegacyWrapperShape");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var ucoMethodHandle = reader.MethodDefinitions
			.First (h => {
				var method = reader.GetMethodDefinition (h);
				var name = reader.GetString (method.Name);
				return name.Contains ("onTouch") && name.Contains ("_uco_");
			});
		var ucoMethod = reader.GetMethodDefinition (ucoMethodHandle);
		var body = pe.GetMethodBody (ucoMethod.RelativeVirtualAddress);
		Assert.NotNull (body);
		Assert.Contains (body.ExceptionRegions, r => r.Kind == ExceptionRegionKind.Catch);
		Assert.DoesNotContain (body.ExceptionRegions, r => r.Kind == ExceptionRegionKind.Finally);
	}

	[Fact]
	public void Generate_UcoMethod_UsesDefaultUnmanagedCallersOnlyAttribute ()
	{
		var peer = FindFixtureByJavaName ("my/app/TouchHandler");
		using var stream = GenerateAssembly (new [] { peer }, "UcoDefaultAttribute");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var ucoMethodHandle = reader.MethodDefinitions
			.First (h => {
				var method = reader.GetMethodDefinition (h);
				var name = reader.GetString (method.Name);
				return name.Contains ("onTouch") && name.Contains ("_uco_");
			});
		var attrs = reader.GetCustomAttributes (ucoMethodHandle)
			.Select (h => reader.GetCustomAttribute (h))
			.Where (attr => attr.Constructor.Kind == HandleKind.MemberReference)
			.Where (attr => {
				var ctor = reader.GetMemberReference ((MemberReferenceHandle) attr.Constructor);
				if (ctor.Parent.Kind != HandleKind.TypeReference)
					return false;
				var type = reader.GetTypeReference ((TypeReferenceHandle) ctor.Parent);
				return reader.GetString (type.Name) == "UnmanagedCallersOnlyAttribute";
			})
			.ToList ();
		var ucoAttr = Assert.Single (attrs);
		Assert.Equal (new byte [] { 0x01, 0x00, 0x00, 0x00 }, reader.GetBlobBytes (ucoAttr.Value));
	}

	static MemberReference FindCallbackMemberRef (MetadataReader reader, string methodName)
	{
		var refs = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
			.Where (m => reader.GetString (m.Name) == methodName)
			.ToList ();
		Assert.Single (refs);
		return refs [0];
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

	[Fact]
	public void Generate_AliasGroup_ProducesCorrectIndexedEntries ()
	{
		var peers = ScanFixtures ();
		var aliasPeers = peers.Where (p => p.JavaName == "test/AliasTarget").ToList ();
		Assert.Equal (3, aliasPeers.Count);

		using var stream = GenerateAssembly (aliasPeers, "AliasRoundTrip");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		// Read all TypeMap attribute blobs
		var typeMapBlobs = new List<(string? jniName, string? proxyRef, string? targetRef)> ();
		var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
		foreach (var attrHandle in asmAttrs) {
			var attr = reader.GetCustomAttribute (attrHandle);
			if (attr.Constructor.Kind == HandleKind.MethodDefinition)
				continue;

			var blobReader = reader.GetBlobReader (attr.Value);
			ushort prolog = blobReader.ReadUInt16 ();
			if (prolog != 1)
				continue;

			string? val1 = blobReader.ReadSerializedString ();
			string? val2 = blobReader.ReadSerializedString ();

			// TypeMap has a jniName string arg; TypeMapAssociation has two Type args.
			// We distinguish by checking if val1 looks like a JNI name (contains '/').
			if (val1 is not null && val1.Contains ('/')) {
				string? val3 = blobReader.RemainingBytes > 2 ? blobReader.ReadSerializedString () : null;
				typeMapBlobs.Add ((val1, val2, val3));
			}
		}

		// Verify indexed entries: "test/AliasTarget[0]", "test/AliasTarget[1]", "test/AliasTarget[2]", and base "test/AliasTarget"
		var jniNames = typeMapBlobs.Select (b => b.jniName).ToList ();
		Assert.Contains ("test/AliasTarget", jniNames);
		Assert.Contains ("test/AliasTarget[0]", jniNames);
		Assert.Contains ("test/AliasTarget[1]", jniNames);
		Assert.Contains ("test/AliasTarget[2]", jniNames);

		// Verify TypeMapAssociationAttribute is referenced (generic version)
		var typeNames = GetTypeRefNames (reader);
		Assert.Contains ("TypeMapAssociationAttribute`1", typeNames);

		// Verify 3 proxy types + 1 alias holder were emitted
		var proxyTypes = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
			.ToList ();
		Assert.Equal (3, proxyTypes.Count);

		var aliasHolders = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Aliases")
			.ToList ();
		Assert.Single (aliasHolders);

		// Verify the alias holder has JavaPeerAliasesAttribute
		Assert.Contains ("JavaPeerAliasesAttribute", typeNames);
	}

	[Fact]
	public void Generate_AliasHolder_ExtendsObjectNotJavaPeerProxy ()
	{
		var peers = ScanFixtures ();
		var aliasPeers = peers.Where (p => p.JavaName == "test/AliasTarget").ToList ();

		using var stream = GenerateAssembly (aliasPeers, "AliasBaseType");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var aliasHolder = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.First (t => reader.GetString (t.Namespace) == "_TypeMap.Aliases");

		var baseTypeHandle = aliasHolder.BaseType;
		Assert.Equal (HandleKind.TypeReference, baseTypeHandle.Kind);
		var baseType = reader.GetTypeReference ((TypeReferenceHandle) baseTypeHandle);
		Assert.Equal ("Object", reader.GetString (baseType.Name));
		Assert.Equal ("System", reader.GetString (baseType.Namespace));
	}

	[Fact]
	public void Generate_AliasHolder_HasDeserializableAliasKeys ()
	{
		var peers = ScanFixtures ();
		var aliasPeers = peers.Where (p => p.JavaName == "test/AliasTarget").ToList ();

		using var stream = GenerateAssembly (aliasPeers, "AliasAttrBlob");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var aliasHolder = reader.TypeDefinitions
			.Select (h => reader.GetTypeDefinition (h))
			.First (t => reader.GetString (t.Namespace) == "_TypeMap.Aliases");

		var aliasHolderHandle = reader.TypeDefinitions
			.First (h => reader.GetString (reader.GetTypeDefinition (h).Namespace) == "_TypeMap.Aliases");

		// Read the JavaPeerAliasesAttribute blob from the alias holder's custom attributes
		var attrs = reader.GetCustomAttributes (aliasHolderHandle);
		Assert.NotEmpty (attrs);

		// Find the attribute blob and parse it
		foreach (var attrHandle in attrs) {
			var attr = reader.GetCustomAttribute (attrHandle);
			var blobReader = reader.GetBlobReader (attr.Value);
			ushort prolog = blobReader.ReadUInt16 ();
			Assert.Equal (1, prolog);

			// Read the params string[] — encoded as int32 count + serialized strings
			int count = blobReader.ReadInt32 ();
			Assert.Equal (3, count);

			var keys = new List<string> ();
			for (int i = 0; i < count; i++) {
				keys.Add (blobReader.ReadSerializedString ()!);
			}

			Assert.Contains ("test/AliasTarget[0]", keys);
			Assert.Contains ("test/AliasTarget[1]", keys);
			Assert.Contains ("test/AliasTarget[2]", keys);
		}
	}

	[Fact]
	public void Generate_UcoConstructor_HasMarshalMethodMetadataAndExceptionRegions ()
	{
		var peer = MakeAcwPeer ("test/UcoCtorExc", "Test.UcoCtorExc", "TestAsm");
		using var stream = GenerateAssembly (new [] { peer }, "UcoCtorMarshalTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		// Member refs for the marshal-method pattern must be present.
		var memberNames = GetMemberRefNames (reader);
		Assert.Contains ("BeginMarshalMethod", memberNames);
		Assert.Contains ("EndMarshalMethod", memberNames);
		Assert.Contains ("OnUserUnhandledException", memberNames);

		// Type refs for the marshal-method locals must be present.
		var typeNames = GetTypeRefNames (reader);
		Assert.Contains ("JniTransition", typeNames);
		Assert.Contains ("JniRuntime", typeNames);
		Assert.Contains ("Exception", typeNames);

		// Find the nctor_*_uco method.
		var nctorMethodHandle = FindNctorUcoMethod (reader);
		Assert.False (nctorMethodHandle.IsNil, "Expected a nctor_*_uco method in the generated assembly");

		// The method body must have exception regions: at least one Catch and one Finally.
		var nctorMethod = reader.GetMethodDefinition (nctorMethodHandle);
		var body = pe.GetMethodBody (nctorMethod.RelativeVirtualAddress);
		Assert.NotNull (body);
		var regions = body.ExceptionRegions;
		Assert.True (regions.Length >= 2,
			$"UCO constructor should have at least 2 exception regions (catch + finally), found {regions.Length}");
		Assert.Contains (regions, r => r.Kind == ExceptionRegionKind.Catch);
		Assert.Contains (regions, r => r.Kind == ExceptionRegionKind.Finally);
	}

	[Fact]
	public void Generate_UcoConstructor_JiStyle_HasExceptionRegions ()
	{
		// Verify the JavaInterop-style UCO constructor activation path also has exception regions.
		var peer = MakeAcwPeer ("test/JiUcoCtorExc", "Test.JiUcoCtorExc", "TestAsm") with {
			ActivationCtor = new ActivationCtorInfo {
				DeclaringTypeName = "Test.JiUcoCtorExc",
				DeclaringAssemblyName = "TestAsm",
				Style = ActivationCtorStyle.JavaInterop,
			},
		};
		using var stream = GenerateAssembly (new [] { peer }, "JiUcoCtorMarshalTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var nctorMethodHandle = FindNctorUcoMethod (reader);
		Assert.False (nctorMethodHandle.IsNil, "Expected a nctor_*_uco method in the generated assembly");

		var nctorMethod = reader.GetMethodDefinition (nctorMethodHandle);
		var body = pe.GetMethodBody (nctorMethod.RelativeVirtualAddress);
		Assert.NotNull (body);
		var regions = body.ExceptionRegions;
		Assert.True (regions.Length >= 2,
			$"JavaInterop UCO constructor should have at least 2 exception regions, found {regions.Length}");
		Assert.Contains (regions, r => r.Kind == ExceptionRegionKind.Catch);
		Assert.Contains (regions, r => r.Kind == ExceptionRegionKind.Finally);
	}

	[Fact]
	public void Generate_UcoConstructor_GenericDefinition_ThrowsWithMarshalMethodPattern ()
	{
		// Open-generic UCO constructors throw inside the same marshal-method wrapper used
		// by normal UCO constructors, so the exception is surfaced through
		// JniRuntime.OnUserUnhandledException instead of crossing the JNI boundary.
		var generic = MakeAcwPeer ("test/GenericHolder", "Test.GenericHolder`1", "TestAsm") with {
			IsGenericDefinition = true,
		};

		using var stream = GenerateAssembly (new [] { generic }, "GenericUcoCtorTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var nctorMethodHandle = FindNctorUcoMethod (reader);
		Assert.False (nctorMethodHandle.IsNil, "Open-generic ACWs should emit a throwing nctor_*_uco method");

		var nctorMethod = reader.GetMethodDefinition (nctorMethodHandle);
		var body = pe.GetMethodBody (nctorMethod.RelativeVirtualAddress);
		Assert.NotNull (body);

		var regions = body.ExceptionRegions;
		Assert.True (regions.Length >= 2,
			$"Open-generic UCO constructor should have at least 2 exception regions, found {regions.Length}");
		Assert.Contains (regions, r => r.Kind == ExceptionRegionKind.Catch);
		Assert.Contains (regions, r => r.Kind == ExceptionRegionKind.Finally);

		var typeNames = GetTypeRefNames (reader);
		Assert.Contains ("NotSupportedException", typeNames);

		var ilBytes = body.GetILBytes ();
		Assert.NotNull (ilBytes);
		var memberRefHandles = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (i => MetadataTokens.MemberReferenceHandle (i))
			.ToList ();
		var notSupportedExceptionCtorHandle = memberRefHandles.First (h => {
			var member = reader.GetMemberReference (h);
			if (reader.GetString (member.Name) != ".ctor" || member.Parent.Kind != HandleKind.TypeReference) {
				return false;
			}

			var parent = reader.GetTypeReference ((TypeReferenceHandle) member.Parent);
			return reader.GetString (parent.Name) == "NotSupportedException";
		});
		var beginHandle = memberRefHandles.First (h => reader.GetString (reader.GetMemberReference (h).Name) == "BeginMarshalMethod");
		var endHandle = memberRefHandles.First (h => reader.GetString (reader.GetMemberReference (h).Name) == "EndMarshalMethod");
		var exHandle = memberRefHandles.First (h => reader.GetString (reader.GetMemberReference (h).Name) == "OnUserUnhandledException");
		int notSupportedExceptionCtorToken = MetadataTokens.GetToken (notSupportedExceptionCtorHandle);
		int beginToken = MetadataTokens.GetToken (beginHandle);
		int endToken = MetadataTokens.GetToken (endHandle);
		int exToken = MetadataTokens.GetToken (exHandle);
		Assert.True (ILContainsNewobjToken (ilBytes, notSupportedExceptionCtorToken), "open-generic nctor_*_uco IL should construct NotSupportedException");
		Assert.True (ilBytes.Contains ((byte) ILOpCode.Throw), "open-generic nctor_*_uco IL should throw");
		Assert.True (ILContainsCallToken (ilBytes, beginToken), "open-generic nctor_*_uco IL should call BeginMarshalMethod");
		Assert.True (ILContainsCallToken (ilBytes, endToken), "open-generic nctor_*_uco IL should call EndMarshalMethod");
		Assert.True (ILContainsCallToken (ilBytes, exToken), "open-generic nctor_*_uco IL should call OnUserUnhandledException");
	}

	[Fact]
	public void Generate_UcoConstructor_InheritedCtor_HasExceptionRegions ()
	{
		// Verify the non-leaf (inherited) activation path also gets exception regions.
		var peers = ScanFixtures ();
		var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");
		Assert.NotNull (simpleActivity.ActivationCtor);
		// SimpleActivity does not declare its own (IntPtr, JniHandleOwnership) ctor,
		// so the activation ctor is inherited from Activity (DeclaringTypeName != ManagedTypeName).
		Assert.NotEqual (simpleActivity.ActivationCtor.DeclaringTypeName, simpleActivity.ManagedTypeName);

		using var stream = GenerateAssembly (new [] { simpleActivity }, "InheritedUcoCtorExcTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var nctorMethodHandle = FindNctorUcoMethod (reader);
		Assert.False (nctorMethodHandle.IsNil, "SimpleActivity (ACW) should have a nctor_*_uco method");

		var nctorMethod = reader.GetMethodDefinition (nctorMethodHandle);
		var body = pe.GetMethodBody (nctorMethod.RelativeVirtualAddress);
		Assert.NotNull (body);
		var regions = body.ExceptionRegions;
		Assert.True (regions.Length >= 2,
			$"Inherited-ctor UCO constructor should have at least 2 exception regions, found {regions.Length}");
		Assert.Contains (regions, r => r.Kind == ExceptionRegionKind.Catch);
		Assert.Contains (regions, r => r.Kind == ExceptionRegionKind.Finally);
	}

	[Fact]
	public void Generate_ProxyTypes_HaveSelfAppliedAttribute ()
	{
		var peers = ScanFixtures ();
		var activityPeer = peers.First (p => p.JavaName == "android/app/Activity");

		using var stream = GenerateAssembly (new [] { activityPeer }, "SelfApply");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();

		var proxyTypeDef = reader.TypeDefinitions
			.First (h => reader.GetString (reader.GetTypeDefinition (h).Namespace) == "_TypeMap.Proxies");

		// The proxy type should have a custom attribute applied to itself (self-application)
		var attrs = reader.GetCustomAttributes (proxyTypeDef);
		Assert.NotEmpty (attrs);

		// Verify the attribute's constructor is a MethodDef (i.e., defined in this assembly,
		// meaning it's the proxy's own .ctor — self-application)
		bool hasSelfApplied = false;
		foreach (var attrHandle in attrs) {
			var attr = reader.GetCustomAttribute (attrHandle);
			if (attr.Constructor.Kind == HandleKind.MethodDefinition) {
				hasSelfApplied = true;
				break;
			}
		}
		Assert.True (hasSelfApplied, "Proxy type should have a self-applied attribute (ctor is MethodDefinition)");
	}
}
