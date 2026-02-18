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
	// Mono.Android strong name public key token (84e04ff9cfb79065)
	static readonly byte [] MonoAndroidPublicKeyToken = { 0x84, 0xe0, 0x4f, 0xf9, 0xcf, 0xb7, 0x90, 0x65 };

	readonly Dictionary<string, AssemblyReferenceHandle> _asmRefCache = new (StringComparer.OrdinalIgnoreCase);
	readonly Dictionary<(string Assembly, string Type), EntityHandle> _typeRefCache = new ();

	// Reusable scratch BlobBuilders — avoids allocating a new one per method body / attribute / member ref.
	// Each is Clear()'d before use. Safe because all emission is single-threaded and non-reentrant.
	readonly BlobBuilder _sigBlob = new BlobBuilder (64);
	readonly BlobBuilder _codeBlob = new BlobBuilder (256);
	readonly BlobBuilder _attrBlob = new BlobBuilder (64);

	readonly Version _systemRuntimeVersion;

	public MetadataBuilder Metadata { get; } = new MetadataBuilder ();
	public BlobBuilder ILBuilder { get; } = new BlobBuilder ();

	/// <summary>Reference to the System.Runtime assembly.</summary>
	public AssemblyReferenceHandle SystemRuntimeRef { get; private set; }

	/// <summary>Reference to the System.Runtime.InteropServices assembly.</summary>
	public AssemblyReferenceHandle SystemRuntimeInteropServicesRef { get; private set; }

	/// <summary>Reference to the Mono.Android assembly.</summary>
	public AssemblyReferenceHandle MonoAndroidRef { get; private set; }

	public PEAssemblyBuilder (Version systemRuntimeVersion)
	{
		_systemRuntimeVersion = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
	}

	/// <summary>
	/// Emits the assembly definition, module definition, common assembly references, and &lt;Module&gt; type.
	/// Call this first.
	/// </summary>
	public void EmitPreamble (string assemblyName, string moduleName)
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
			Metadata.GetOrAddGuid (MetadataHelper.DeterministicMvid (moduleName)),
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

	/// <summary>Serialises the metadata + IL into a PE DLL at <paramref name="outputPath"/>.</summary>
	public void WritePE (string outputPath)
	{
		var dir = Path.GetDirectoryName (outputPath);
		if (!string.IsNullOrEmpty (dir)) {
			Directory.CreateDirectory (dir);
		}

		var peBuilder = new ManagedPEBuilder (
			new PEHeaderBuilder (imageCharacteristics: Characteristics.Dll),
			new MetadataRootBuilder (Metadata),
			ILBuilder);
		var peBlob = new BlobBuilder ();
		peBuilder.Serialize (peBlob);
		using var fs = File.Create (outputPath);
		peBlob.WriteContentTo (fs);
	}

	// ---- Assembly / type / member reference helpers ----

	/// <summary>Adds (or retrieves from cache) an assembly reference.</summary>
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

	/// <summary>Finds an existing assembly reference or adds one with version 0.0.0.0.</summary>
	public AssemblyReferenceHandle FindOrAddAssemblyRef (string assemblyName)
	{
		if (_asmRefCache.TryGetValue (assemblyName, out var handle)) {
			return handle;
		}
		return AddAssemblyRef (assemblyName, new Version (0, 0, 0, 0));
	}

	/// <summary>Adds a member reference using the reusable signature blob builder.</summary>
	public MemberReferenceHandle AddMemberRef (EntityHandle parent, string name, Action<BlobEncoder> encodeSig)
	{
		_sigBlob.Clear ();
		encodeSig (new BlobEncoder (_sigBlob));
		return Metadata.AddMemberReference (parent, Metadata.GetOrAddString (name), Metadata.GetOrAddBlob (_sigBlob));
	}

	/// <summary>Resolves a <see cref="TypeRefData"/> to a TypeReference/TypeSpecification handle, with caching.</summary>
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

	// ---- Method body emission ----

	/// <summary>Emits a method body and definition in one call.</summary>
	public MethodDefinitionHandle EmitBody (string name, MethodAttributes attrs,
		Action<BlobEncoder> encodeSig, Action<InstructionEncoder> emitIL)
	{
		_sigBlob.Clear ();
		encodeSig (new BlobEncoder (_sigBlob));

		_codeBlob.Clear ();
		var encoder = new InstructionEncoder (_codeBlob);
		emitIL (encoder);

		while (ILBuilder.Count % 4 != 0) {
			ILBuilder.WriteByte (0);
		}
		var bodyEncoder = new MethodBodyStreamEncoder (ILBuilder);
		int bodyOffset = bodyEncoder.AddMethodBody (encoder);

		return Metadata.AddMethodDefinition (
			attrs, MethodImplAttributes.IL,
			Metadata.GetOrAddString (name),
			Metadata.GetOrAddBlob (_sigBlob),
			bodyOffset, default);
	}

	// ---- Attribute blob helpers ----

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
}
