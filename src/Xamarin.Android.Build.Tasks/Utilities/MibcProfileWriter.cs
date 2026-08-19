#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Writes a MIBC (Managed Instrumented Binary Code) profile that lists every compilable method
/// of one or more input assemblies.
///
/// When <c>crossgen2</c> is invoked with <c>--partial</c> it only precompiles methods that appear
/// in the profile data supplied via <c>--mibc</c>.  Feeding it a profile naming the methods of the
/// "main app assembly" therefore ReadyToRun compiles that assembly, minus the generic code excluded
/// below, while the rest of the application remains partially compiled.
///
/// The set of methods emitted here intentionally mirrors what crossgen2's
/// <c>ReadyToRunLibraryRootProvider</c> would have rooted for a full (non-partial) compilation:
///
///   * abstract methods, <c>[MethodImpl(InternalCall)]</c> methods and runtime-provided bodies
///     (such as delegate <c>Invoke</c>) are skipped, and
///   * methods in a generic context (declared by a generic type, or generic themselves) are
///     skipped.  crossgen2 compiles those as shared code instantiated over <c>System.__Canon</c>,
///     which a profile can only name via that runtime implementation detail.  The main app
///     assembly of a project template contains no such methods, so they are left to the JIT.
///
/// A MIBC file is a zip archive containing a single managed PE named <c>&lt;filename&gt;.dll</c>.
/// The PE holds global methods:
///
///   * <c>AssemblyDictionary</c>: <c>ldstr "&lt;assembly name&gt;;"; ldtoken &lt;group method&gt;; pop</c>
///   * one group method per assembly: <c>ldtoken &lt;method&gt;; pop</c> per profiled method
/// </summary>
class MibcProfileWriter
{
	/// <summary>
	/// Earliest timestamp representable in a zip entry: the DOS date format stores the year as an
	/// offset from 1980, so anything earlier throws.  There is no constant for this in the BCL --
	/// <c>System.IO.Compression.ZipHelper.ValidZipDate_YearMin</c> is internal.
	/// </summary>
	static readonly DateTimeOffset ZipEpoch = new DateTimeOffset (1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

	readonly MetadataBuilder metadata = new MetadataBuilder ();
	readonly BlobBuilder ilBuilder = new BlobBuilder ();
	readonly MethodBodyStreamEncoder methodBodies;
	readonly Action<string>? log;

	readonly Dictionary<string, AssemblyReferenceHandle> assemblyRefs = new Dictionary<string, AssemblyReferenceHandle> (StringComparer.Ordinal);
	// Keyed by metadata token from the assembly currently being scanned; cleared per assembly.
	readonly Dictionary<int, EntityHandle> typeRefs = new Dictionary<int, EntityHandle> ();

	MibcProfileWriter (Action<string>? log)
	{
		this.log = log;
		methodBodies = new MethodBodyStreamEncoder (ilBuilder);
	}

	/// <summary>
	/// Scans <paramref name="assemblies"/> and writes a MIBC profile to <paramref name="outputPath"/>.
	/// </summary>
	/// <param name="assemblies">Paths of the managed assemblies to include, in full.</param>
	/// <param name="outputPath">
	/// Destination file.  A <c>.dll</c> extension writes the raw (uncompressed) PE, anything else
	/// (normally <c>.mibc</c>) writes the zip-compressed form.
	/// </param>
	/// <param name="log">Optional callback receiving diagnostic messages.</param>
	/// <returns>The number of methods written into the profile.</returns>
	public static int Write (IEnumerable<string> assemblies, string outputPath, Action<string>? log = null)
	{
		if (assemblies is null)
			throw new ArgumentNullException (nameof (assemblies));
		if (outputPath is null)
			throw new ArgumentNullException (nameof (outputPath));

		return new MibcProfileWriter (log).WriteCore (assemblies, outputPath);
	}

	int WriteCore (IEnumerable<string> assemblies, string outputPath)
	{
		ReservedBlob<GuidHandle> mvid = metadata.ReserveGuid ();

		metadata.AddModule (
			generation: 0,
			moduleName: metadata.GetOrAddString (Path.GetFileName (outputPath)),
			mvid: mvid.Handle,
			encId: default,
			encBaseId: default);

		metadata.AddAssembly (
			name: metadata.GetOrAddString (Path.GetFileNameWithoutExtension (outputPath)),
			version: new Version (1, 0, 0, 0),
			culture: default,
			publicKey: default,
			flags: 0,
			hashAlgorithm: AssemblyHashAlgorithm.None);

		// <Module> type, required to host the global methods.
		metadata.AddTypeDefinition (
			attributes: default,
			@namespace: default,
			name: metadata.GetOrAddString ("<Module>"),
			baseType: default,
			fieldList: MetadataTokens.FieldDefinitionHandle (1),
			methodList: MetadataTokens.MethodDefinitionHandle (1));

		var voidSignature = new BlobBuilder ();
		new BlobEncoder (voidSignature)
			.MethodSignature (isInstanceMethod: false)
			.Parameters (0, r => r.Void (), _ => { });
		BlobHandle voidSignatureHandle = metadata.GetOrAddBlob (voidSignature);

		var groups = new List<(string Name, MethodDefinitionHandle Handle)> ();
		var fingerprint = new StringBuilder ();
		int totalMethods = 0;
		int groupIndex = 0;

		foreach (string assembly in assemblies) {
			groupIndex++;
			var group = new BlobBuilder ();
			var il = new InstructionEncoder (group);

			(string assemblyName, int methodCount) = AddAssembly (assembly, il, fingerprint);
			totalMethods += methodCount;
			log?.Invoke ($"Added {methodCount} method(s) from '{assembly}' to the MIBC profile.");
			if (methodCount == 0)
				continue;

			MethodDefinitionHandle handle = AddGlobalMethod ($"Assemblies_{assemblyName}_{groupIndex}", il, voidSignatureHandle);

			// The group name lists every assembly referenced by the group, ';' separated, with the
			// defining assembly first.  Our groups only ever reference their own assembly.
			groups.Add ((assemblyName + ";", handle));
		}

		var dictionary = new BlobBuilder ();
		var dictionaryIL = new InstructionEncoder (dictionary);
		foreach ((string name, MethodDefinitionHandle handle) in groups) {
			dictionaryIL.LoadString (metadata.GetOrAddUserString (name));
			EmitToken (dictionaryIL, handle);
		}

		AddGlobalMethod ("AssemblyDictionary", dictionaryIL, voidSignatureHandle);

		BlobContentId contentId = ContentId (fingerprint.ToString ());
		new BlobWriter (mvid.Content).WriteGuid (contentId.Guid);

		WritePE (outputPath, contentId);
		return totalMethods;
	}

	/// <summary>
	/// Adds a global <c>static void</c> method holding <paramref name="il"/>, terminating it with
	/// <c>ret</c>.
	/// </summary>
	MethodDefinitionHandle AddGlobalMethod (string name, InstructionEncoder il, BlobHandle signature)
	{
		il.OpCode (ILOpCode.Ret);
		return metadata.AddMethodDefinition (
			MethodAttributes.Public | MethodAttributes.Static,
			MethodImplAttributes.IL,
			metadata.GetOrAddString (name),
			signature,
			methodBodies.AddMethodBody (il, maxStack: 8),
			parameterList: MetadataTokens.ParameterHandle (1));
	}

	/// <summary>
	/// A MIBC profile names an entity by pushing its token and discarding it; the IL is never run.
	/// </summary>
	static void EmitToken (InstructionEncoder il, EntityHandle handle)
	{
		il.OpCode (ILOpCode.Ldtoken);
		il.Token (handle);
		il.OpCode (ILOpCode.Pop);
	}

	/// <summary>
	/// Emits <c>ldtoken</c>/<c>pop</c> pairs for every compilable method of <paramref name="path"/>
	/// and appends that assembly's identity to <paramref name="fingerprint"/>.  Any method left out
	/// is logged individually.
	/// </summary>
	(string Name, int MethodCount) AddAssembly (string path, InstructionEncoder il, StringBuilder fingerprint)
	{
		int methodCount = 0;

		using var stream = File.OpenRead (path);
		using var peReader = new PEReader (stream);

		MetadataReader reader = peReader.GetMetadataReader ();
		typeRefs.Clear ();

		AssemblyDefinition assembly = reader.GetAssemblyDefinition ();
		string assemblyName = reader.GetString (assembly.Name);
		AssemblyReferenceHandle assemblyRef = GetOrAddAssemblyReference (reader, assembly);

		fingerprint.Append (assemblyName).Append ('|')
			.Append (reader.GetGuid (reader.GetModuleDefinition ().Mvid).ToString ("N")).Append ('\n');

		string Describe (TypeDefinition type, MethodDefinition method) =>
			$"{assemblyName}!{reader.GetString (type.Name)}.{reader.GetString (method.Name)}";

		foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions) {
			TypeDefinition type = reader.GetTypeDefinition (typeHandle);

			// Nested types re-declare the generic parameters of their enclosing types, so this
			// also excludes types nested inside a generic type.
			bool genericType = type.GetGenericParameters ().Count > 0;
			EntityHandle owner = genericType ? default : GetTypeReference (reader, assemblyRef, typeHandle);

			foreach (MethodDefinitionHandle methodHandle in type.GetMethods ()) {
				MethodDefinition method = reader.GetMethodDefinition (methodHandle);
				if (!IsCompilable (method))
					continue;

				if (genericType || method.GetGenericParameters ().Count > 0) {
					log?.Invoke ($"Skipping '{Describe (type, method)}', methods in a generic context are left to the JIT.");
					continue;
				}

				EntityHandle target;
				try {
					target = metadata.AddMemberReference (
						owner,
						metadata.GetOrAddString (reader.GetString (method.Name)),
						metadata.GetOrAddBlob (TranslateMethodSignature (reader, assemblyRef, method.Signature)));
				} catch (BadImageFormatException e) {
					// A signature we cannot translate only costs us that one method, so keep
					// going, but do not do it silently.
					log?.Invoke ($"Skipping '{Describe (type, method)}', its signature could not be read: {e.Message}");
					continue;
				}

				EmitToken (il, target);
				methodCount++;
			}
		}

		return (assemblyName, methodCount);
	}

	/// <summary>
	/// Mirrors the filtering done by crossgen2's <c>ReadyToRunLibraryRootProvider</c>: abstract and
	/// <c>InternalCall</c> methods are skipped, as are runtime-provided bodies such as delegate
	/// <c>Invoke</c>.  P/Invokes are kept, because crossgen2 does compile their marshalling stubs.
	/// </summary>
	static bool IsCompilable (MethodDefinition method)
	{
		if ((method.Attributes & MethodAttributes.Abstract) != 0)
			return false;
		if ((method.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL)
			return false;
		if ((method.ImplAttributes & MethodImplAttributes.InternalCall) != 0)
			return false;
		if (method.RelativeVirtualAddress == 0 && (method.Attributes & MethodAttributes.PinvokeImpl) == 0)
			return false;
		return true;
	}

	byte []? GetPublicKeyToken (MetadataReader reader, AssemblyDefinition assembly)
	{
		if (assembly.PublicKey.IsNil)
			return null;

		byte [] publicKey = reader.GetBlobBytes (assembly.PublicKey);
		if (publicKey.Length == 0)
			return null;
		if (publicKey.Length <= 8)
			return publicKey;

		var name = new AssemblyName ();
		name.SetPublicKey (publicKey);
		return name.GetPublicKeyToken ();
	}

	/// <summary>
	/// Adds an <c>AssemblyRef</c> for the assembly being scanned.  <c>AssemblyDef</c> stores the
	/// full public key, which has to be reduced to a token first.
	/// </summary>
	AssemblyReferenceHandle GetOrAddAssemblyReference (MetadataReader reader, AssemblyDefinition assembly) =>
		GetOrAddAssemblyReference (
			reader,
			reader.GetString (assembly.Name),
			assembly.Version,
			assembly.Culture,
			GetPublicKeyToken (reader, assembly));

	/// <summary>
	/// Adds an <c>AssemblyRef</c> mirroring one of the source assembly's own references, whose
	/// <c>PublicKeyOrToken</c> is already in the form we need.
	/// </summary>
	AssemblyReferenceHandle GetOrAddAssemblyReference (MetadataReader reader, AssemblyReference assembly) =>
		GetOrAddAssemblyReference (
			reader,
			reader.GetString (assembly.Name),
			assembly.Version,
			assembly.Culture,
			assembly.PublicKeyOrToken.IsNil ? null : reader.GetBlobBytes (assembly.PublicKeyOrToken));

	AssemblyReferenceHandle GetOrAddAssemblyReference (MetadataReader reader, string name, Version version, StringHandle culture, byte []? publicKeyToken)
	{
		byte [] token = publicKeyToken ?? [];
		string cultureName = culture.IsNil ? "" : reader.GetString (culture);

		string key = $"{name},{version},{cultureName},{Convert.ToBase64String (token)}";
		if (assemblyRefs.TryGetValue (key, out AssemblyReferenceHandle handle))
			return handle;

		handle = metadata.AddAssemblyReference (
			name: metadata.GetOrAddString (name),
			version: version,
			culture: CopyString (reader, culture),
			publicKeyOrToken: token.Length > 0 ? metadata.GetOrAddBlob (token) : default,
			flags: 0,
			hashValue: default);
		assemblyRefs [key] = handle;
		return handle;
	}

	/// <summary>Copies a string from the assembly being scanned into the MIBC module.</summary>
	StringHandle CopyString (MetadataReader reader, StringHandle source) =>
		source.IsNil ? default : metadata.GetOrAddString (reader.GetString (source));

	/// <summary>
	/// Maps a <c>TypeDef</c>/<c>TypeRef</c>/<c>TypeSpec</c> from the source assembly onto an
	/// equivalent entity in the MIBC module.
	/// </summary>
	EntityHandle GetTypeReference (MetadataReader reader, AssemblyReferenceHandle assemblyRef, EntityHandle source)
	{
		int token = MetadataTokens.GetToken (source);
		if (typeRefs.TryGetValue (token, out EntityHandle handle))
			return handle;

		switch (source.Kind) {
			case HandleKind.TypeDefinition: {
					TypeDefinition type = reader.GetTypeDefinition ((TypeDefinitionHandle) source);
					EntityHandle scope = type.IsNested
						? GetTypeReference (reader, assemblyRef, type.GetDeclaringType ())
						: assemblyRef;
					handle = metadata.AddTypeReference (
						scope,
						CopyString (reader, type.Namespace),
						CopyString (reader, type.Name));
					break;
				}
			case HandleKind.TypeReference: {
					TypeReference type = reader.GetTypeReference ((TypeReferenceHandle) source);
					handle = metadata.AddTypeReference (
						GetResolutionScope (reader, assemblyRef, type.ResolutionScope),
						CopyString (reader, type.Namespace),
						CopyString (reader, type.Name));
					break;
				}
			case HandleKind.TypeSpecification: {
					TypeSpecification type = reader.GetTypeSpecification ((TypeSpecificationHandle) source);
					var blob = new BlobBuilder ();
					BlobReader signature = reader.GetBlobReader (type.Signature);
					TranslateType (reader, assemblyRef, ref signature, blob);
					handle = metadata.AddTypeSpecification (metadata.GetOrAddBlob (blob));
					break;
				}
			default:
				throw new BadImageFormatException ($"Unexpected type handle kind '{source.Kind}'.");
		}

		typeRefs [token] = handle;
		return handle;
	}

	EntityHandle GetResolutionScope (MetadataReader reader, AssemblyReferenceHandle assemblyRef, EntityHandle scope)
	{
		switch (scope.Kind) {
			case HandleKind.AssemblyReference:
				return GetOrAddAssemblyReference (reader, reader.GetAssemblyReference ((AssemblyReferenceHandle) scope));
			case HandleKind.TypeReference:
				return GetTypeReference (reader, assemblyRef, scope);
			default:
				// ModuleDefinition/ModuleReference: the type lives in the assembly being scanned.
				return assemblyRef;
		}
	}

	BlobBuilder TranslateMethodSignature (MetadataReader reader, AssemblyReferenceHandle assemblyRef, BlobHandle source)
	{
		var result = new BlobBuilder ();
		BlobReader signature = reader.GetBlobReader (source);
		TranslateMethodSignature (reader, assemblyRef, ref signature, result);
		return result;
	}

	/// <summary>
	/// Copies a <c>MethodDefSig</c>/<c>MethodRefSig</c>: the calling convention, the generic arity
	/// if there is one, the parameter count, the return type and finally the parameters.  A
	/// <c>FnPtr</c> element inside a signature is followed by one of these too.
	/// </summary>
	void TranslateMethodSignature (MetadataReader reader, AssemblyReferenceHandle assemblyRef, ref BlobReader signature, BlobBuilder result)
	{
		byte header = signature.ReadByte ();
		result.WriteByte (header);

		if (((SignatureAttributes) header & SignatureAttributes.Generic) != 0)
			CopyCompressedInteger (ref signature, result);

		int parameterCount = CopyCompressedInteger (ref signature, result);

		TranslateType (reader, assemblyRef, ref signature, result);
		for (int i = 0; i < parameterCount; i++)
			TranslateType (reader, assemblyRef, ref signature, result);
	}

	/// <summary>
	/// Copies one <c>Type</c> production of an ECMA-335 signature blob, rewriting the embedded
	/// <c>TypeDefOrRefOrSpec</c> tokens so that they are valid in the MIBC module.
	/// </summary>
	void TranslateType (MetadataReader reader, AssemblyReferenceHandle assemblyRef, ref BlobReader signature, BlobBuilder result)
	{
		while (true) {
			byte raw = signature.ReadByte ();
			result.WriteByte (raw);

			// ELEMENT_TYPE_VALUETYPE/CLASS are the only two values System.Reflection.Metadata
			// models with SignatureTypeKind rather than SignatureTypeCode.
			if (raw == (byte) SignatureTypeKind.ValueType || raw == (byte) SignatureTypeKind.Class) {
				TranslateTypeToken (reader, assemblyRef, ref signature, result);
				return;
			}

			switch ((SignatureTypeCode) raw) {
				// Leaf types, nothing else to copy.
				case SignatureTypeCode.Void:
				case SignatureTypeCode.Boolean:
				case SignatureTypeCode.Char:
				case SignatureTypeCode.SByte:
				case SignatureTypeCode.Byte:
				case SignatureTypeCode.Int16:
				case SignatureTypeCode.UInt16:
				case SignatureTypeCode.Int32:
				case SignatureTypeCode.UInt32:
				case SignatureTypeCode.Int64:
				case SignatureTypeCode.UInt64:
				case SignatureTypeCode.Single:
				case SignatureTypeCode.Double:
				case SignatureTypeCode.String:
				case SignatureTypeCode.IntPtr:
				case SignatureTypeCode.UIntPtr:
				case SignatureTypeCode.Object:
				case SignatureTypeCode.TypedReference:
					return;

				// Prefixes: keep copying the type that follows.
				case SignatureTypeCode.Pointer:
				case SignatureTypeCode.ByReference:
				case SignatureTypeCode.SZArray:
				case SignatureTypeCode.Pinned:
				case SignatureTypeCode.Sentinel:
					continue;

				case SignatureTypeCode.RequiredModifier:
				case SignatureTypeCode.OptionalModifier:
					TranslateTypeToken (reader, assemblyRef, ref signature, result);
					continue;

				case SignatureTypeCode.Array: {
						TranslateType (reader, assemblyRef, ref signature, result);
						CopyCompressedInteger (ref signature, result);                  // rank
						int sizeCount = CopyCompressedInteger (ref signature, result);
						for (int i = 0; i < sizeCount; i++)
							CopyCompressedInteger (ref signature, result);
						int lowerBoundCount = CopyCompressedInteger (ref signature, result);
						for (int i = 0; i < lowerBoundCount; i++)
							CopyCompressedSignedInteger (ref signature, result);
						return;
					}

				case SignatureTypeCode.GenericTypeInstance: {
						result.WriteByte (signature.ReadByte ());                       // CLASS or VALUETYPE
						TranslateTypeToken (reader, assemblyRef, ref signature, result);
						int argumentCount = CopyCompressedInteger (ref signature, result);
						for (int i = 0; i < argumentCount; i++)
							TranslateType (reader, assemblyRef, ref signature, result);
						return;
					}

				case SignatureTypeCode.FunctionPointer:
					TranslateMethodSignature (reader, assemblyRef, ref signature, result);
					return;

				default:
					throw new BadImageFormatException ($"Unexpected signature element type '0x{raw:x2}'.");
			}
		}
	}

	void TranslateTypeToken (MetadataReader reader, AssemblyReferenceHandle assemblyRef, ref BlobReader signature, BlobBuilder result)
	{
		EntityHandle source = signature.ReadTypeHandle ();
		result.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (GetTypeReference (reader, assemblyRef, source)));
	}

	static int CopyCompressedInteger (ref BlobReader signature, BlobBuilder result)
	{
		int value = signature.ReadCompressedInteger ();
		result.WriteCompressedInteger (value);
		return value;
	}

	static void CopyCompressedSignedInteger (ref BlobReader signature, BlobBuilder result)
	{
		result.WriteCompressedSignedInteger (signature.ReadCompressedSignedInteger ());
	}

	void WritePE (string outputPath, BlobContentId contentId)
	{
		var peBuilder = new ManagedPEBuilder (
			new PEHeaderBuilder (imageCharacteristics: Characteristics.Dll),
			new MetadataRootBuilder (metadata),
			ilBuilder,
			deterministicIdProvider: _ => contentId);

		var peBlob = new BlobBuilder ();
		peBuilder.Serialize (peBlob);

		string? directory = Path.GetDirectoryName (outputPath);
		if (!directory.IsNullOrEmpty ())
			Directory.CreateDirectory (directory);

		if (string.Equals (Path.GetExtension (outputPath), ".dll", StringComparison.OrdinalIgnoreCase)) {
			using var file = File.Create (outputPath);
			peBlob.WriteContentTo (file);
			return;
		}

		using var archiveStream = File.Create (outputPath);
		using var archive = new ZipArchive (archiveStream, ZipArchiveMode.Create);
		// crossgen2 looks for an entry named "<mibc file name>.dll".
		ZipArchiveEntry entry = archive.CreateEntry (Path.GetFileName (outputPath) + ".dll", CompressionLevel.Optimal);
		// Keep the archive deterministic; the default is DateTime.Now.
		entry.LastWriteTime = ZipEpoch;
		using var entryStream = entry.Open ();
		peBlob.WriteContentTo (entryStream);
	}

	/// <summary>
	/// Derives a stable MVID and timestamp from the profile contents so that unchanged inputs
	/// produce byte-identical output and downstream incremental builds are not invalidated.
	/// </summary>
	/// <param name="fingerprint">
	/// One <c>name|mvid</c> line per input assembly, as built by <see cref="AddAssembly"/>.  The
	/// input MVIDs are hashed rather than reused directly: a module's MVID has to be unique to
	/// that module (ECMA-335 II.22.30), and the profile is not the assembly it profiles.
	/// </param>
	static BlobContentId ContentId (string fingerprint)
	{
		// This is only a content fingerprint, so a non cryptographic hash is fine, and unlike
		// System.Security.Cryptography the System.IO.Hashing APIs are span based on netstandard2.0.
		int byteCount = Encoding.UTF8.GetByteCount (fingerprint);
		Span<byte> bytes = byteCount <= TypeMapHelper.StackallocThresholdBytes
			? stackalloc byte [byteCount]
			: new byte [byteCount];
		TypeMapHelper.GetBytes (fingerprint, Encoding.UTF8, bytes);

		// BlobContentId.FromHash turns hash bytes into an MVID and timestamp for us, including the
		// RFC 4122 version and variant bits and the byte order of the Guid fields.  It reads 20
		// bytes: 0..16 become the MVID and 16..20 become the timestamp.  XxHash128 only produces
		// 16, so the timestamp slot repeats the first four rather than hashing the input again.
		// This cannot be a stackalloc: FromHash only overloads on byte[] and ImmutableArray<byte>.
		var hash = new byte [20];
		XxHash128.Hash (bytes, hash.AsSpan (0, 16));
		hash.AsSpan (0, 4).CopyTo (hash.AsSpan (16));
		return BlobContentId.FromHash (hash);
	}
}
