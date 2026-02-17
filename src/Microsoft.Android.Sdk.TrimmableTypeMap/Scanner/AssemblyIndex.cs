using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Phase 1 index for a single assembly. Built in one pass over TypeDefinitions,
/// all subsequent lookups are O(1) dictionary lookups.
/// </summary>
sealed class AssemblyIndex : IDisposable
{
	readonly PEReader peReader;
	readonly CustomAttributeTypeProvider customAttributeTypeProvider;

	public MetadataReader Reader { get; }
	public string AssemblyName { get; }
	public string FilePath { get; }

	/// <summary>
	/// Maps full managed type name (e.g., "Android.App.Activity") to its TypeDefinitionHandle.
	/// </summary>
	public Dictionary<string, TypeDefinitionHandle> TypesByFullName { get; } = new (StringComparer.Ordinal);

	/// <summary>
	/// Cached [Register] attribute data per type.
	/// </summary>
	public Dictionary<TypeDefinitionHandle, RegisterInfo> RegisterInfoByType { get; } = new ();

	/// <summary>
	/// All custom attribute data per type, pre-parsed for the attributes we care about.
	/// </summary>
	public Dictionary<TypeDefinitionHandle, TypeAttributeInfo> AttributesByType { get; } = new ();

	AssemblyIndex (PEReader peReader, MetadataReader reader, string assemblyName, string filePath)
	{
		this.peReader = peReader;
		this.customAttributeTypeProvider = new CustomAttributeTypeProvider (reader);
		Reader = reader;
		AssemblyName = assemblyName;
		FilePath = filePath;
	}

	public static AssemblyIndex Create (string filePath)
	{
		var peReader = new PEReader (File.OpenRead (filePath));
		var reader = peReader.GetMetadataReader ();
		var assemblyName = reader.GetString (reader.GetAssemblyDefinition ().Name);
		var index = new AssemblyIndex (peReader, reader, assemblyName, filePath);
		index.Build ();
		return index;
	}

	void Build ()
	{
		foreach (var typeHandle in Reader.TypeDefinitions) {
			var typeDef = Reader.GetTypeDefinition (typeHandle);

			var fullName = MetadataTypeNameResolver.GetFullName (typeDef, Reader);
			if (fullName.Length == 0) {
				continue;
			}

			TypesByFullName [fullName] = typeHandle;

			var (registerInfo, attrInfo) = ParseAttributes (typeDef);

			if (attrInfo is not null) {
				AttributesByType [typeHandle] = attrInfo;
			}

			if (registerInfo is not null) {
				RegisterInfoByType [typeHandle] = registerInfo;
			}
		}
	}

	(RegisterInfo? register, TypeAttributeInfo? attrs) ParseAttributes (TypeDefinition typeDef)
	{
		RegisterInfo? registerInfo = null;
		TypeAttributeInfo? attrInfo = null;

		foreach (var caHandle in typeDef.GetCustomAttributes ()) {
			var ca = Reader.GetCustomAttribute (caHandle);
			var attrName = GetCustomAttributeName (ca, Reader);

			if (attrName is null) {
				continue;
			}

			if (attrName == "RegisterAttribute") {
				registerInfo = ParseRegisterAttribute (ca);
			} else if (attrName == "ExportAttribute") {
				// [Export] is a method-level attribute; it is parsed at scan time by JavaPeerScanner
			} else if (IsKnownComponentAttribute (attrName)) {
				attrInfo ??= CreateTypeAttributeInfo (attrName);
				var name = TryGetNameProperty (ca);
				if (name is not null) {
					attrInfo.JniName = name.Replace ('.', '/');
				}
				if (attrInfo is ApplicationAttributeInfo applicationAttributeInfo) {
					applicationAttributeInfo.BackupAgent = TryGetTypeProperty (ca, "BackupAgent");
					applicationAttributeInfo.ManageSpaceActivity = TryGetTypeProperty (ca, "ManageSpaceActivity");
				}
			}
		}

		return (registerInfo, attrInfo);
	}

	static readonly HashSet<string> KnownComponentAttributes = new (StringComparer.Ordinal) {
		"ActivityAttribute",
		"ServiceAttribute",
		"BroadcastReceiverAttribute",
		"ContentProviderAttribute",
		"ApplicationAttribute",
		"InstrumentationAttribute",
	};

	static TypeAttributeInfo CreateTypeAttributeInfo (string attrName)
	{
		return attrName == "ApplicationAttribute"
			? new ApplicationAttributeInfo ()
			: new TypeAttributeInfo (attrName);
	}

	static bool IsKnownComponentAttribute (string attrName) => KnownComponentAttributes.Contains (attrName);

	internal static string? GetCustomAttributeName (CustomAttribute ca, MetadataReader reader)
	{
		if (ca.Constructor.Kind == HandleKind.MemberReference) {
			var memberRef = reader.GetMemberReference ((MemberReferenceHandle)ca.Constructor);
			if (memberRef.Parent.Kind == HandleKind.TypeReference) {
				var typeRef = reader.GetTypeReference ((TypeReferenceHandle)memberRef.Parent);
				return reader.GetString (typeRef.Name);
			}
		} else if (ca.Constructor.Kind == HandleKind.MethodDefinition) {
			var methodDef = reader.GetMethodDefinition ((MethodDefinitionHandle)ca.Constructor);
			var declaringType = reader.GetTypeDefinition (methodDef.GetDeclaringType ());
			return reader.GetString (declaringType.Name);
		}
		return null;
	}

	internal RegisterInfo ParseRegisterAttribute (CustomAttribute ca)
	{
		return ParseRegisterInfo (DecodeAttribute (ca));
	}

	internal CustomAttributeValue<string> DecodeAttribute (CustomAttribute ca)
	{
		return ca.DecodeValue (customAttributeTypeProvider);
	}

	RegisterInfo ParseRegisterInfo (CustomAttributeValue<string> value)
	{

		string jniName = "";
		string? signature = null;
		string? connector = null;
		bool doNotGenerateAcw = false;

		if (value.FixedArguments.Length > 0) {
			jniName = (string?)value.FixedArguments [0].Value ?? "";
		}
		if (value.FixedArguments.Length > 1) {
			signature = (string?)value.FixedArguments [1].Value;
		}
		if (value.FixedArguments.Length > 2) {
			connector = (string?)value.FixedArguments [2].Value;
		}

		if (TryGetNamedArgument<bool> (value, "DoNotGenerateAcw", out var doNotGenerateAcwValue)) {
			doNotGenerateAcw = doNotGenerateAcwValue;
		}

		return new RegisterInfo {
			JniName = jniName,
			Signature = signature,
			Connector = connector,
			DoNotGenerateAcw = doNotGenerateAcw,
		};
	}

	string? TryGetTypeProperty (CustomAttribute ca, string propertyName)
	{
		var value = DecodeAttribute (ca);
		if (TryGetNamedArgument<string> (value, propertyName, out var typeName) && !string.IsNullOrEmpty (typeName)) {
			return typeName;
		}
		return null;
	}

	string? TryGetNameProperty (CustomAttribute ca)
	{
		var name = TryGetTypeProperty (ca, "Name");
		if (!string.IsNullOrEmpty (name)) {
			return name;
		}

		var value = DecodeAttribute (ca);

		// Fall back to first constructor argument (e.g., [CustomJniName("...")])
		if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is string ctorName && !string.IsNullOrEmpty (ctorName)) {
			return ctorName;
		}

		return null;
	}

	static bool TryGetNamedArgument<T> (CustomAttributeValue<string> value, string argumentName, [MaybeNullWhen (false)] out T argumentValue) where T : notnull
	{
		foreach (var named in value.NamedArguments) {
			if (named.Name == argumentName && named.Value is T typedValue) {
				argumentValue = typedValue;
				return true;
			}
		}
		argumentValue = default;
		return false;
	}

	public void Dispose ()
	{
		peReader.Dispose ();
	}
}

/// <summary>
/// Parsed [Register] attribute data for a type or method.
/// </summary>
sealed record RegisterInfo
{
	public required string JniName { get; init; }
	public string? Signature { get; init; }
	public string? Connector { get; init; }
	public bool DoNotGenerateAcw { get; init; }
}

/// <summary>
/// Parsed [Export] attribute data for a method.
/// </summary>
sealed record ExportInfo
{
	public IReadOnlyList<string>? ThrownNames { get; init; }
	public string? SuperArgumentsString { get; init; }
}

class TypeAttributeInfo (string attributeName)
{
	public string AttributeName { get; } = attributeName;
	public string? JniName { get; set; }
}

sealed class ApplicationAttributeInfo () : TypeAttributeInfo ("ApplicationAttribute")
{
	public string? BackupAgent { get; set; }
	public string? ManageSpaceActivity { get; set; }
}
