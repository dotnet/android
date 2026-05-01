using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

	AssemblyIndex (PEReader peReader, MetadataReader reader, string assemblyName)
	{
		this.peReader = peReader;
		this.customAttributeTypeProvider = new CustomAttributeTypeProvider (reader);
		Reader = reader;
		AssemblyName = assemblyName;
	}

	public static AssemblyIndex Create (PEReader peReader, string assemblyName)
	{
		var reader = peReader.GetMetadataReader ();
		var index = new AssemblyIndex (peReader, reader, assemblyName);
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

		// Collect intent filters and metadata separately to avoid ordering issues:
		// if [IntentFilter] appears before [Activity], we must not create attrInfo
		// with the wrong AttributeName.
		List<IntentFilterInfo>? intentFilters = null;
		List<MetaDataInfo>? metaData = null;

		foreach (var caHandle in typeDef.GetCustomAttributes ()) {
			var ca = Reader.GetCustomAttribute (caHandle);
			var attrName = GetCustomAttributeName (ca, Reader);

			if (attrName is null) {
				continue;
			}

			if (attrName == "RegisterAttribute") {
				registerInfo = ParseRegisterAttribute (ca);
				registerInfo = registerInfo with { JniName = registerInfo.JniName.Replace ('.', '/') };
			} else if (attrName == "JniTypeSignatureAttribute") {
				registerInfo = ParseJniTypeSignatureAttribute (ca);
			} else if (attrName == "ExportAttribute") {
				// [Export] is a method-level attribute; it is parsed at scan time by JavaPeerScanner
			} else if (IsKnownComponentAttribute (attrName)) {
				attrInfo ??= CreateTypeAttributeInfo (attrName);
				var value = DecodeAttribute (ca);

				if (attrName == "ContentProviderAttribute") {
					AddContentProviderAuthorities (value, attrInfo.Properties);
				}

				// Capture all named properties
				foreach (var named in value.NamedArguments) {
					if (named.Name is not null) {
						attrInfo.Properties [named.Name] = GetComponentPropertyValue (named.Name, named.Value);
					}
				}

				var name = TryGetNameFromDecodedAttribute (value);
				if (name is not null) {
					attrInfo.JniName = name.Replace ('.', '/');
				}
				if (attrInfo is ApplicationAttributeInfo applicationAttributeInfo) {
					if (TryGetNamedArgument<string> (value, "BackupAgent", out var backupAgent)) {
						applicationAttributeInfo.BackupAgent = backupAgent;
					}
					if (TryGetNamedArgument<string> (value, "ManageSpaceActivity", out var manageSpace)) {
						applicationAttributeInfo.ManageSpaceActivity = manageSpace;
					}
				}
			} else if (attrName == "IntentFilterAttribute") {
				intentFilters ??= new List<IntentFilterInfo> ();
				intentFilters.Add (ParseIntentFilterAttribute (ca));
			} else if (attrName == "MetaDataAttribute") {
				metaData ??= new List<MetaDataInfo> ();
				var (mdName, mdProps) = ParseNameAndProperties (ca);
				metaData.Add (CreateMetaDataInfo (mdName, mdProps));
			} else if (attrInfo is null && ImplementsJniNameProviderAttribute (ca)) {
				// Custom attribute implementing IJniNameProviderAttribute (e.g., user-defined [CustomJniName])
				var name = TryGetNameProperty (ca);
				if (name is not null) {
					attrInfo = new TypeAttributeInfo (attrName);
					attrInfo.JniName = name.Replace ('.', '/');
				}
			}
		}

		// Attach collected intent filters and metadata to the component attribute
		if (attrInfo is not null) {
			if (intentFilters is not null) {
				attrInfo.IntentFilters.AddRange (intentFilters);
			}
			if (metaData is not null) {
				attrInfo.MetaData.AddRange (metaData);
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

	static TypeAttributeInfo CreateTypeAttributeInfo (string attrName) => attrName switch {
		"ApplicationAttribute" => new ApplicationAttributeInfo (),
		"InstrumentationAttribute" => new InstrumentationAttributeInfo (),
		_ => new TypeAttributeInfo (attrName),
	};

	static bool IsKnownComponentAttribute (string attrName) => KnownComponentAttributes.Contains (attrName);

	/// <summary>
	/// Checks whether a custom attribute's type implements <c>Java.Interop.IJniNameProviderAttribute</c>.
	/// Only works for attributes defined in the assembly being scanned (MethodDefinition constructors).
	/// </summary>
	bool ImplementsJniNameProviderAttribute (CustomAttribute ca)
	{
		if (ca.Constructor.Kind != HandleKind.MethodDefinition) {
			return false;
		}
		var methodDef = Reader.GetMethodDefinition ((MethodDefinitionHandle)ca.Constructor);
		var typeDef = Reader.GetTypeDefinition (methodDef.GetDeclaringType ());
		foreach (var implHandle in typeDef.GetInterfaceImplementations ()) {
			var impl = Reader.GetInterfaceImplementation (implHandle);
			if (impl.Interface.Kind == HandleKind.TypeReference) {
				var typeRef = Reader.GetTypeReference ((TypeReferenceHandle)impl.Interface);
				if (Reader.GetString (typeRef.Name) == "IJniNameProviderAttribute" &&
				    Reader.GetString (typeRef.Namespace) == "Java.Interop") {
					return true;
				}
			} else if (impl.Interface.Kind == HandleKind.TypeDefinition) {
				var ifaceDef = Reader.GetTypeDefinition ((TypeDefinitionHandle)impl.Interface);
				if (Reader.GetString (ifaceDef.Name) == "IJniNameProviderAttribute" &&
				    Reader.GetString (ifaceDef.Namespace) == "Java.Interop") {
					return true;
				}
			}
		}
		return false;
	}

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

	internal RegisterInfo ParseJniTypeSignatureAttribute (CustomAttribute ca)
	{
		var value = DecodeAttribute (ca);

		string jniName = "";
		bool doNotGenerateAcw = false;

		if (value.FixedArguments.Length > 0) {
			jniName = (string?)value.FixedArguments [0].Value ?? "";
		}

		if (TryGetNamedArgument<bool> (value, "GenerateJavaPeer", out var generateJavaPeer)) {
			doNotGenerateAcw = !generateJavaPeer;
		}

		var isArrayType = TryGetNamedArgument<int> (value, "ArrayRank", out var rank) && rank > 0;

		return new RegisterInfo {
			JniName = jniName.Replace ('.', '/'),
			DoNotGenerateAcw = doNotGenerateAcw,
			IsFromJniTypeSignature = true,
			IsArrayType = isArrayType,
		};
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
		return TryGetNameFromDecodedAttribute (value);
	}

	static string? TryGetNameFromDecodedAttribute (CustomAttributeValue<string> value)
	{
		if (TryGetNamedArgument<string> (value, "Name", out var name) && !string.IsNullOrEmpty (name)) {
			return name;
		}

		// Fall back to first constructor argument (e.g., [CustomJniName("...")])
		if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is string ctorName && !string.IsNullOrEmpty (ctorName)) {
			return ctorName;
		}

		return null;
	}

	IntentFilterInfo ParseIntentFilterAttribute (CustomAttribute ca)
	{
		var value = DecodeAttribute (ca);

		// First ctor argument is string[] actions
		var actions = new List<string> ();
		if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is IReadOnlyCollection<CustomAttributeTypedArgument<string>> actionArgs) {
			foreach (var arg in actionArgs) {
				if (arg.Value is string action) {
					actions.Add (action);
				}
			}
		}

		var categories = new List<string> ();
		// Categories is a string[] property — the SRM decoder sees it as
		// IReadOnlyCollection<CustomAttributeTypedArgument<string>>, not string.
		foreach (var named in value.NamedArguments) {
			if (named.Name == "Categories" && named.Value is IReadOnlyCollection<CustomAttributeTypedArgument<string>> catArgs) {
				foreach (var arg in catArgs) {
					if (arg.Value is string cat) {
						categories.Add (cat);
					}
				}
			}
		}

		var properties = new Dictionary<string, object?> (StringComparer.Ordinal);
		foreach (var named in value.NamedArguments) {
			if (named.Name is not null && named.Name != "Categories") {
				properties [named.Name] = named.Value;
			}
		}

		return new IntentFilterInfo {
			Actions = actions,
			Categories = categories,
			Properties = properties,
		};
	}

	static void AddContentProviderAuthorities (CustomAttributeValue<string> value, Dictionary<string, object?> properties)
	{
		if (value.FixedArguments.Length == 0) {
			return;
		}

		if (TryGetStringArray (value.FixedArguments [0].Value, out var authorities)) {
			properties ["Authorities"] = string.Join (";", authorities);
		}
	}

	static object? GetComponentPropertyValue (string name, object? value)
	{
		if (name == "Authorities" && TryGetStringArray (value, out var authorities)) {
			return string.Join (";", authorities);
		}

		return value;
	}

	static bool TryGetStringArray (object? value, [NotNullWhen (true)] out List<string>? strings)
	{
		if (value is IReadOnlyCollection<CustomAttributeTypedArgument<string>> args) {
			strings = new List<string> (args.Count);
			foreach (var arg in args) {
				if (arg.Value is string s) {
					strings.Add (s);
				}
			}
			return true;
		}

		strings = null;
		return false;
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

	/// <summary>
	/// Scans assembly-level custom attributes for manifest-related data.
	/// </summary>
	static readonly HashSet<string> KnownAssemblyAttributes = new (StringComparer.Ordinal) {
		"PermissionAttribute",
		"PermissionGroupAttribute",
		"PermissionTreeAttribute",
		"UsesPermissionAttribute",
		"UsesFeatureAttribute",
		"UsesLibraryAttribute",
		"UsesConfigurationAttribute",
		"MetaDataAttribute",
		"PropertyAttribute",
		"SupportsGLTextureAttribute",
		"ApplicationAttribute",
	};

	internal void ScanAssemblyAttributes (AssemblyManifestInfo info)
	{
		var asmDef = Reader.GetAssemblyDefinition ();
		foreach (var caHandle in asmDef.GetCustomAttributes ()) {
			var ca = Reader.GetCustomAttribute (caHandle);
			var attrName = GetCustomAttributeName (ca, Reader);
			if (attrName is null || !KnownAssemblyAttributes.Contains (attrName)) {
				continue;
			}

			var (name, props) = ParseNameAndProperties (ca);

			switch (attrName) {
			case "PermissionAttribute":
				info.Permissions.Add (new PermissionInfo { Name = name, Properties = props });
				break;
			case "PermissionGroupAttribute":
				info.PermissionGroups.Add (new PermissionGroupInfo { Name = name, Properties = props });
				break;
			case "PermissionTreeAttribute":
				info.PermissionTrees.Add (new PermissionTreeInfo { Name = name, Properties = props });
				break;
			case "UsesPermissionAttribute":
				info.UsesPermissions.Add (CreateUsesPermissionInfo (name, props));
				break;
			case "UsesFeatureAttribute":
				info.UsesFeatures.Add (CreateUsesFeatureInfo (name, props));
				break;
			case "UsesLibraryAttribute":
				info.UsesLibraries.Add (CreateUsesLibraryInfo (name, props));
				break;
			case "UsesConfigurationAttribute":
				info.UsesConfigurations.Add (CreateUsesConfigurationInfo (props));
				break;
			case "MetaDataAttribute":
				info.MetaData.Add (CreateMetaDataInfo (name, props));
				break;
			case "PropertyAttribute":
				info.Properties.Add (CreatePropertyInfo (name, props));
				break;
			case "SupportsGLTextureAttribute":
				if (name.Length > 0) {
					info.SupportsGLTextures.Add (new SupportsGLTextureInfo { Name = name });
				}
				break;
			case "ApplicationAttribute":
				info.ApplicationProperties ??= new Dictionary<string, object?> (StringComparer.Ordinal);
				foreach (var kvp in props) {
					info.ApplicationProperties [kvp.Key] = kvp.Value;
				}
				break;
			}
		}
	}

	(string name, Dictionary<string, object?> props) ParseNameAndProperties (CustomAttribute ca)
	{
		var value = DecodeAttribute (ca);
		string name = "";
		var props = new Dictionary<string, object?> (StringComparer.Ordinal);
		if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is string ctorName) {
			name = ctorName;
		}
		// Handle 2-arg ctors like UsesLibrary(string, bool) — store extra ctor args in props
		for (int i = 1; i < value.FixedArguments.Length; i++) {
			if (value.FixedArguments [i].Value is bool boolVal) {
				props ["Required"] = boolVal;
			}
		}
		foreach (var named in value.NamedArguments) {
			if (named.Name == "Name" && named.Value is string n) {
				name = n;
			}
			if (named.Name is not null) {
				props [named.Name] = named.Value;
			}
		}
		return (name, props);
	}

	static UsesPermissionInfo CreateUsesPermissionInfo (string name, Dictionary<string, object?> props)
	{
		int? maxSdk = props.TryGetValue ("MaxSdkVersion", out var v) && v is int max ? max : null;
		string? flags = props.TryGetValue ("UsesPermissionFlags", out var f) && f is string s ? s : null;
		return new UsesPermissionInfo { Name = name, MaxSdkVersion = maxSdk, UsesPermissionFlags = flags };
	}

	static UsesFeatureInfo CreateUsesFeatureInfo (string name, Dictionary<string, object?> props)
	{
		var required = !props.TryGetValue ("Required", out var r) || r is not bool req || req;
		var glesVersion = props.TryGetValue ("GLESVersion", out var g) && g is int gles ? gles : 0;
		return new UsesFeatureInfo {
			Name = name.Length > 0 ? name : null,
			GLESVersion = glesVersion,
			Required = required,
		};
	}

	static UsesLibraryInfo CreateUsesLibraryInfo (string name, Dictionary<string, object?> props)
	{
		var required = !props.TryGetValue ("Required", out var r) || r is not bool req || req;
		return new UsesLibraryInfo { Name = name, Required = required };
	}

	static UsesConfigurationInfo CreateUsesConfigurationInfo (Dictionary<string, object?> props)
	{
		return new UsesConfigurationInfo {
			ReqFiveWayNav = props.TryGetValue ("ReqFiveWayNav", out var v1) && v1 is bool b1 && b1,
			ReqHardKeyboard = props.TryGetValue ("ReqHardKeyboard", out var v2) && v2 is bool b2 && b2,
			ReqKeyboardType = props.TryGetValue ("ReqKeyboardType", out var v3) && v3 is string s3 ? s3 : null,
			ReqNavigation = props.TryGetValue ("ReqNavigation", out var v4) && v4 is string s4 ? s4 : null,
			ReqTouchScreen = props.TryGetValue ("ReqTouchScreen", out var v5) && v5 is string s5 ? s5 : null,
		};
	}

	static MetaDataInfo CreateMetaDataInfo (string name, Dictionary<string, object?> props)
	{
		return new MetaDataInfo {
			Name = name,
			Value = props.TryGetValue ("Value", out var v) && v is string val ? val : null,
			Resource = props.TryGetValue ("Resource", out var r) && r is string res ? res : null,
		};
	}

	static PropertyInfo CreatePropertyInfo (string name, Dictionary<string, object?> props)
	{
		return new PropertyInfo {
			Name = name,
			Value = props.TryGetValue ("Value", out var v) && v is string val ? val : null,
			Resource = props.TryGetValue ("Resource", out var r) && r is string res ? res : null,
		};
	}

	public void Dispose ()
	{
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
	public bool IsFromJniTypeSignature { get; init; }
	public bool IsArrayType { get; init; }
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

	/// <summary>
	/// All named property values from the component attribute.
	/// </summary>
	public Dictionary<string, object?> Properties { get; } = new (StringComparer.Ordinal);

	/// <summary>
	/// Intent filters declared on this type via [IntentFilter] attributes.
	/// </summary>
	public List<IntentFilterInfo> IntentFilters { get; } = [];

	/// <summary>
	/// Metadata entries declared on this type via [MetaData] attributes.
	/// </summary>
	public List<MetaDataInfo> MetaData { get; } = [];
}

sealed class ApplicationAttributeInfo () : TypeAttributeInfo ("ApplicationAttribute")
{
	public string? BackupAgent { get; set; }
	public string? ManageSpaceActivity { get; set; }
}

sealed class InstrumentationAttributeInfo () : TypeAttributeInfo ("InstrumentationAttribute") { }
