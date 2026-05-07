using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Shared plumbing for building PE assemblies with System.Reflection.Metadata.
/// Owns the <see cref="MetadataBuilder"/>, common assembly/type references, scratch blob builders,
/// and the final PE serialisation.  Both <see cref="TypeMapAssemblyEmitter"/> and
/// <see cref="RootTypeMapAssemblyGenerator"/> delegate to this instead of duplicating boilerplate.
/// </summary>
sealed class PEAssemblyBuilder
{
	const int DefaultMaxStack = 32;

	// Mono.Android strong name public key token (84e04ff9cfb79065)
	static readonly byte [] MonoAndroidPublicKeyToken = { 0x84, 0xe0, 0x4f, 0xf9, 0xcf, 0xb7, 0x90, 0x65 };

	readonly Dictionary<string, AssemblyReferenceHandle> _asmRefCache = new (StringComparer.OrdinalIgnoreCase);
	readonly Dictionary<(string Assembly, string Type), EntityHandle> _typeRefCache = new ();

	// Reusable scratch BlobBuilders — avoids allocating a new one per method body / attribute / member ref.
	// Each is Clear()'d before use. Safe because all emission is single-threaded and non-reentrant.
	readonly BlobBuilder _sigBlob = new BlobBuilder (64);
	readonly BlobBuilder _codeBlob = new BlobBuilder (256);
	readonly BlobBuilder _attrBlob = new BlobBuilder (64);

	// Holds raw byte data for fields with FieldAttributes.HasFieldRVA (e.g., UTF-8 string literals).
	// Passed to ManagedPEBuilder as the mappedFieldData section.
	readonly BlobBuilder _mappedFieldData = new BlobBuilder ();

	// Cache of sized value types for RVA fields, keyed by byte length.
	// Avoids creating duplicate __utf8_N types when multiple fields share the same size.
	readonly Dictionary<int, TypeDefinitionHandle> _sizedTypeCache = new ();

	// Deduplication cache for UTF-8 string RVA fields. Strings like "()V" that repeat across
	// many proxy types are stored once and shared via the same FieldDefinitionHandle.
	readonly Dictionary<string, FieldDefinitionHandle> _utf8FieldCache = new (StringComparer.Ordinal);
	TypeDefinitionHandle _privateImplDetailsType;
	int _utf8FieldCounter;

	readonly Version _systemRuntimeVersion;

	public MetadataBuilder Metadata { get; } = new MetadataBuilder ();
	public BlobBuilder ILBuilder { get; } = new BlobBuilder ();

	public AssemblyReferenceHandle SystemRuntimeRef { get; private set; }
	public AssemblyReferenceHandle SystemRuntimeInteropServicesRef { get; private set; }
	public AssemblyReferenceHandle MonoAndroidRef { get; private set; }

	public PEAssemblyBuilder (Version systemRuntimeVersion)
	{
		_systemRuntimeVersion = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
	}

	/// <summary>
	/// Emits the assembly definition, module definition, common assembly references, and &lt;Module&gt; type.
	/// Call this first.
	/// </summary>
	public void EmitPreamble (string assemblyName, string moduleName, ReadOnlySpan<byte> contentFingerprint = default)
	{
		_asmRefCache.Clear ();
		_typeRefCache.Clear ();

		Metadata.AddAssembly (
			Metadata.GetOrAddString (assemblyName),
			new Version (1, 0, 0, 0),
			culture: default,
			publicKey: default,
			flags: 0,
			hashAlgorithm: AssemblyHashAlgorithm.None);

		Metadata.AddModule (
			generation: 0,
			Metadata.GetOrAddString (moduleName),
			Metadata.GetOrAddGuid (MetadataHelper.DeterministicMvid (moduleName, contentFingerprint)),
			encId: default,
			encBaseId: default);

		// Common assembly references
		SystemRuntimeRef = AddAssemblyRef ("System.Runtime", _systemRuntimeVersion);
		SystemRuntimeInteropServicesRef = AddAssemblyRef ("System.Runtime.InteropServices", _systemRuntimeVersion);
		MonoAndroidRef = AddAssemblyRef ("Mono.Android", new Version (0, 0, 0, 0), MonoAndroidPublicKeyToken);

		// <Module> type
		Metadata.AddTypeDefinition (
			default, default,
			Metadata.GetOrAddString ("<Module>"),
			default,
			MetadataTokens.FieldDefinitionHandle (1),
			MetadataTokens.MethodDefinitionHandle (1));
	}

	/// <summary>
	/// Serialises the metadata + IL into a PE DLL and writes it to the given <paramref name="stream"/>.
	/// </summary>
	public void WritePE (Stream stream)
	{
		var peBuilder = new ManagedPEBuilder (
			new PEHeaderBuilder (imageCharacteristics: Characteristics.Dll),
			new MetadataRootBuilder (Metadata),
			ILBuilder,
			mappedFieldData: _mappedFieldData.Count > 0 ? _mappedFieldData : null);
		var peBlob = new BlobBuilder ();
		peBuilder.Serialize (peBlob);
		peBlob.WriteContentTo (stream);
	}

	/// <summary>
	/// Adds (or retrieves from cache) an assembly reference.
	/// </summary>
	public AssemblyReferenceHandle AddAssemblyRef (string name, Version version, byte []? publicKeyOrToken = null)
	{
		if (_asmRefCache.TryGetValue (name, out var existing)) {
			return existing;
		}
		var handle = Metadata.AddAssemblyReference (
			Metadata.GetOrAddString (name), version, default,
			publicKeyOrToken != null ? Metadata.GetOrAddBlob (publicKeyOrToken) : default, 0, default);
		_asmRefCache [name] = handle;
		return handle;
	}

	/// <summary>
	/// Finds an existing assembly reference or adds one with version 0.0.0.0.
	/// </summary>
	public AssemblyReferenceHandle FindOrAddAssemblyRef (string assemblyName)
		=> AddAssemblyRef (assemblyName, new Version (0, 0, 0, 0));

	/// <summary>
	/// Adds a member reference using the reusable signature blob builder.
	/// </summary>
	public MemberReferenceHandle AddMemberRef (EntityHandle parent, string name, Action<BlobEncoder> encodeSig)
	{
		_sigBlob.Clear ();
		encodeSig (new BlobEncoder (_sigBlob));
		return Metadata.AddMemberReference (parent, Metadata.GetOrAddString (name), Metadata.GetOrAddBlob (_sigBlob));
	}

	/// <summary>
	/// Resolves a <see cref="TypeRefData"/> to a TypeReference/TypeSpecification handle, with caching.
	/// </summary>
	public EntityHandle ResolveTypeRef (TypeRefData typeRef)
	{
		var cacheKey = (typeRef.AssemblyName, typeRef.ManagedTypeName);
		if (_typeRefCache.TryGetValue (cacheKey, out var cached)) {
			return cached;
		}
		var asmRef = FindOrAddAssemblyRef (typeRef.AssemblyName);
		var result = MakeTypeRefForManagedName (asmRef, typeRef.ManagedTypeName);
		_typeRefCache [cacheKey] = result;
		return result;
	}

	TypeReferenceHandle MakeTypeRefForManagedName (EntityHandle scope, string managedTypeName)
	{
		int plusIndex = managedTypeName.IndexOf ('+');
		if (plusIndex >= 0) {
			var outerRef = MakeTypeRefForManagedName (scope, managedTypeName.Substring (0, plusIndex));
			return MakeTypeRefForManagedName (outerRef, managedTypeName.Substring (plusIndex + 1));
		}
		int lastDot = managedTypeName.LastIndexOf ('.');
		var ns = lastDot >= 0 ? managedTypeName.Substring (0, lastDot) : "";
		var name = lastDot >= 0 ? managedTypeName.Substring (lastDot + 1) : managedTypeName;
		return Metadata.AddTypeReference (scope, Metadata.GetOrAddString (ns), Metadata.GetOrAddString (name));
	}

	/// <summary>
	/// Returns a deduplicated RVA field containing the null-terminated UTF-8 encoding of
	/// <paramref name="value"/>. Strings like <c>"()V"</c> that appear across many proxy
	/// types are stored once and share the same <see cref="FieldDefinitionHandle"/>.
	/// The field is declared on an internal sized helper type (e.g. <c>__utf8_10</c>)
	/// nested under <c>&lt;PrivateImplementationDetails&gt;</c>.
	/// </summary>
	public FieldDefinitionHandle GetOrAddUtf8Field (string value)
	{
		if (_utf8FieldCache.TryGetValue (value, out var existing)) {
			return existing;
		}

		EnsurePrivateImplDetailsType ();

		// Encode to null-terminated UTF-8 (all JNI names/signatures are ASCII).
		int byteCount = System.Text.Encoding.UTF8.GetByteCount (value);
		var bytes = new byte [byteCount + 1];
		System.Text.Encoding.UTF8.GetBytes (value, 0, value.Length, bytes, 0);
		// bytes[byteCount] is already 0 (null terminator)

		var sizedType = GetOrCreateSizedType (bytes.Length);

		_sigBlob.Clear ();
		new BlobEncoder (_sigBlob).FieldSignature ().Type (sizedType, true);

		int rva = _mappedFieldData.Count;
		_mappedFieldData.WriteBytes (bytes);

		var fieldHandle = Metadata.AddFieldDefinition (
			FieldAttributes.Static | FieldAttributes.Assembly | FieldAttributes.HasFieldRVA | FieldAttributes.InitOnly,
			Metadata.GetOrAddString ($"__utf8_{_utf8FieldCounter++}"),
			Metadata.GetOrAddBlob (_sigBlob));

		Metadata.AddFieldRelativeVirtualAddress (fieldHandle, rva);

		_utf8FieldCache [value] = fieldHandle;
		return fieldHandle;
	}

	void EnsurePrivateImplDetailsType ()
	{
		if (!_privateImplDetailsType.IsNil) {
			return;
		}

		int typeFieldStart = Metadata.GetRowCount (TableIndex.Field) + 1;
		int typeMethodStart = Metadata.GetRowCount (TableIndex.MethodDef) + 1;

		_privateImplDetailsType = Metadata.AddTypeDefinition (
			TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit,
			default,
			Metadata.GetOrAddString ("<PrivateImplementationDetails>"),
			Metadata.AddTypeReference (SystemRuntimeRef,
				Metadata.GetOrAddString ("System"), Metadata.GetOrAddString ("Object")),
			MetadataTokens.FieldDefinitionHandle (typeFieldStart),
			MetadataTokens.MethodDefinitionHandle (typeMethodStart));
	}

	TypeDefinitionHandle GetOrCreateSizedType (int size)
	{
		if (_sizedTypeCache.TryGetValue (size, out var existing)) {
			return existing;
		}

		EnsurePrivateImplDetailsType ();

		int typeFieldStart = Metadata.GetRowCount (TableIndex.Field) + 1;
		int typeMethodStart = Metadata.GetRowCount (TableIndex.MethodDef) + 1;

		var handle = Metadata.AddTypeDefinition (
			TypeAttributes.NestedAssembly | TypeAttributes.ExplicitLayout | TypeAttributes.Sealed | TypeAttributes.AnsiClass,
			default,
			Metadata.GetOrAddString ($"__utf8_{size}"),
			Metadata.AddTypeReference (SystemRuntimeRef,
				Metadata.GetOrAddString ("System"), Metadata.GetOrAddString ("ValueType")),
			MetadataTokens.FieldDefinitionHandle (typeFieldStart),
			MetadataTokens.MethodDefinitionHandle (typeMethodStart));

		Metadata.AddTypeLayout (handle, packingSize: 1, size: (uint) size);
		Metadata.AddNestedType (handle, _privateImplDetailsType);

		_sizedTypeCache [size] = handle;
		return handle;
	}

	/// <summary>
	/// Emits a method body and definition in one call.
	/// </summary>
	public MethodDefinitionHandle EmitBody (string name, MethodAttributes attrs,
		Action<BlobEncoder> encodeSig, Action<InstructionEncoder> emitIL)
		=> EmitBody (name, attrs, encodeSig, emitIL, encodeLocals: null, useBranches: false);

	/// <summary>
	/// Emits a method body and definition with optional local variable declarations.
	/// </summary>
	/// <param name="encodeLocals">
	/// If non-null, writes the local variable signature blob. The callback receives a fresh
	/// <see cref="BlobBuilder"/> and must write the full <c>LOCAL_SIG</c> blob (header 0x07,
	/// compressed count, then each variable type).
	/// </param>
	/// <param name="useBranches">
	/// If true, creates a <see cref="ControlFlowBuilder"/> so the emitted IL can use
	/// <see cref="InstructionEncoder.DefineLabel"/>, <see cref="InstructionEncoder.Branch"/>,
	/// and <see cref="InstructionEncoder.MarkLabel"/>.
	/// </param>
	public MethodDefinitionHandle EmitBody (string name, MethodAttributes attrs,
		Action<BlobEncoder> encodeSig, Action<InstructionEncoder> emitIL,
		Action<BlobBuilder>? encodeLocals)
		=> EmitBody (name, attrs, encodeSig, emitIL, encodeLocals, useBranches: false);

	public MethodDefinitionHandle EmitBody (string name, MethodAttributes attrs,
		Action<BlobEncoder> encodeSig, Action<InstructionEncoder> emitIL,
		Action<BlobBuilder>? encodeLocals, bool useBranches)
	{
		_sigBlob.Clear ();
		encodeSig (new BlobEncoder (_sigBlob));
		// Capture the sig blob handle before emitIL, because emitIL callbacks
		// may call AddMemberRef which clears and repopulates _sigBlob.
		var sigBlobHandle = Metadata.GetOrAddBlob (_sigBlob);

		StandaloneSignatureHandle localSigHandle = default;
		if (encodeLocals != null) {
			var localSigBlob = new BlobBuilder (32);
			encodeLocals (localSigBlob);
			localSigHandle = Metadata.AddStandaloneSignature (Metadata.GetOrAddBlob (localSigBlob));
		}

		_codeBlob.Clear ();
		ControlFlowBuilder? cfb = useBranches ? new ControlFlowBuilder () : null;
		var encoder = new InstructionEncoder (_codeBlob, cfb);
		emitIL (encoder);

		while (ILBuilder.Count % 4 != 0) {
			ILBuilder.WriteByte (0);
		}
		var bodyEncoder = new MethodBodyStreamEncoder (ILBuilder);
		int bodyOffset = localSigHandle.IsNil
			? bodyEncoder.AddMethodBody (encoder)
			: bodyEncoder.AddMethodBody (encoder, maxStack: DefaultMaxStack, localSigHandle, MethodBodyAttributes.InitLocals);

		return Metadata.AddMethodDefinition (
			attrs, MethodImplAttributes.IL,
			Metadata.GetOrAddString (name),
			sigBlobHandle,
			bodyOffset, MetadataTokens.ParameterHandle (Metadata.GetRowCount (TableIndex.Param) + 1));
	}

	/// <summary>
	/// Emits a method body with full support for exception regions (try/catch/finally).
	/// The callback receives both the <see cref="InstructionEncoder"/> and the
	/// <see cref="ControlFlowBuilder"/> so it can emit IL and register exception regions
	/// (e.g. via <c>cfb.AddCatchRegion</c> / <c>cfb.AddFinallyRegion</c>) in one pass.
	/// A <see cref="ControlFlowBuilder"/> is always created for this overload.
	/// </summary>
	public MethodDefinitionHandle EmitBody (string name, MethodAttributes attrs,
		Action<BlobEncoder> encodeSig,
		Action<InstructionEncoder, ControlFlowBuilder> emitIL,
		Action<BlobBuilder>? encodeLocals)
	{
		_sigBlob.Clear ();
		encodeSig (new BlobEncoder (_sigBlob));
		// Capture the sig blob handle before emitIL, because emitIL callbacks
		// may call AddMemberRef which clears and repopulates _sigBlob.
		var sigBlobHandle = Metadata.GetOrAddBlob (_sigBlob);

		StandaloneSignatureHandle localSigHandle = default;
		if (encodeLocals != null) {
			var localSigBlob = new BlobBuilder (32);
			encodeLocals (localSigBlob);
			localSigHandle = Metadata.AddStandaloneSignature (Metadata.GetOrAddBlob (localSigBlob));
		}

		_codeBlob.Clear ();
		var cfb = new ControlFlowBuilder ();
		var encoder = new InstructionEncoder (_codeBlob, cfb);
		emitIL (encoder, cfb);

		while (ILBuilder.Count % 4 != 0) {
			ILBuilder.WriteByte (0);
		}
		var bodyEncoder = new MethodBodyStreamEncoder (ILBuilder);
		int bodyOffset = localSigHandle.IsNil
			? bodyEncoder.AddMethodBody (encoder)
			: bodyEncoder.AddMethodBody (encoder, maxStack: DefaultMaxStack, localSigHandle, MethodBodyAttributes.InitLocals);

		return Metadata.AddMethodDefinition (
			attrs, MethodImplAttributes.IL,
			Metadata.GetOrAddString (name),
			sigBlobHandle,
			bodyOffset, MetadataTokens.ParameterHandle (Metadata.GetRowCount (TableIndex.Param) + 1));
	}

	/// <summary>
	/// Builds a <c>TypeSpec</c> for a closed generic type with a single type argument.
	/// For example, <c>MakeGenericTypeSpec(openAttrRef, javaLangObjectRef)</c> produces
	/// <c>TypeMapAttribute&lt;Java.Lang.Object&gt;</c>.
	/// </summary>
	public TypeSpecificationHandle MakeGenericTypeSpec (EntityHandle openType, EntityHandle typeArg)
	{
		_sigBlob.Clear ();
		_sigBlob.WriteByte (0x15); // ELEMENT_TYPE_GENERICINST
		_sigBlob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		_sigBlob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (openType));
		_sigBlob.WriteCompressedInteger (1); // generic arity = 1
		_sigBlob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		_sigBlob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (typeArg));
		return Metadata.AddTypeSpecification (Metadata.GetOrAddBlob (_sigBlob));
	}

	/// <summary>
	/// Writes a custom attribute blob. Calls <paramref name="writePayload"/> to fill in the
	/// payload between the prolog and NumNamed footer.
	/// </summary>
	public BlobHandle BuildAttributeBlob (Action<BlobBuilder> writePayload)
	{
		_attrBlob.Clear ();
		_attrBlob.WriteUInt16 (0x0001); // Prolog
		writePayload (_attrBlob);
		_attrBlob.WriteUInt16 (0x0000); // NumNamed
		return Metadata.GetOrAddBlob (_attrBlob);
	}

	/// <summary>
	/// Emits the <c>IgnoresAccessChecksToAttribute</c> type and applies
	/// <c>[assembly: IgnoresAccessChecksTo("...")]</c> for each assembly name.
	/// </summary>
	public void EmitIgnoresAccessChecksToAttribute (List<string> assemblyNames)
	{
		var attributeTypeRef = Metadata.AddTypeReference (SystemRuntimeRef,
			Metadata.GetOrAddString ("System"), Metadata.GetOrAddString ("Attribute"));

		int typeFieldStart = Metadata.GetRowCount (TableIndex.Field) + 1;
		int typeMethodStart = Metadata.GetRowCount (TableIndex.MethodDef) + 1;

		var baseAttrCtorRef = AddMemberRef (attributeTypeRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }));

		var ctorDef = EmitBody (".ctor",
			MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()),
			encoder => {
				encoder.LoadArgument (0);
				encoder.Call (baseAttrCtorRef);
				encoder.OpCode (ILOpCode.Ret);
			});

		Metadata.AddTypeDefinition (
			TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
			Metadata.GetOrAddString ("System.Runtime.CompilerServices"),
			Metadata.GetOrAddString ("IgnoresAccessChecksToAttribute"),
			attributeTypeRef,
			MetadataTokens.FieldDefinitionHandle (typeFieldStart),
			MetadataTokens.MethodDefinitionHandle (typeMethodStart));

		foreach (var asmName in assemblyNames) {
			var blob = BuildAttributeBlob (b => b.WriteSerializedString (asmName));
			Metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorDef, blob);
		}
	}
}
