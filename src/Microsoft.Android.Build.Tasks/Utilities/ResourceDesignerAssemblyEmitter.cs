using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks;

sealed class ResourceDesignerAssemblyEmitter
{
	const string DesignerNamespace = "_Microsoft.Android.Resource.Designer";
	static readonly Lazy<byte []> PublicKey = new (ReadPublicKey);
	readonly MetadataBuilder metadata = new ();
	readonly BlobBuilder il = new ();
	readonly BlobBuilder signature = new ();
	readonly BlobBuilder code = new ();
	readonly BlobBuilder attribute = new ();
	readonly MethodBodyStreamEncoder bodies;
	AssemblyReferenceHandle netstandard;
	TypeReferenceHandle objectType;
	TypeReferenceHandle intType;
	MemberReferenceHandle objectConstructor;
	MemberReferenceHandle editorBrowsableConstructor;
	MemberReferenceHandle generatedCodeConstructor;
	BlobHandle instanceConstructorSignature;
	BlobHandle staticConstructorSignature;
	BlobHandle intGetterSignature;
	BlobHandle arrayGetterSignature;
	BlobHandle intPropertySignature;
	BlobHandle arrayPropertySignature;
	BlobHandle intFieldSignature;
	BlobHandle arrayFieldSignature;
	BlobHandle editorBrowsableAttribute;
	BlobHandle generatedCodeAttribute;

	public ResourceDesignerAssemblyEmitter () => bodies = new MethodBodyStreamEncoder (il);

	public BlobBuilder Emit (string assemblyName, IEnumerable<R> resources, bool isApplication, string? applicationAssemblyName, bool deterministic)
	{
		var mvid = metadata.ReserveGuid ();
		if (!deterministic)
			new BlobWriter (mvid.Content).WriteGuid (Guid.NewGuid ());
		metadata.AddModule (0, metadata.GetOrAddString (assemblyName), mvid.Handle, default, default);
		var assembly = metadata.AddAssembly (metadata.GetOrAddString (assemblyName), new Version (1, 0, 0, 0),
			default, metadata.GetOrAddBlob (PublicKey.Value), AssemblyFlags.PublicKey, AssemblyHashAlgorithm.Sha1);
		netstandard = metadata.AddAssemblyReference (metadata.GetOrAddString ("netstandard"), new Version (2, 1, 0, 0),
			default, metadata.GetOrAddBlob (new byte [] { 0xcc, 0x7b, 0x13, 0xff, 0xcd, 0x2d, 0xdd, 0x51 }), 0, default);
		objectType = AddTypeReference ("System", "Object");
		intType = AddTypeReference ("System", "Int32");
		instanceConstructorSignature = MethodSignature (isInstance: true);
		staticConstructorSignature = MethodSignature (isInstance: false);
		objectConstructor = AddConstructor (objectType, instanceConstructorSignature);
		InitializeSignatures ();

		if (!isApplication) {
			AddAttribute (assembly, "System.Runtime.CompilerServices", "ReferenceAssemblyAttribute");
		} else if (!string.IsNullOrEmpty (applicationAssemblyName)) {
			AddAttribute (assembly, "System.Runtime.CompilerServices", "InternalsVisibleToAttribute", applicationAssemblyName);
		}
		var targetFrameworkConstructor = AddConstructor (
			AddTypeReference ("System.Runtime.Versioning", "TargetFrameworkAttribute"), MethodSignature (true, stringParameters: 1));
		attribute.Clear ();
		attribute.WriteUInt16 (1);
		attribute.WriteSerializedString (".NETStandard,Version=v2.1");
		attribute.WriteUInt16 (1);
		attribute.WriteByte (0x54); // Named property.
		attribute.WriteByte ((byte) SignatureTypeCode.String);
		attribute.WriteSerializedString ("FrameworkDisplayName");
		attribute.WriteSerializedString ("");
		metadata.AddCustomAttribute (assembly, targetFrameworkConstructor, metadata.GetOrAddBlob (attribute));

		var editorBrowsableState = AddTypeReference ("System.ComponentModel", "EditorBrowsableState");
		signature.Clear ();
		new BlobEncoder (signature).MethodSignature (isInstanceMethod: true)
			.Parameters (1, result => result.Void (), parameters => parameters.AddParameter ().Type ().Type (editorBrowsableState, isValueType: true));
		editorBrowsableConstructor = AddConstructor (AddTypeReference ("System.ComponentModel", "EditorBrowsableAttribute"), metadata.GetOrAddBlob (signature));
		attribute.Clear ();
		attribute.WriteUInt16 (1);
		attribute.WriteInt32 ((int) System.ComponentModel.EditorBrowsableState.Never);
		attribute.WriteUInt16 (0);
		editorBrowsableAttribute = metadata.GetOrAddBlob (attribute);
		generatedCodeConstructor = AddConstructor (AddTypeReference ("System.CodeDom.Compiler", "GeneratedCodeAttribute"),
			MethodSignature (true, stringParameters: 2));
		attribute.Clear ();
		attribute.WriteUInt16 (1);
		attribute.WriteSerializedString (nameof (GenerateResourceDesignerAssembly));
		attribute.WriteSerializedString (typeof (GenerateResourceDesignerAssembly).Assembly.GetName ().Version?.ToString ()
			?? throw new InvalidOperationException ("The build tasks assembly has no version."));
		attribute.WriteUInt16 (0);
		generatedCodeAttribute = metadata.GetOrAddBlob (attribute);

		metadata.AddTypeDefinition (0, default, metadata.GetOrAddString ("<Module>"), default, NextField (), NextMethod ());
		var groups = new Dictionary<string, List<R>> (StringComparer.OrdinalIgnoreCase);
		foreach (var resource in resources) {
			if (resource.ResourceTypeName == null || resource.Identifier == null ||
				(resource.Type == RType.Array && resource.Ids == null))
				continue;
			var name = ResourceParser.GetNestedTypeName (resource.ResourceTypeName);
			if (!groups.TryGetValue (name, out var group))
				groups.Add (name, group = []);
			group.Add (resource);
		}
		EmitDesigner ("Resource", groups, constants: false);
		if (isApplication)
			EmitDesigner ("ResourceConstant", groups, constants: true);

		var pe = new ManagedPEBuilder (
			new PEHeaderBuilder (imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll | Characteristics.LargeAddressAware),
			new MetadataRootBuilder (metadata), il,
			strongNameSignatureSize: PublicKey.Value.Length - 32,
			flags: CorFlags.ILOnly,
			deterministicIdProvider: deterministic ? DeterministicContentId : null);
		var image = new BlobBuilder ();
		var contentId = pe.Serialize (image);
		if (deterministic)
			new BlobWriter (mvid.Content).WriteGuid (contentId.Guid);
		return image;
	}

	void InitializeSignatures ()
	{
		intGetterSignature = ValueSignature (property: false, array: false);
		arrayGetterSignature = ValueSignature (property: false, array: true);
		intPropertySignature = ValueSignature (property: true, array: false);
		arrayPropertySignature = ValueSignature (property: true, array: true);
		signature.Clear ();
		new BlobEncoder (signature).FieldSignature ().Int32 ();
		intFieldSignature = metadata.GetOrAddBlob (signature);
		signature.Clear ();
		new BlobEncoder (signature).FieldSignature ().SZArray ().Int32 ();
		arrayFieldSignature = metadata.GetOrAddBlob (signature);
	}

	BlobHandle ValueSignature (bool property, bool array)
	{
		signature.Clear ();
		var encoder = new BlobEncoder (signature);
		var method = property ? encoder.PropertySignature (isInstanceProperty: false) : encoder.MethodSignature ();
		method.Parameters (0, result => {
			var type = result.Type ();
			if (array)
				type.SZArray ().Int32 ();
			else
				type.Int32 ();
		}, _ => { });
		return metadata.GetOrAddBlob (signature);
	}

	BlobHandle MethodSignature (bool isInstance, int stringParameters = 0)
	{
		signature.Clear ();
		new BlobEncoder (signature).MethodSignature (isInstanceMethod: isInstance)
			.Parameters (stringParameters, result => result.Void (), parameters => {
				for (int i = 0; i < stringParameters; i++)
					parameters.AddParameter ().Type ().String ();
			});
		return metadata.GetOrAddBlob (signature);
	}

	TypeReferenceHandle AddTypeReference (string ns, string name) =>
		metadata.AddTypeReference (netstandard, metadata.GetOrAddString (ns), metadata.GetOrAddString (name));

	MemberReferenceHandle AddConstructor (TypeReferenceHandle type, BlobHandle blob) =>
		metadata.AddMemberReference (type, metadata.GetOrAddString (".ctor"), blob);

	void AddAttribute (EntityHandle parent, string ns, string name, string? value = null)
	{
		var constructor = AddConstructor (AddTypeReference (ns, name), MethodSignature (true, value == null ? 0 : 1));
		attribute.Clear ();
		attribute.WriteUInt16 (1);
		if (value != null)
			attribute.WriteSerializedString (value);
		attribute.WriteUInt16 (0);
		metadata.AddCustomAttribute (parent, constructor, metadata.GetOrAddBlob (attribute));
	}

	FieldDefinitionHandle NextField () => MetadataTokens.FieldDefinitionHandle (metadata.GetRowCount (TableIndex.Field) + 1);
	MethodDefinitionHandle NextMethod () => MetadataTokens.MethodDefinitionHandle (metadata.GetRowCount (TableIndex.MethodDef) + 1);

	void EmitDesigner (string name, Dictionary<string, List<R>> groups, bool constants)
	{
		var parent = metadata.AddTypeDefinition (TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.BeforeFieldInit,
			metadata.GetOrAddString (DesignerNamespace), metadata.GetOrAddString (name), objectType, NextField (), NextMethod ());
		metadata.AddCustomAttribute (parent, editorBrowsableConstructor, editorBrowsableAttribute);
		metadata.AddCustomAttribute (parent, generatedCodeConstructor, generatedCodeAttribute);
		EmitConstructor ();
		foreach (var group in groups) {
			bool hasArrays = constants && group.Value.Exists (r => r.Type == RType.Array);
			var type = metadata.AddTypeDefinition (TypeAttributes.NestedPublic | TypeAttributes.AutoClass | TypeAttributes.Sealed |
				(hasArrays ? 0 : TypeAttributes.BeforeFieldInit), default, metadata.GetOrAddString (group.Key), objectType, NextField (), NextMethod ());
			metadata.AddNestedType (type, parent);
			if (constants) {
				EmitFields (group.Value);
				EmitConstructor ();
			} else {
				var firstProperty = MetadataTokens.PropertyDefinitionHandle (metadata.GetRowCount (TableIndex.Property) + 1);
				metadata.AddPropertyMap (type, firstProperty);
				foreach (var resource in group.Value)
					EmitProperty (resource);
				EmitConstructor ();
			}
		}
	}

	void EmitConstructor ()
	{
		code.Clear ();
		var instructions = new InstructionEncoder (code);
		instructions.LoadArgument (0);
		instructions.Call (objectConstructor);
		instructions.OpCode (ILOpCode.Ret);
		AddMethod (".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
			instanceConstructorSignature, instructions, maxStack: 1);
	}

	MethodDefinitionHandle AddMethod (string name, MethodAttributes attributes, BlobHandle blob, InstructionEncoder instructions, int maxStack) =>
		metadata.AddMethodDefinition (attributes, MethodImplAttributes.IL,
			metadata.GetOrAddString (name), blob, bodies.AddMethodBody (instructions, maxStack),
			MetadataTokens.ParameterHandle (1));

	void EmitProperty (R resource)
	{
		bool array = resource.Type == RType.Array;
		code.Clear ();
		var instructions = new InstructionEncoder (code);
		if (array) {
			EmitArray (instructions, resource.Ids ?? throw new InvalidOperationException ("An array resource has no values."));
		} else {
			instructions.LoadConstantI4 (resource.Id);
		}
		instructions.OpCode (ILOpCode.Ret);
		var getter = AddMethod ($"get_{resource.Identifier}", MethodAttributes.Public | MethodAttributes.Static |
			(array ? 0 : MethodAttributes.SpecialName | MethodAttributes.HideBySig),
			array ? arrayGetterSignature : intGetterSignature, instructions, maxStack: array ? 4 : 1);
		var property = metadata.AddProperty (PropertyAttributes.None, metadata.GetOrAddString (resource.Identifier
			?? throw new InvalidOperationException ("A resource has no identifier.")),
			array ? arrayPropertySignature : intPropertySignature);
		metadata.AddMethodSemantics (property, MethodSemanticsAttributes.Getter, getter);
	}

	void EmitArray (InstructionEncoder instructions, int [] values)
	{
		instructions.LoadConstantI4 (values.Length);
		instructions.OpCode (ILOpCode.Newarr);
		instructions.Token (intType);
		for (int i = 0; i < values.Length; i++) {
			instructions.OpCode (ILOpCode.Dup);
			instructions.LoadConstantI4 (i);
			instructions.LoadConstantI4 (values [i]);
			instructions.OpCode (ILOpCode.Stelem_i4);
		}
	}

	void EmitFields (List<R> resources)
	{
		code.Clear ();
		var instructions = new InstructionEncoder (code);
		bool hasArrays = false;
		foreach (var resource in resources) {
			bool array = resource.Type == RType.Array;
			var field = metadata.AddFieldDefinition (FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.HasDefault |
				(array ? 0 : FieldAttributes.Literal), metadata.GetOrAddString (resource.Identifier
					?? throw new InvalidOperationException ("A resource has no identifier.")),
				array ? arrayFieldSignature : intFieldSignature);
			metadata.AddConstant (field, array ? null : (object) resource.Id);
			if (!array)
				continue;
			hasArrays = true;
			EmitArray (instructions, resource.Ids ?? throw new InvalidOperationException ("An array resource has no values."));
			instructions.OpCode (ILOpCode.Stsfld);
			instructions.Token (field);
		}
		if (hasArrays) {
			instructions.OpCode (ILOpCode.Ret);
			AddMethod (".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
				staticConstructorSignature, instructions, maxStack: 4);
		}
	}

	static BlobContentId DeterministicContentId (IEnumerable<Blob> blobs)
	{
		using var hash = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);
		foreach (var blob in blobs)
			hash.AppendData (blob.GetBytes ().AsSpan ());
		var id = BlobContentId.FromHash (hash.GetHashAndReset ());
		return new BlobContentId (id.Guid, stamp: 0);
	}

	static byte [] ReadPublicKey ()
	{
		using var stream = typeof (GenerateResourceDesignerAssembly).Assembly.GetManifestResourceStream ("Resource.Designer.snk")
			?? throw new InvalidOperationException ("The resource designer strong-name key is missing.");
		using var reader = new BinaryReader (stream);
		if (reader.ReadUInt32 () != 0x207 || reader.ReadUInt32 () != 0x2400 || reader.ReadUInt32 () != 0x32415352)
			throw new InvalidDataException ("The resource designer key is not a CAPI RSA private key.");
		var bits = reader.ReadUInt32 ();
		var exponent = reader.ReadUInt32 ();
		var modulus = reader.ReadBytes (checked ((int) bits / 8));
		if (modulus.Length != bits / 8)
			throw new EndOfStreamException ("The resource designer key has a truncated modulus.");
		var key = new BlobBuilder ();
		key.WriteUInt32 (0x2400); // Strong-name signature and hash algorithms.
		key.WriteUInt32 (0x8004);
		key.WriteUInt32 (checked ((uint) modulus.Length + 20));
		key.WriteUInt32 (0x206); // CAPI PUBLICKEYBLOB.
		key.WriteUInt32 (0x2400);
		key.WriteUInt32 (0x31415352); // RSA1.
		key.WriteUInt32 (bits);
		key.WriteUInt32 (exponent);
		key.WriteBytes (modulus);
		return key.ToArray ();
	}
}
