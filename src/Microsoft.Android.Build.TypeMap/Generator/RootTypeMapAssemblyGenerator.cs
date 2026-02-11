using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// Generates the root <c>_Microsoft.Android.TypeMaps.dll</c> assembly that references
/// all per-assembly typemap assemblies via <c>[assembly: TypeMapAssemblyTarget("name")]</c>.
/// </summary>
sealed class RootTypeMapAssemblyGenerator
{
	const string DefaultAssemblyName = "_Microsoft.Android.TypeMaps";

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

		// Assembly reference for System.Runtime (needed for Attribute base class)
		var systemRuntimeRef = metadata.AddAssemblyReference (
			metadata.GetOrAddString ("System.Runtime"),
			new Version (11, 0, 0, 0), default, default, 0, default);

		var systemRuntimeInteropServicesRef = metadata.AddAssemblyReference (
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			new Version (11, 0, 0, 0), default, default, 0, default);

		// <Module> type
		metadata.AddTypeDefinition (
			default, default,
			metadata.GetOrAddString ("<Module>"),
			default,
			MetadataTokens.FieldDefinitionHandle (1),
			MetadataTokens.MethodDefinitionHandle (1));

		// TypeMapAssemblyTargetAttribute type definition + [assembly: ...] applications
		var attributeTypeRef = metadata.AddTypeReference (systemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Attribute"));

		var baseAttrCtorRef = AddMemberRef (metadata, attributeTypeRef, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), p => { }));

		var stringRef = metadata.AddTypeReference (systemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("String"));

		// Define TypeMapAssemblyTargetAttribute with (string assemblyName) ctor
		int typeFieldStart = metadata.GetRowCount (TableIndex.Field) + 1;
		int typeMethodStart = metadata.GetRowCount (TableIndex.MethodDef) + 1;

		var ctorSigBlob = new BlobBuilder ();
		new BlobEncoder (ctorSigBlob).MethodSignature (isInstanceMethod: true)
			.Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ());

		var ctorCodeBuilder = new BlobBuilder ();
		var ctorEncoder = new InstructionEncoder (ctorCodeBuilder);
		ctorEncoder.OpCode (ILOpCode.Ldarg_0);
		ctorEncoder.Call (baseAttrCtorRef);
		ctorEncoder.OpCode (ILOpCode.Ret);

		while (ilBuilder.Count % 4 != 0) {
			ilBuilder.WriteByte (0);
		}
		var bodyEncoder = new MethodBodyStreamEncoder (ilBuilder);
		int ctorBodyOffset = bodyEncoder.AddMethodBody (ctorEncoder);

		var ctorDef = metadata.AddMethodDefinition (
			MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			MethodImplAttributes.IL,
			metadata.GetOrAddString (".ctor"),
			metadata.GetOrAddBlob (ctorSigBlob),
			ctorBodyOffset, default);

		metadata.AddTypeDefinition (
			TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
			metadata.GetOrAddString ("System.Runtime.InteropServices"),
			metadata.GetOrAddString ("TypeMapAssemblyTargetAttribute"),
			attributeTypeRef,
			MetadataTokens.FieldDefinitionHandle (typeFieldStart),
			MetadataTokens.MethodDefinitionHandle (typeMethodStart));

		// Add [assembly: TypeMapAssemblyTarget("name")] for each per-assembly typemap
		foreach (var name in perAssemblyTypeMapNames) {
			var attrBlob = new BlobBuilder ();
			attrBlob.WriteUInt16 (1); // Prolog
			attrBlob.WriteSerializedString (name);
			attrBlob.WriteUInt16 (0); // NumNamed
			metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorDef,
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
