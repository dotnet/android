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
		Assert.Contains ("get_TargetType", methods);
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

		using var stream = GenerateAssembly (peers, "AliasTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var assemblyAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
		Assert.True (assemblyAttrs.Count () >= 3);

		var typeNames = GetTypeRefNames (reader);
		Assert.Contains ("TypeMapAssociationAttribute", typeNames);
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
	public void Generate_SimpleActivity_UsesGetUninitializedObject ()
	{
		var peers = ScanFixtures ();
		var simpleActivity = peers.First (p => p.JavaName == "my/app/SimpleActivity");
		Assert.NotNull (simpleActivity.ActivationCtor);
		Assert.NotEqual (simpleActivity.ManagedTypeName, simpleActivity.ActivationCtor.DeclaringTypeName);

		using var stream = GenerateAssembly (new [] { simpleActivity }, "InheritedCtorTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var typeNames = GetTypeRefNames (reader);
		Assert.Contains ("RuntimeHelpers", typeNames);

		var memberNames = GetMemberRefNames (reader);
		Assert.DoesNotContain ("CreateManagedPeer", memberNames);
		Assert.Contains ("GetUninitializedObject", memberNames);
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
	public void Generate_GenericType_ThrowsNotSupportedException ()
	{
		var peers = ScanFixtures ();
		var generic = peers.First (p => p.JavaName == "my/app/GenericHolder");
		Assert.True (generic.IsGenericDefinition);

		using var stream = GenerateAssembly (new [] { generic }, "GenericTest");
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var typeNames = GetTypeRefNames (reader);
		Assert.Contains ("NotSupportedException", typeNames);
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
		Assert.Equal (1, sig.ParameterTypes.Length);
		Assert.Equal ("System.Int32", sig.ParameterTypes [0]);
	}
}