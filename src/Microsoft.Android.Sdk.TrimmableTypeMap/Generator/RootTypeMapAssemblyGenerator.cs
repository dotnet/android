using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates the root <c>_Microsoft.Android.TypeMaps.dll</c> assembly that references
/// all per-assembly typemap assemblies via
/// <c>[assembly: TypeMapAssemblyTargetAttribute&lt;Java.Lang.Object&gt;("name")]</c>.
/// </summary>
sealed class RootTypeMapAssemblyGenerator
{
	const string DefaultAssemblyName = "_Microsoft.Android.TypeMaps";

	// Mono.Android strong name public key token (84e04ff9cfb79065)
	static readonly byte [] MonoAndroidPublicKeyToken = { 0x84, 0xe0, 0x4f, 0xf9, 0xcf, 0xb7, 0x90, 0x65 };

	readonly Version _systemRuntimeVersion;

	/// <param name="systemRuntimeVersion">Version for System.Runtime assembly references.</param>
	public RootTypeMapAssemblyGenerator (Version systemRuntimeVersion)
	{
		_systemRuntimeVersion = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
	}

	/// <summary>
	/// Generates the root typemap assembly.
	/// </summary>
	/// <param name="perAssemblyTypeMapNames">Names of per-assembly typemap assemblies to reference.</param>
	/// <param name="outputPath">Path to write the output .dll.</param>
	/// <param name="assemblyName">Optional assembly name (defaults to _Microsoft.Android.TypeMaps).</param>
	public void Generate (IReadOnlyList<string> perAssemblyTypeMapNames, string outputPath, string? assemblyName = null)
	{
		if (perAssemblyTypeMapNames is null) {
			throw new ArgumentNullException (nameof (perAssemblyTypeMapNames));
		}
		if (outputPath is null) {
			throw new ArgumentNullException (nameof (outputPath));
		}

		assemblyName ??= DefaultAssemblyName;
		var moduleName = Path.GetFileName (outputPath);

		var dir = Path.GetDirectoryName (outputPath);
		if (!string.IsNullOrEmpty (dir)) {
			Directory.CreateDirectory (dir);
		}

		var metadata = new MetadataBuilder ();
		var ilBuilder = new BlobBuilder ();

		// Assembly definition
		metadata.AddAssembly (
			metadata.GetOrAddString (assemblyName),
			new Version (1, 0, 0, 0),
			culture: default,
			publicKey: default,
			flags: 0,
			hashAlgorithm: AssemblyHashAlgorithm.None);

		// Module definition
		metadata.AddModule (
			generation: 0,
			metadata.GetOrAddString (moduleName),
			metadata.GetOrAddGuid (Guid.NewGuid ()),
			encId: default,
			encBaseId: default);

		// Assembly references
		var systemRuntimeRef = metadata.AddAssemblyReference (
			metadata.GetOrAddString ("System.Runtime"),
			_systemRuntimeVersion, default, default, 0, default);

		var systemRuntimeInteropServicesRef = metadata.AddAssemblyReference (
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			_systemRuntimeVersion, default, default, 0, default);

		var monoAndroidRef = metadata.AddAssemblyReference (
			metadata.GetOrAddString ("Mono.Android"),
			new Version (0, 0, 0, 0), default,
			metadata.GetOrAddBlob (MonoAndroidPublicKeyToken), 0, default);

		// <Module> type
		metadata.AddTypeDefinition (
			default, default,
			metadata.GetOrAddString ("<Module>"),
			default,
			MetadataTokens.FieldDefinitionHandle (1),
			MetadataTokens.MethodDefinitionHandle (1));

		// Reference the open generic TypeMapAssemblyTargetAttribute`1 from System.Runtime.InteropServices
		var openAttrRef = metadata.AddTypeReference (systemRuntimeInteropServicesRef,
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			metadata.GetOrAddString ("TypeMapAssemblyTargetAttribute`1"));

		// Reference Java.Lang.Object from Mono.Android (the type universe)
		var javaLangObjectRef = metadata.AddTypeReference (monoAndroidRef,
			metadata.GetOrAddString ("Java.Lang"), metadata.GetOrAddString ("Object"));

		// Build TypeSpec for TypeMapAssemblyTargetAttribute<Java.Lang.Object>
		var genericInstBlob = new BlobBuilder ();
		genericInstBlob.WriteByte (0x15); // ELEMENT_TYPE_GENERICINST
		genericInstBlob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		genericInstBlob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (openAttrRef));
		genericInstBlob.WriteCompressedInteger (1); // generic arity = 1
		genericInstBlob.WriteByte (0x12); // ELEMENT_TYPE_CLASS
		genericInstBlob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (javaLangObjectRef));
		var closedAttrTypeSpec = metadata.AddTypeSpecification (metadata.GetOrAddBlob (genericInstBlob));

		// MemberRef for .ctor(string) on the closed generic type
		var ctorRef = AddMemberRef (metadata, closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()));

		// Add [assembly: TypeMapAssemblyTargetAttribute<Java.Lang.Object>("name")] for each per-assembly typemap
		foreach (var name in perAssemblyTypeMapNames) {
			var attrBlob = new BlobBuilder ();
			attrBlob.WriteUInt16 (1); // Prolog
			attrBlob.WriteSerializedString (name);
			attrBlob.WriteUInt16 (0); // NumNamed
			metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorRef,
				metadata.GetOrAddBlob (attrBlob));
		}

		// Write PE
		var peBuilder = new ManagedPEBuilder (
			new PEHeaderBuilder (imageCharacteristics: Characteristics.Dll),
			new MetadataRootBuilder (metadata),
			ilBuilder);
		var peBlob = new BlobBuilder ();
		peBuilder.Serialize (peBlob);
		using var fs = File.Create (outputPath);
		peBlob.WriteContentTo (fs);
	}

	static MemberReferenceHandle AddMemberRef (MetadataBuilder metadata, EntityHandle parent, string name,
		Action<BlobEncoder> encodeSig)
	{
		var blob = new BlobBuilder ();
		encodeSig (new BlobEncoder (blob));
		return metadata.AddMemberReference (parent, metadata.GetOrAddString (name), metadata.GetOrAddBlob (blob));
	}
}
