using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// Phase 1 index for a single assembly. Built in one pass over TypeDefinitions,
/// all subsequent lookups are O(1) dictionary lookups.
/// </summary>
sealed class AssemblyIndex : IDisposable
{
	readonly PEReader peReader;

	public MetadataReader Reader { get; }
	public string AssemblyName { get; }
	public string FilePath { get; }

	/// <summary>
	/// Maps full managed type name (e.g., "Android.App.Activity") to its TypeDefinitionHandle.
	/// </summary>
	public Dictionary<string, TypeDefinitionHandle> TypesByFullName { get; } = new (StringComparer.Ordinal);

	/// <summary>
	/// Maps JNI type name (from [Register]) to TypeDefinitionHandle.
	/// Only types with [Register] are included.
	/// </summary>
	public Dictionary<string, TypeDefinitionHandle> TypesByJniName { get; } = new (StringComparer.Ordinal);

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

			var fullName = GetFullName (typeDef);
			if (fullName.Length == 0) {
				continue;
			}

			TypesByFullName [fullName] = typeHandle;

			var (registerInfo, attrInfo) = ParseAttributes (typeDef);

			if (attrInfo != null) {
				AttributesByType [typeHandle] = attrInfo;
			}

			if (registerInfo != null) {
				RegisterInfoByType [typeHandle] = registerInfo;
				if (!string.IsNullOrEmpty (registerInfo.JniName)) {
					TypesByJniName [registerInfo.JniName] = typeHandle;
				}
			}
		}
	}

	string GetFullName (TypeDefinition typeDef)
	{
		var name = Reader.GetString (typeDef.Name);
		var ns = Reader.GetString (typeDef.Namespace);

		if (typeDef.IsNested) {
			var declaringType = Reader.GetTypeDefinition (typeDef.GetDeclaringType ());
			var parentName = GetFullName (declaringType);
			return parentName + "+" + name;
		}

		if (ns.Length == 0) {
			return name;
		}

		return ns + "." + name;
	}

	(RegisterInfo? register, TypeAttributeInfo? attrs) ParseAttributes (TypeDefinition typeDef)
	{
		RegisterInfo? registerInfo = null;
		TypeAttributeInfo? attrInfo = null;

		foreach (var caHandle in typeDef.GetCustomAttributes ()) {
			var ca = Reader.GetCustomAttribute (caHandle);
			var attrName = GetCustomAttributeName (ca);

			if (attrName == null) {
				continue;
			}

			switch (attrName) {
			case "RegisterAttribute":
				registerInfo = ParseRegisterAttribute (ca);
				break;

			case "ActivityAttribute":
			case "ServiceAttribute":
			case "BroadcastReceiverAttribute":
			case "ContentProviderAttribute":
			case "ApplicationAttribute":
			case "InstrumentationAttribute":
				attrInfo ??= new TypeAttributeInfo ();
				attrInfo.HasComponentAttribute = true;
				var componentName = TryGetNameProperty (ca);
				if (componentName != null) {
					attrInfo.ComponentAttributeJniName = componentName.Replace ('.', '/');
				}
				if (attrName == "ApplicationAttribute") {
					attrInfo.ApplicationBackupAgent = TryGetBackupAgentType (ca);
				}
				break;

			case "ExportAttribute":
				attrInfo ??= new TypeAttributeInfo ();
				attrInfo.HasExportAttribute = true;
				break;
			}
		}

		return (registerInfo, attrInfo);
	}

	string? GetCustomAttributeName (CustomAttribute ca)
	{
		if (ca.Constructor.Kind == HandleKind.MemberReference) {
			var memberRef = Reader.GetMemberReference ((MemberReferenceHandle)ca.Constructor);
			if (memberRef.Parent.Kind == HandleKind.TypeReference) {
				var typeRef = Reader.GetTypeReference ((TypeReferenceHandle)memberRef.Parent);
				return Reader.GetString (typeRef.Name);
			}
		} else if (ca.Constructor.Kind == HandleKind.MethodDefinition) {
			var methodDef = Reader.GetMethodDefinition ((MethodDefinitionHandle)ca.Constructor);
			var declaringType = Reader.GetTypeDefinition (methodDef.GetDeclaringType ());
			return Reader.GetString (declaringType.Name);
		}
		return null;
	}

	RegisterInfo ParseRegisterAttribute (CustomAttribute ca)
	{
		var value = ca.DecodeValue (new CustomAttributeTypeProvider (Reader));

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

		foreach (var named in value.NamedArguments) {
			if (named.Name == "DoNotGenerateAcw" && named.Value is bool val) {
				doNotGenerateAcw = val;
			}
		}

		return new RegisterInfo (jniName, signature, connector, doNotGenerateAcw);
	}

	string? TryGetBackupAgentType (CustomAttribute ca)
	{
		// TODO: Parse BackupAgent named argument (typeof reference)
		return null;
	}

	string? TryGetNameProperty (CustomAttribute ca)
	{
		var value = ca.DecodeValue (new CustomAttributeTypeProvider (Reader));
		foreach (var named in value.NamedArguments) {
			if (named.Name == "Name" && named.Value is string name && !string.IsNullOrEmpty (name)) {
				return name;
			}
		}
		return null;
	}

	public void Dispose ()
	{
		peReader.Dispose ();
	}
}

/// <summary>
/// Parsed [Register] or [Export] attribute data for a type or method.
/// </summary>
sealed class RegisterInfo
{
	public string JniName { get; }
	public string? Signature { get; }
	public string? Connector { get; }
	public bool DoNotGenerateAcw { get; }

	/// <summary>
	/// For [Export] methods: Java exception type names the method declares it can throw.
	/// </summary>
	public IReadOnlyList<string>? ThrownNames { get; }

	/// <summary>
	/// For [Export] methods: super constructor arguments string.
	/// </summary>
	public string? SuperArgumentsString { get; }

	public RegisterInfo (string jniName, string? signature, string? connector, bool doNotGenerateAcw,
		IReadOnlyList<string>? thrownNames = null, string? superArgumentsString = null)
	{
		JniName = jniName;
		Signature = signature;
		Connector = connector;
		DoNotGenerateAcw = doNotGenerateAcw;
		ThrownNames = thrownNames;
		SuperArgumentsString = superArgumentsString;
	}
}

/// <summary>
/// Aggregated attribute information for a type, beyond [Register].
/// </summary>
sealed class TypeAttributeInfo
{
	/// <summary>
	/// Type has [Activity], [Service], [BroadcastReceiver], [ContentProvider],
	/// [Application], or [Instrumentation].
	/// </summary>
	public bool HasComponentAttribute { get; set; }

	/// <summary>
	/// The JNI name from the Name property of a component attribute
	/// (e.g., [Activity(Name = "my.app.MainActivity")] → "my/app/MainActivity").
	/// Null if no Name was specified on the component attribute.
	/// </summary>
	public string? ComponentAttributeJniName { get; set; }

	/// <summary>
	/// Type has [Export] attribute on itself.
	/// </summary>
	public bool HasExportAttribute { get; set; }

	/// <summary>
	/// If the type has [Application(BackupAgent = typeof(X))],
	/// this is the full name of X.
	/// </summary>
	public string? ApplicationBackupAgent { get; set; }
}
