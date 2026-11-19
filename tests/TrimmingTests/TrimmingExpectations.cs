#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.TrimmingTests;

/// <summary>
/// Checks the expectations declared with the attributes from <c>Cases/TrimmingTests.Expectations</c> (read from the untrimmed
/// case assembly) against the trimmed output of one configuration.
/// </summary>
static class TrimmingExpectations
{
	/// <param name="resolveInputAssembly">Returns the untrimmed path of an app assembly by name, or null when unknown.</param>
	/// <param name="isTrimmed">
	/// Whether the configuration trims an assembly. Expectations on an assembly it doesn't trim (the app's own assemblies in
	/// partial mode) are replaced by a check that the whole assembly was kept.
	/// </param>
	public static IReadOnlyList<string> Validate (string caseAssembly, TrimmingConfiguration configuration, ITrimmedOutput output,
		Func<string, string?> resolveInputAssembly, Func<AssemblyMetadata, bool> isTrimmed)
	{
		var failures = new List<string> ();
		// The Tool flags of Microsoft.Android.TrimmingTests.Expectations.
		int tool = configuration switch {
			TrimmingConfiguration.CoreClrFullTrimming => 1,
			TrimmingConfiguration.NativeAot => 2,
			TrimmingConfiguration.CoreClrPartialTrimming => 4,
			_ => throw new ArgumentOutOfRangeException (nameof (configuration)),
		};
		var assembly = AssemblyMetadata.Read (caseAssembly);
		var inputs = new Dictionary<string, AssemblyMetadata?> { [assembly.Name] = assembly };

		CheckInAssemblyExpectations (assembly.Expectations);
		foreach (var type in assembly.Types.Values) {
			foreach (var expectation in Applicable (type.Expectations, "KeptAttribute", "RemovedAttribute")) {
				Expect (type, expectation.Name == "KeptAttribute");
			}
			foreach (var member in type.Members) {
				foreach (var expectation in Applicable (member.Expectations, "KeptAttribute", "RemovedAttribute")) {
					Expect (member, expectation.Name == "KeptAttribute");
				}
			}
			foreach (var expectation in Applicable (type.Expectations, "KeptMemberAttribute")) {
				var signature = expectation.GetString (0);
				var member = type.FindMember (signature);
				if (member is null) {
					failures.Add ($"[KeptMember (\"{signature}\")] on {type.FullName}: no such member in the input assembly.");
				} else {
					Expect (member, kept: true);
				}
			}
			CheckInAssemblyExpectations (type.Expectations);
		}
		foreach (var input in inputs.Values) {
			if (input is not null && !isTrimmed (input)) {
				ExpectKeptWhole (input);
			}
		}
		return failures;

		void ExpectKeptWhole (AssemblyMetadata input)
		{
			foreach (var type in input.Types.Values) {
				if (!output.IsKept (type)) {
					failures.Add ($"{Describe (type)} should have been kept on {configuration}, which doesn't trim '{input.Name}'.");
					continue;
				}
				foreach (var member in type.Members.Where (m => !output.IsKept (m))) {
					failures.Add ($"{Describe (member)} should have been kept on {configuration}, which doesn't trim '{input.Name}'.");
				}
			}
		}

		void CheckInAssemblyExpectations (IEnumerable<ExpectationAttribute> expectations)
		{
			foreach (var expectation in Applicable (expectations, "KeptTypeInAssemblyAttribute", "RemovedTypeInAssemblyAttribute",
					"KeptMemberInAssemblyAttribute", "RemovedMemberInAssemblyAttribute")) {
				var name = expectation.Name;
				var assemblyName = expectation.GetString (0);
				var typeName = expectation.GetString (1);
				var input = GetInput (assemblyName);
				if (input is null) {
					failures.Add ($"[{name}]: unable to find the untrimmed '{assemblyName}' assembly of the app.");
					continue;
				}
				int typeArguments = typeName.IndexOf ('<');
				var definitionName = typeArguments < 0 ? typeName : typeName.Substring (0, typeArguments);
				if (!input.Types.TryGetValue (definitionName, out var type)) {
					failures.Add ($"[{name}]: no type '{definitionName}' in the untrimmed '{assemblyName}' assembly.");
					continue;
				}
				bool kept = name.StartsWith ("Kept", StringComparison.Ordinal);
				if (typeArguments >= 0) {
					ExpectInstantiation (type, typeName, kept);
					continue;
				}
				if (name.EndsWith ("TypeInAssemblyAttribute", StringComparison.Ordinal)) {
					Expect (type, kept);
					continue;
				}
				foreach (var signature in expectation.GetStrings (2)) {
					var member = type.FindMember (signature);
					if (member is null) {
						failures.Add ($"[{name}]: no member '{signature}' on '{typeName}' in the untrimmed '{assemblyName}' assembly.");
					} else {
						Expect (member, kept);
					}
				}
			}
		}

		IEnumerable<ExpectationAttribute> Applicable (IEnumerable<ExpectationAttribute> expectations, params string [] names)
			=> expectations.Where (e => names.Contains (e.Name) && (e.By & tool) != 0);

		void Expect (object typeOrMember, bool kept)
		{
			var declaringType = typeOrMember as TypeMetadata ?? ((MemberMetadata) typeOrMember).DeclaringType;
			if (GetInput (declaringType.AssemblyName) is { } input && !isTrimmed (input)) {
				// ExpectKeptWhole () checks this assembly instead.
				return;
			}
			bool actual;
			try {
				actual = typeOrMember is TypeMetadata type ? output.IsKept (type) : output.IsKept ((MemberMetadata) typeOrMember);
			} catch (NotSupportedException e) {
				failures.Add ($"{Describe (typeOrMember)}: {e.Message}");
				return;
			}
			if (actual != kept) {
				failures.Add ($"{Describe (typeOrMember)} should have been {(kept ? "kept" : "removed")} on {configuration}.");
			}
		}

		void ExpectInstantiation (TypeMetadata definition, string instantiation, bool kept)
		{
			if (GetInput (definition.AssemblyName) is { } input && !isTrimmed (input)) {
				return;
			}
			var description = $"[{definition.AssemblyName}]{instantiation}";
			try {
				if (output.IsKept (definition, instantiation) != kept) {
					failures.Add ($"{description} should have been {(kept ? "kept" : "removed")} on {configuration}.");
				}
			} catch (NotSupportedException e) {
				failures.Add ($"{description}: {e.Message}");
			}
		}

		AssemblyMetadata? GetInput (string name)
		{
			if (!inputs.TryGetValue (name, out var input)) {
				var path = resolveInputAssembly (name);
				input = path is null ? null : AssemblyMetadata.Read (path);
				inputs [name] = input;
			}
			return input;
		}
	}

	static string Describe (object typeOrMember)
		=> typeOrMember switch {
			TypeMetadata type => $"[{type.AssemblyName}]{type.FullName}",
			MemberMetadata member => $"[{member.DeclaringType.AssemblyName}]{member.DeclaringType.FullName}::{member.Signature}",
			_ => typeOrMember.ToString () ?? "",
		};
}

interface ITrimmedOutput
{
	bool IsKept (TypeMetadata type);
	bool IsKept (MemberMetadata member);

	/// <summary>Whether a generic instantiation of <paramref name="definition"/>, e.g. <c>X`1&lt;System.Int32&gt;</c>, survived.</summary>
	bool IsKept (TypeMetadata definition, string instantiation);
}

/// <summary>The assemblies written by ILLink (CoreCLR builds).</summary>
sealed class LinkedAssemblies : ITrimmedOutput
{
	readonly string directory;
	readonly Dictionary<string, AssemblyMetadata?> assemblies = new ();

	public LinkedAssemblies (string directory) => this.directory = directory;

	public bool IsKept (TypeMetadata type) => Find (type) is not null;

	public bool IsKept (MemberMetadata member) => Find (member.DeclaringType)?.FindMember (member.Signature) is not null;

	public bool IsKept (TypeMetadata definition, string instantiation)
		=> throw new NotSupportedException ("ILLink keeps or removes generic definitions, not instantiations; use By = Tool.NativeAot.");

	TypeMetadata? Find (TypeMetadata type)
	{
		if (!assemblies.TryGetValue (type.AssemblyName, out var assembly)) {
			var path = Path.Combine (directory, $"{type.AssemblyName}.dll");
			assembly = File.Exists (path) ? AssemblyMetadata.Read (path) : null;
			assemblies [type.AssemblyName] = assembly;
		}
		return assembly is not null && assembly.Types.TryGetValue (type.FullName, out var linked) ? linked : null;
	}
}

/// <summary>
/// The types and methods ILC compiled into a NativeAOT app, read from its mstat file (<c>IlcGenerateMstatFile=true</c>). The mstat
/// is an assembly whose global <c>Methods</c>, <c>DeduplicatedMethods</c> and <c>Types</c> methods <c>ldtoken</c> every compiled
/// method and type, referencing them in their original assemblies. Inlined methods have no body of their own, so the cases
/// build with <c>JitNoInline=1</c> (see <c>Cases/Directory.Build.targets</c>).
/// </summary>
sealed class IlcCompiledOutput : ITrimmedOutput
{
	readonly HashSet<string> types = new (StringComparer.Ordinal);
	readonly HashSet<string> instantiations = new (StringComparer.Ordinal);
	readonly HashSet<string> methods = new (StringComparer.Ordinal);

	public IlcCompiledOutput (string path)
	{
		using var stream = File.OpenRead (path);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var provider = new SignatureTypeProvider (reader.GetString (reader.GetAssemblyDefinition ().Name));
		foreach (var handle in reader.MethodDefinitions) {
			var method = reader.GetMethodDefinition (handle);
			if (reader.GetString (method.Name) is not ("Methods" or "DeduplicatedMethods" or "Types")) {
				continue;
			}
			var il = pe.GetMethodBody (method.RelativeVirtualAddress).GetILReader ();
			while (il.RemainingBytes > 0) {
				var opCode = (ILOpCode) il.ReadByte ();
				switch (opCode) {
				case ILOpCode.Ldtoken:
					Add (reader, provider, MetadataTokens.EntityHandle (il.ReadInt32 ()));
					break;
				case ILOpCode.Ldc_i4:
					il.ReadInt32 ();
					break;
				case ILOpCode.Ldc_i4_s:
					il.ReadSByte ();
					break;
				case >= ILOpCode.Ldc_i4_m1 and <= ILOpCode.Ldc_i4_8:
				case ILOpCode.Pop:
				case ILOpCode.Ret:
					break;
				default:
					throw new InvalidDataException ($"Unexpected IL opcode {opCode} in {path}.");
				}
			}
		}
	}

	void Add (MetadataReader reader, SignatureTypeProvider provider, EntityHandle handle)
	{
		switch (handle.Kind) {
		case HandleKind.TypeReference:
		case HandleKind.TypeSpecification:
			if (GetTypeKey (reader, provider, handle) is { } type) {
				types.Add (type);
			}
			AddInstantiation (reader, provider, handle);
			break;
		case HandleKind.MethodSpecification:
			Add (reader, provider, reader.GetMethodSpecification ((MethodSpecificationHandle) handle).Method);
			break;
		case HandleKind.MemberReference:
			var member = reader.GetMemberReference ((MemberReferenceHandle) handle);
			if (member.GetKind () != MemberReferenceKind.Method || GetTypeKey (reader, provider, member.Parent) is not { } declaringType) {
				break;
			}
			var signature = member.DecodeMethodSignature (provider, GenericContext.Empty);
			methods.Add ($"{declaringType}::{reader.GetString (member.Name)}({string.Join (",", signature.ParameterTypes.Select (p => p.CanonicalName))})");
			// A type whose code was compiled survives even without a runtime type (for example a static class).
			types.Add (declaringType);
			AddInstantiation (reader, provider, member.Parent);
			break;
		}
	}

	void AddInstantiation (MetadataReader reader, SignatureTypeProvider provider, EntityHandle handle)
	{
		if (handle.Kind == HandleKind.TypeSpecification &&
				provider.GetTypeFromSpecification (reader, GenericContext.Empty, (TypeSpecificationHandle) handle, 0) is GenericInstanceSignatureType { Definition: NamedSignatureType definition } instance) {
			instantiations.Add ($"[{definition.Assembly}]{instance.FullName}");
		}
	}

	static string? GetTypeKey (MetadataReader reader, SignatureTypeProvider provider, EntityHandle handle)
	{
		var type = handle.Kind switch {
			HandleKind.TypeReference => provider.GetTypeFromReference (reader, (TypeReferenceHandle) handle, 0),
			HandleKind.TypeSpecification => provider.GetTypeFromSpecification (reader, GenericContext.Empty, (TypeSpecificationHandle) handle, 0),
			_ => null,
		};
		if (type is GenericInstanceSignatureType instance) {
			type = instance.Definition;
		}
		return type is NamedSignatureType named ? $"[{named.Assembly}]{named.FullName}" : null;
	}

	public bool IsKept (TypeMetadata type) => types.Contains ($"[{type.AssemblyName}]{type.FullName}");

	public bool IsKept (TypeMetadata definition, string instantiation) => instantiations.Contains ($"[{definition.AssemblyName}]{instantiation}");

	public bool IsKept (MemberMetadata member)
	{
		if (member is not MethodMetadata method) {
			throw new NotSupportedException ("only types and methods are visible in ILC's compiled output; use By = Tool.Trimmer.");
		}
		return methods.Contains ($"[{method.DeclaringType.AssemblyName}]{method.DeclaringType.FullName}::{method.Name}({string.Join (",", method.Parameters.Select (p => p.CanonicalName))})");
	}
}

/// <summary>An expectation attribute from <c>Microsoft.Android.TrimmingTests.Expectations</c>.</summary>
sealed record ExpectationAttribute (string Name, object? [] Arguments, int By)
{
	public string GetString (int index)
		=> Arguments [index] as string ?? throw new InvalidOperationException ($"[{Name}] argument {index} must be a string.");

	public string [] GetStrings (int index)
		=> Arguments [index] as string [] ?? throw new InvalidOperationException ($"[{Name}] argument {index} must be a string array.");
}

/// <summary>The types and members of an assembly, read with System.Reflection.Metadata.</summary>
sealed class AssemblyMetadata
{
	const string ExpectationsNamespace = "Microsoft.Android.TrimmingTests.Expectations";
	const int AllTools = 7;

	public string Name { get; }
	/// <summary>Whether the assembly declares <c>[assembly: AssemblyMetadata ("IsTrimmable", "True")]</c>.</summary>
	public bool DeclaresIsTrimmable { get; }
	public IReadOnlyList<ExpectationAttribute> Expectations { get; }
	/// <summary>Types by full name (<c>Namespace.Name</c>, with <c>/</c> separating nested types).</summary>
	public IReadOnlyDictionary<string, TypeMetadata> Types { get; }

	AssemblyMetadata (string name, bool declaresIsTrimmable, IReadOnlyList<ExpectationAttribute> expectations, IReadOnlyDictionary<string, TypeMetadata> types)
	{
		Name = name;
		DeclaresIsTrimmable = declaresIsTrimmable;
		Expectations = expectations;
		Types = types;
	}

	public static AssemblyMetadata Read (string path)
	{
		using var stream = File.OpenRead (path);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var name = reader.GetString (reader.GetAssemblyDefinition ().Name);

		var types = new Dictionary<TypeDefinitionHandle, TypeMetadata> ();
		foreach (var handle in reader.TypeDefinitions) {
			GetType (handle);
		}
		return new AssemblyMetadata (name, ReadIsTrimmable (reader), ReadExpectations (reader, reader.GetAssemblyDefinition ().GetCustomAttributes ()),
			types.Values.ToDictionary (t => t.FullName, StringComparer.Ordinal));

		TypeMetadata GetType (TypeDefinitionHandle handle)
		{
			if (types.TryGetValue (handle, out var type)) {
				return type;
			}
			var definition = reader.GetTypeDefinition (handle);
			var declaringHandle = definition.GetDeclaringType ();
			var declaring = declaringHandle.IsNil ? null : GetType (declaringHandle);
			var genericParameters = definition.GetGenericParameters ().Select (p => reader.GetString (reader.GetGenericParameter (p).Name)).ToArray ();
			type = new TypeMetadata (name, reader.GetString (definition.Namespace), reader.GetString (definition.Name), declaring, genericParameters,
				ReadExpectations (reader, definition.GetCustomAttributes ()));
			types.Add (handle, type);

			var provider = new SignatureTypeProvider (name);
			foreach (var methodHandle in definition.GetMethods ()) {
				var method = reader.GetMethodDefinition (methodHandle);
				var methodGenericParameters = method.GetGenericParameters ().Select (p => reader.GetString (reader.GetGenericParameter (p).Name)).ToArray ();
				var signature = method.DecodeSignature (provider, new GenericContext (genericParameters, methodGenericParameters));
				type.Members.Add (new MethodMetadata (type, reader.GetString (method.Name), signature.ParameterTypes, methodGenericParameters,
					ReadExpectations (reader, method.GetCustomAttributes ())));
			}
			foreach (var fieldHandle in definition.GetFields ()) {
				var field = reader.GetFieldDefinition (fieldHandle);
				type.Members.Add (new MemberMetadata (type, reader.GetString (field.Name), ReadExpectations (reader, field.GetCustomAttributes ())));
			}
			foreach (var propertyHandle in definition.GetProperties ()) {
				var property = reader.GetPropertyDefinition (propertyHandle);
				type.Members.Add (new MemberMetadata (type, reader.GetString (property.Name), ReadExpectations (reader, property.GetCustomAttributes ())));
			}
			foreach (var eventHandle in definition.GetEvents ()) {
				var @event = reader.GetEventDefinition (eventHandle);
				type.Members.Add (new MemberMetadata (type, reader.GetString (@event.Name), ReadExpectations (reader, @event.GetCustomAttributes ())));
			}
			return type;
		}
	}

	static bool ReadIsTrimmable (MetadataReader reader)
	{
		foreach (var handle in reader.GetAssemblyDefinition ().GetCustomAttributes ()) {
			var attribute = reader.GetCustomAttribute (handle);
			if (GetAttributeTypeName (reader, attribute.Constructor) != ("System.Reflection", "AssemblyMetadataAttribute")) {
				continue;
			}
			var arguments = attribute.DecodeValue (AttributeTypeProvider.Instance).FixedArguments;
			if (arguments is [{ Value: "IsTrimmable" }, { Value: string value }] && string.Equals (value, "True", StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
		}
		return false;
	}

	static IReadOnlyList<ExpectationAttribute> ReadExpectations (MetadataReader reader, CustomAttributeHandleCollection handles)
	{
		List<ExpectationAttribute>? result = null;
		foreach (var handle in handles) {
			var attribute = reader.GetCustomAttribute (handle);
			var (ns, name) = GetAttributeTypeName (reader, attribute.Constructor);
			if (ns != ExpectationsNamespace) {
				continue;
			}
			var value = attribute.DecodeValue (AttributeTypeProvider.Instance);
			var arguments = value.FixedArguments
				.Select (a => a.Value is ImmutableArray<CustomAttributeTypedArgument<string>> array ? array.Select (e => e.Value as string ?? throw new InvalidOperationException ($"[{name}] has a null string argument.")).ToArray () : a.Value)
				.ToArray ();
			int by = value.NamedArguments.Where (a => a.Name == "By").Select (a => Convert.ToInt32 (a.Value)).DefaultIfEmpty (AllTools).First ();
			(result ??= new List<ExpectationAttribute> ()).Add (new ExpectationAttribute (name, arguments, by));
		}
		return result ?? [];
	}

	static (string Namespace, string Name) GetAttributeTypeName (MetadataReader reader, EntityHandle constructor)
	{
		var type = constructor.Kind == HandleKind.MemberReference
			? reader.GetMemberReference ((MemberReferenceHandle) constructor).Parent
			: reader.GetMethodDefinition ((MethodDefinitionHandle) constructor).GetDeclaringType ();
		return type.Kind switch {
			HandleKind.TypeReference => (reader.GetString (reader.GetTypeReference ((TypeReferenceHandle) type).Namespace),
				reader.GetString (reader.GetTypeReference ((TypeReferenceHandle) type).Name)),
			HandleKind.TypeDefinition => (reader.GetString (reader.GetTypeDefinition ((TypeDefinitionHandle) type).Namespace),
				reader.GetString (reader.GetTypeDefinition ((TypeDefinitionHandle) type).Name)),
			_ => ("", ""),
		};
	}

	/// <summary>Decodes attribute arguments; the expectations only use strings, string arrays and the int-based <c>Tool</c> enum.</summary>
	sealed class AttributeTypeProvider : ICustomAttributeTypeProvider<string>
	{
		public static readonly AttributeTypeProvider Instance = new ();

		public string GetPrimitiveType (PrimitiveTypeCode typeCode) => typeCode.ToString ();
		public string GetSystemType () => "System.Type";
		public string GetSZArrayType (string elementType) => elementType + "[]";
		public string GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => reader.GetString (reader.GetTypeDefinition (handle).Name);
		public string GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => reader.GetString (reader.GetTypeReference (handle).Name);
		public string GetTypeFromSerializedName (string name) => name;
		public PrimitiveTypeCode GetUnderlyingEnumType (string type) => PrimitiveTypeCode.Int32;
		public bool IsSystemType (string type) => type == "System.Type";
	}
}

sealed class TypeMetadata
{
	public TypeMetadata (string assemblyName, string @namespace, string name, TypeMetadata? declaringType, string [] genericParameters,
		IReadOnlyList<ExpectationAttribute> expectations)
	{
		AssemblyName = assemblyName;
		Namespace = @namespace;
		Name = name;
		DeclaringType = declaringType;
		GenericParameters = genericParameters;
		Expectations = expectations;
		FullName = declaringType is not null ? $"{declaringType.FullName}/{name}" : @namespace.Length == 0 ? name : $"{@namespace}.{name}";
	}

	public string AssemblyName { get; }
	public string Namespace { get; }
	public string Name { get; }
	public string FullName { get; }
	public TypeMetadata? DeclaringType { get; }
	public string [] GenericParameters { get; }
	public IReadOnlyList<ExpectationAttribute> Expectations { get; }
	public List<MemberMetadata> Members { get; } = new ();

	/// <summary>Finds a method by <c>Name(ParameterType,...)</c>, or a field, property or event by name.</summary>
	public MemberMetadata? FindMember (string signature)
		=> signature.Contains ('(')
			? Members.OfType<MethodMetadata> ().FirstOrDefault (m => m.Signature == signature)
			: Members.FirstOrDefault (m => m is not MethodMetadata && m.Name == signature);
}

class MemberMetadata
{
	public MemberMetadata (TypeMetadata declaringType, string name, IReadOnlyList<ExpectationAttribute> expectations)
	{
		DeclaringType = declaringType;
		Name = name;
		Expectations = expectations;
	}

	public TypeMetadata DeclaringType { get; }
	public string Name { get; }
	public IReadOnlyList<ExpectationAttribute> Expectations { get; }
	public virtual string Signature => Name;
}

sealed class MethodMetadata : MemberMetadata
{
	public MethodMetadata (TypeMetadata declaringType, string name, ImmutableArray<SignatureType> parameters, string [] genericParameters,
		IReadOnlyList<ExpectationAttribute> expectations)
		: base (declaringType, name, expectations)
	{
		Parameters = parameters;
		GenericParameters = genericParameters;
	}

	public ImmutableArray<SignatureType> Parameters { get; }
	public string [] GenericParameters { get; }
	public override string Signature => $"{Name}({string.Join (",", Parameters.Select (p => p.FullName))})";
}

/// <summary>A type in a method signature.</summary>
abstract record SignatureType
{
	/// <summary><c>Namespace.Name</c>, <c>/</c> for nested types, <c>T</c> for generic parameters, e.g. <c>System.Collections.Generic.IList`1&lt;System.Int32&gt;</c>.</summary>
	public abstract string FullName { get; }
	/// <summary>Like <see cref="FullName"/>, but generic parameters by position (<c>!0</c>, <c>!!0</c>), as in member references.</summary>
	public abstract string CanonicalName { get; }
}

sealed record PrimitiveSignatureType (PrimitiveTypeCode Code) : SignatureType
{
	public override string FullName => $"System.{Code}";
	public override string CanonicalName => FullName;
}

sealed record NamedSignatureType (string Assembly, string Namespace, string Name, NamedSignatureType? DeclaringType) : SignatureType
{
	public override string FullName => DeclaringType is not null ? $"{DeclaringType.FullName}/{Name}" : Namespace.Length == 0 ? Name : $"{Namespace}.{Name}";
	public override string CanonicalName => FullName;
}

sealed record GenericInstanceSignatureType (SignatureType Definition, ImmutableArray<SignatureType> Arguments) : SignatureType
{
	public override string FullName => $"{Definition.FullName}<{string.Join (",", Arguments.Select (a => a.FullName))}>";
	public override string CanonicalName => $"{Definition.CanonicalName}<{string.Join (",", Arguments.Select (a => a.CanonicalName))}>";
}

sealed record ArraySignatureType (SignatureType Element, int Rank, bool IsVector) : SignatureType
{
	public override string FullName => Element.FullName + (IsVector ? "[]" : $"[{new string (',', Rank - 1)}]");
	public override string CanonicalName => Element.CanonicalName + (IsVector ? "[]" : $"[{new string (',', Rank - 1)}]");
}

sealed record ByReferenceSignatureType (SignatureType Element) : SignatureType
{
	public override string FullName => Element.FullName + "&";
	public override string CanonicalName => Element.CanonicalName + "&";
}

sealed record PointerSignatureType (SignatureType Element) : SignatureType
{
	public override string FullName => Element.FullName + "*";
	public override string CanonicalName => Element.CanonicalName + "*";
}

sealed record GenericParameterSignatureType (string Name, int Index, bool IsMethodParameter) : SignatureType
{
	public override string FullName => Name;
	public override string CanonicalName => IsMethodParameter ? $"!!{Index}" : $"!{Index}";
}

sealed record FunctionPointerSignatureType () : SignatureType
{
	public override string FullName => "method";
	public override string CanonicalName => "method";
}

sealed record GenericContext (string [] TypeParameters, string [] MethodParameters)
{
	public static readonly GenericContext Empty = new ([], []);
}

sealed class SignatureTypeProvider : ISignatureTypeProvider<SignatureType, GenericContext>
{
	readonly string assemblyName;

	/// <param name="assemblyName">The assembly whose metadata is decoded; it declares the types referenced by definition.</param>
	public SignatureTypeProvider (string assemblyName) => this.assemblyName = assemblyName;

	public SignatureType GetArrayType (SignatureType elementType, ArrayShape shape) => new ArraySignatureType (elementType, shape.Rank, IsVector: false);
	public SignatureType GetByReferenceType (SignatureType elementType) => new ByReferenceSignatureType (elementType);
	public SignatureType GetFunctionPointerType (MethodSignature<SignatureType> signature) => new FunctionPointerSignatureType ();
	public SignatureType GetGenericInstantiation (SignatureType genericType, ImmutableArray<SignatureType> typeArguments) => new GenericInstanceSignatureType (genericType, typeArguments);
	public SignatureType GetGenericMethodParameter (GenericContext genericContext, int index)
		=> new GenericParameterSignatureType (index < genericContext.MethodParameters.Length ? genericContext.MethodParameters [index] : $"!!{index}", index, IsMethodParameter: true);
	public SignatureType GetGenericTypeParameter (GenericContext genericContext, int index)
		=> new GenericParameterSignatureType (index < genericContext.TypeParameters.Length ? genericContext.TypeParameters [index] : $"!{index}", index, IsMethodParameter: false);
	public SignatureType GetModifiedType (SignatureType modifier, SignatureType unmodifiedType, bool isRequired) => unmodifiedType;
	public SignatureType GetPinnedType (SignatureType elementType) => elementType;
	public SignatureType GetPointerType (SignatureType elementType) => new PointerSignatureType (elementType);
	public SignatureType GetPrimitiveType (PrimitiveTypeCode typeCode) => new PrimitiveSignatureType (typeCode);
	public SignatureType GetSZArrayType (SignatureType elementType) => new ArraySignatureType (elementType, 1, IsVector: true);

	public SignatureType GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
	{
		var definition = reader.GetTypeDefinition (handle);
		var declaring = definition.GetDeclaringType ();
		return new NamedSignatureType (assemblyName, reader.GetString (definition.Namespace), reader.GetString (definition.Name),
			declaring.IsNil ? null : (NamedSignatureType) GetTypeFromDefinition (reader, declaring, rawTypeKind));
	}

	public SignatureType GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
	{
		var reference = reader.GetTypeReference (handle);
		var scope = reference.ResolutionScope;
		var declaring = scope.Kind == HandleKind.TypeReference
			? (NamedSignatureType) GetTypeFromReference (reader, (TypeReferenceHandle) scope, rawTypeKind)
			: null;
		var assembly = declaring?.Assembly ?? (scope.Kind == HandleKind.AssemblyReference
			? reader.GetString (reader.GetAssemblyReference ((AssemblyReferenceHandle) scope).Name)
			: assemblyName);
		return new NamedSignatureType (assembly, reader.GetString (reference.Namespace), reader.GetString (reference.Name), declaring);
	}

	public SignatureType GetTypeFromSpecification (MetadataReader reader, GenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
		=> reader.GetTypeSpecification (handle).DecodeSignature (this, genericContext);
}
