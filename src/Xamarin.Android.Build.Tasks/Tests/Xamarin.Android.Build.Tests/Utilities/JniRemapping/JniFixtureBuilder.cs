using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// Builds small but structurally faithful managed PE fixtures with System.Reflection.Metadata
	/// only, so the JNI rewriter can be exercised end to end without any external assembly.
	/// </summary>
	class JniFixtureBuilder
	{
		public const string RegisterAttributeNamespace = "Android.Runtime";
		public const string RegisterAttributeName = "RegisterAttribute";
		public const string JavaInteropNamespace = "Java.Interop";
		public const string PrivateImplementationDetails = "<PrivateImplementationDetails>";

		public MetadataBuilder Metadata { get; } = new MetadataBuilder ();
		public BlobBuilder Il { get; } = new BlobBuilder ();
		public BlobBuilder MappedFieldData { get; } = new BlobBuilder ();
		public BlobBuilder ManagedResources { get; } = new BlobBuilder ();

		public Guid Mvid { get; } = Guid.NewGuid ();
		public uint TimeDateStamp { get; } = 0x5A5A1234;
		public CorFlags Flags { get; set; } = CorFlags.ILOnly;
		public int StrongNameSignatureSize { get; set; }
		public DebugDirectoryBuilder DebugDirectory { get; set; }
		public ResourceSectionBuilder NativeResources { get; set; }

		public AssemblyReferenceHandle CoreLibraryReference { get; }
		public TypeReferenceHandle ValueTypeReference { get; }
		public TypeReferenceHandle JavaPeerProxyReference { get; }
		public TypeReferenceHandle ExceptionReference { get; }

		public MethodDefinitionHandle RegisterCtor1 { get; }
		public MethodDefinitionHandle RegisterCtor3 { get; }
		public MethodDefinitionHandle JniTypeSignatureCtor1 { get; }
		public MethodDefinitionHandle JniMethodSignatureCtor2 { get; }
		public MethodDefinitionHandle JniConstructorSignatureCtor1 { get; }

		readonly MethodBodyStreamEncoder bodyEncoder;
		TypeDefinitionHandle privateImplementationDetails;
		readonly Dictionary<int, TypeDefinitionHandle> sizedTypes = new Dictionary<int, TypeDefinitionHandle> ();
		int utf8FieldCounter;

		public JniFixtureBuilder ()
		{
			bodyEncoder = new MethodBodyStreamEncoder (Il);

			Metadata.AddModule (0, Metadata.GetOrAddString ("Fixture.dll"), Metadata.GetOrAddGuid (Mvid), default, default);
			Metadata.AddAssembly (Metadata.GetOrAddString ("Fixture"), new Version (1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.Sha1);
			Metadata.AddTypeDefinition (default, default, Metadata.GetOrAddString ("<Module>"), default,
				MetadataTokens.FieldDefinitionHandle (1), MetadataTokens.MethodDefinitionHandle (1));

			CoreLibraryReference = Metadata.AddAssemblyReference (
				Metadata.GetOrAddString ("System.Runtime"), new Version (11, 0, 0, 0), default, default, default, default);
			ValueTypeReference = Metadata.AddTypeReference (CoreLibraryReference,
				Metadata.GetOrAddString ("System"), Metadata.GetOrAddString ("ValueType"));
			ExceptionReference = Metadata.AddTypeReference (CoreLibraryReference,
				Metadata.GetOrAddString ("System"), Metadata.GetOrAddString ("Exception"));
			JavaPeerProxyReference = Metadata.AddTypeReference (CoreLibraryReference,
				Metadata.GetOrAddString (JavaInteropNamespace), Metadata.GetOrAddString ("JavaPeerProxy"));

			int fieldStart = NextFieldRid;
			int methodStart = NextMethodRid;
			RegisterCtor1 = AddAttributeCtor (1);
			RegisterCtor3 = AddAttributeCtor (3);
			AddType (RegisterAttributeNamespace, RegisterAttributeName, fieldStart, methodStart);

			fieldStart = NextFieldRid;
			methodStart = NextMethodRid;
			JniTypeSignatureCtor1 = AddAttributeCtor (1);
			AddType (JavaInteropNamespace, "JniTypeSignatureAttribute", fieldStart, methodStart);

			fieldStart = NextFieldRid;
			methodStart = NextMethodRid;
			JniMethodSignatureCtor2 = AddAttributeCtor (2);
			AddType (JavaInteropNamespace, "JniMethodSignatureAttribute", fieldStart, methodStart);

			fieldStart = NextFieldRid;
			methodStart = NextMethodRid;
			JniConstructorSignatureCtor1 = AddAttributeCtor (1);
			AddType (JavaInteropNamespace, "JniConstructorSignatureAttribute", fieldStart, methodStart);
		}

		public int NextFieldRid => Metadata.GetRowCount (TableIndex.Field) + 1;

		public int NextMethodRid => Metadata.GetRowCount (TableIndex.MethodDef) + 1;

		public TypeDefinitionHandle AddType (string ns, string name, int fieldStart, int methodStart,
			TypeAttributes attributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
			EntityHandle baseType = default)
			=> Metadata.AddTypeDefinition (
				attributes,
				ns == null ? default : Metadata.GetOrAddString (ns),
				Metadata.GetOrAddString (name),
				baseType,
				MetadataTokens.FieldDefinitionHandle (fieldStart),
				MetadataTokens.MethodDefinitionHandle (methodStart));

		public MethodDefinitionHandle AddVoidMethod (string name, int bodyOffset,
			MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.HideBySig)
			=> Metadata.AddMethodDefinition (
				attributes,
				MethodImplAttributes.IL,
				Metadata.GetOrAddString (name),
				Metadata.GetOrAddBlob (BuildVoidNoArgsSignature ()),
				bodyOffset,
				MetadataTokens.ParameterHandle (Metadata.GetRowCount (TableIndex.Param) + 1));

		MethodDefinitionHandle AddAttributeCtor (int argCount)
		{
			var signature = new BlobBuilder ();
			new BlobEncoder (signature).MethodSignature (isInstanceMethod: true)
				.Parameters (argCount, out ReturnTypeEncoder returnType, out ParametersEncoder parameters);
			returnType.Void ();
			for (int i = 0; i < argCount; i++) {
				parameters.AddParameter ().Type ().String ();
			}

			return Metadata.AddMethodDefinition (
				MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
				MethodImplAttributes.IL,
				Metadata.GetOrAddString (".ctor"),
				Metadata.GetOrAddBlob (signature),
				EmitReturnOnlyBody (),
				MetadataTokens.ParameterHandle (Metadata.GetRowCount (TableIndex.Param) + 1));
		}

		static BlobBuilder BuildVoidNoArgsSignature ()
		{
			var signature = new BlobBuilder ();
			new BlobEncoder (signature).MethodSignature (isInstanceMethod: true)
				.Parameters (0, out ReturnTypeEncoder returnType, out ParametersEncoder _);
			returnType.Void ();
			return signature;
		}

		public int EmitReturnOnlyBody () => EmitBody (encoder => encoder.OpCode (ILOpCode.Ret));

		public int EmitLoadStringBody (params UserStringHandle [] strings)
			=> EmitBody (encoder => {
				foreach (UserStringHandle handle in strings) {
					encoder.LoadString (handle);
					encoder.OpCode (ILOpCode.Pop);
				}
				encoder.OpCode (ILOpCode.Ret);
			});

		public int EmitBody (Action<InstructionEncoder> emit, StandaloneSignatureHandle localSignature = default,
			ControlFlowBuilder controlFlow = null, int maxStack = 8)
		{
			var code = new BlobBuilder ();
			var encoder = new InstructionEncoder (code, controlFlow);
			emit (encoder);
			return bodyEncoder.AddMethodBody (encoder, maxStack, localSignature);
		}

		public UserStringHandle String (string value) => Metadata.GetOrAddUserString (value);

		public BlobHandle AttributeBlob (params string [] fixedStringArgs)
		{
			var blob = new BlobBuilder ();
			blob.WriteUInt16 (0x0001); // Prolog
			foreach (string arg in fixedStringArgs) {
				blob.WriteSerializedString (arg);
			}
			blob.WriteUInt16 (0x0000); // NumNamed
			return Metadata.GetOrAddBlob (blob);
		}

		public void AddEmbeddedResource (string name, byte [] content)
		{
			ManagedResources.Align (8);
			int offset = ManagedResources.Count;
			ManagedResources.WriteInt32 (content.Length);
			ManagedResources.WriteBytes (content);
			Metadata.AddManifestResource (ManifestResourceAttributes.Public, Metadata.GetOrAddString (name), default, (uint) offset);
		}

		/// <summary>
		/// Emits a null-terminated UTF-8 JNI datum exactly the way
		/// Microsoft.Android.Sdk.TrimmableTypeMap's PEAssemblyBuilder does: a static HasFieldRVA
		/// field whose type is a <c>&lt;PrivateImplementationDetails&gt;/__utf8_N</c>
		/// explicit-layout value type sized to the datum.
		/// </summary>
		public FieldDefinitionHandle AddUtf8Field (string value)
		{
			int size = Encoding.UTF8.GetByteCount (value) + 1;
			TypeDefinitionHandle sizedType = GetOrCreateSizedType (size);

			var signature = new BlobBuilder ();
			new BlobEncoder (signature).FieldSignature ().Type (sizedType, isValueType: true);

			int rva = MappedFieldData.Count;
			var bytes = new byte [size];
			Encoding.UTF8.GetBytes (value, 0, value.Length, bytes, 0);
			MappedFieldData.WriteBytes (bytes);

			FieldDefinitionHandle handle = Metadata.AddFieldDefinition (
				FieldAttributes.Static | FieldAttributes.Assembly | FieldAttributes.HasFieldRVA | FieldAttributes.InitOnly,
				Metadata.GetOrAddString ("__utf8_" + utf8FieldCounter++),
				Metadata.GetOrAddBlob (signature));
			Metadata.AddFieldRelativeVirtualAddress (handle, rva);
			return handle;
		}

		public TypeDefinitionHandle EnsurePrivateImplementationDetails ()
		{
			if (!privateImplementationDetails.IsNil) {
				return privateImplementationDetails;
			}

			privateImplementationDetails = AddType (null, PrivateImplementationDetails, NextFieldRid, NextMethodRid,
				TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit);
			return privateImplementationDetails;
		}

		TypeDefinitionHandle GetOrCreateSizedType (int size)
		{
			if (sizedTypes.TryGetValue (size, out TypeDefinitionHandle existing)) {
				return existing;
			}

			TypeDefinitionHandle enclosing = EnsurePrivateImplementationDetails ();
			TypeDefinitionHandle handle = AddType (null, "__utf8_" + size, NextFieldRid, NextMethodRid,
				TypeAttributes.NestedAssembly | TypeAttributes.ExplicitLayout | TypeAttributes.Sealed | TypeAttributes.AnsiClass,
				ValueTypeReference);
			Metadata.AddTypeLayout (handle, packingSize: 1, size: (uint) size);
			Metadata.AddNestedType (handle, enclosing);

			sizedTypes [size] = handle;
			return handle;
		}

		public byte [] Serialize ()
		{
			var headerBuilder = new PEHeaderBuilder (
				imageCharacteristics: Characteristics.Dll | Characteristics.ExecutableImage);

			var peBuilder = new ManagedPEBuilder (
				headerBuilder,
				new MetadataRootBuilder (Metadata),
				Il,
				mappedFieldData: MappedFieldData.Count > 0 ? MappedFieldData : null,
				managedResources: ManagedResources.Count > 0 ? ManagedResources : null,
				nativeResources: NativeResources,
				debugDirectoryBuilder: DebugDirectory,
				strongNameSignatureSize: StrongNameSignatureSize,
				entryPoint: default,
				flags: Flags,
				deterministicIdProvider: _ => new BlobContentId (Mvid, TimeDateStamp));

			var peBlob = new BlobBuilder ();
			peBuilder.Serialize (peBlob);

			using var stream = new MemoryStream ();
			peBlob.WriteContentTo (stream);
			return stream.ToArray ();
		}
	}
}
