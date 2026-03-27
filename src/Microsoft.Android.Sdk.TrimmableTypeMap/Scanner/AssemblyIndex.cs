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
				registerInfo = registerInfo with { JniName = registerInfo.JniName.Replace ('.', '/') };
			} else if (attrName == "ExportAttribute") {
				// [Export] is a method-level attribute; it is parsed at scan time by JavaPeerScanner
			} else if (IsKnownComponentAttribute (attrName)) {
				attrInfo ??= CreateTypeAttributeInfo (attrName);
				var value = DecodeAttribute (ca);

				// Capture all named properties
				foreach (var named in value.NamedArguments) {
					if (named.Name is not null) {
						attrInfo.Properties [named.Name] = named.Value;
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
				attrInfo ??= new TypeAttributeInfo ("IntentFilterAttribute");
				attrInfo.IntentFilters.Add (ParseIntentFilterAttribute (ca));
			} else if (attrName == "MetaDataAttribute") {
				attrInfo ??= new TypeAttributeInfo ("MetaDataAttribute");
				attrInfo.MetaData.Add (ParseMetaDataAttribute (ca));
			} else if (attrInfo is null && ImplementsJniNameProviderAttribute (ca)) {
				// Custom attribute implementing IJniNameProviderAttribute (e.g., user-defined [CustomJniName])
				var name = TryGetNameProperty (ca);
				if (name is not null) {
					attrInfo = new TypeAttributeInfo (attrName);
					attrInfo.JniName = name.Replace ('.', '/');
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

	MetaDataInfo ParseMetaDataAttribute (CustomAttribute ca)
	{
		var value = DecodeAttribute (ca);

		string name = "";
		if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is string nameArg) {
			name = nameArg;
		}

		string? metaValue = null;
		string? resource = null;
		TryGetNamedArgument<string> (value, "Value", out metaValue);
		TryGetNamedArgument<string> (value, "Resource", out resource);

		return new MetaDataInfo {
			Name = name,
			Value = metaValue,
			Resource = resource,
		};
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
	internal void ScanAssemblyAttributes (AssemblyManifestInfo info)
	{
		var asmDef = Reader.GetAssemblyDefinition ();
		foreach (var caHandle in asmDef.GetCustomAttributes ()) {
			var ca = Reader.GetCustomAttribute (caHandle);
			var attrName = GetCustomAttributeName (ca, Reader);
			if (attrName is null) {
				continue;
			}

			switch (attrName) {
			case "PermissionAttribute":
				info.Permissions.Add (ParsePermissionAttribute (ca));
				break;
			case "PermissionGroupAttribute":
				info.PermissionGroups.Add (ParsePermissionGroupAttribute (ca));
				break;
			case "PermissionTreeAttribute":
				info.PermissionTrees.Add (ParsePermissionTreeAttribute (ca));
				break;
			case "UsesPermissionAttribute":
				info.UsesPermissions.Add (ParseUsesPermissionAttribute (ca));
				break;
			case "UsesFeatureAttribute":
				info.UsesFeatures.Add (ParseUsesFeatureAttribute (ca));
				break;
			case "UsesLibraryAttribute":
				info.UsesLibraries.Add (ParseUsesLibraryAttribute (ca));
				break;
			case "UsesConfigurationAttribute":
				info.UsesConfigurations.Add (ParseUsesConfigurationAttribute (ca));
				break;
			case "MetaDataAttribute":
				info.MetaData.Add (ParseMetaDataAttribute (ca));
				break;
			case "PropertyAttribute":
				info.Properties.Add (ParsePropertyAttribute (ca));
				break;
			case "ApplicationAttribute":
				info.ApplicationProperties ??= new Dictionary<string, object?> (StringComparer.Ordinal);
				var appValue = DecodeAttribute (ca);
				foreach (var named in appValue.NamedArguments) {
					if (named.Name is not null) {
						info.ApplicationProperties [named.Name] = named.Value;
					}
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

	PermissionInfo ParsePermissionAttribute (CustomAttribute ca)
	{
		var (name, props) = ParseNameAndProperties (ca);
		return new PermissionInfo { Name = name, Properties = props };
	}

	PermissionGroupInfo ParsePermissionGroupAttribute (CustomAttribute ca)
	{
		var (name, props) = ParseNameAndProperties (ca);
		return new PermissionGroupInfo { Name = name, Properties = props };
	}

	PermissionTreeInfo ParsePermissionTreeAttribute (CustomAttribute ca)
	{
		var (name, props) = ParseNameAndProperties (ca);
		return new PermissionTreeInfo { Name = name, Properties = props };
	}

	UsesPermissionInfo ParseUsesPermissionAttribute (CustomAttribute ca)
	{
		var value = DecodeAttribute (ca);
		string name = "";
		int? maxSdk = null;
		if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is string n) {
			name = n;
		}
		foreach (var named in value.NamedArguments) {
			if (named.Name == "Name" && named.Value is string nameVal) {
				name = nameVal;
			} else if (named.Name == "MaxSdkVersion" && named.Value is int max) {
				maxSdk = max;
			}
		}
		return new UsesPermissionInfo { Name = name, MaxSdkVersion = maxSdk };
	}

	UsesFeatureInfo ParseUsesFeatureAttribute (CustomAttribute ca)
	{
		var value = DecodeAttribute (ca);
		string? name = null;
		int glesVersion = 0;
		bool required = true;
		foreach (var named in value.NamedArguments) {
			if (named.Name == "Name" && named.Value is string n) {
				name = n;
			} else if (named.Name == "GLESVersion" && named.Value is int v) {
				glesVersion = v;
			} else if (named.Name == "Required" && named.Value is bool r) {
				required = r;
			}
		}
		return new UsesFeatureInfo { Name = name, GLESVersion = glesVersion, Required = required };
	}

	UsesLibraryInfo ParseUsesLibraryAttribute (CustomAttribute ca)
	{
		var value = DecodeAttribute (ca);
		string name = "";
		bool required = true;
		if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is string n) {
			name = n;
		}
		foreach (var named in value.NamedArguments) {
			if (named.Name == "Name" && named.Value is string nameVal) {
				name = nameVal;
			} else if (named.Name == "Required" && named.Value is bool r) {
				required = r;
			}
		}
		return new UsesLibraryInfo { Name = name, Required = required };
	}

	UsesConfigurationInfo ParseUsesConfigurationAttribute (CustomAttribute ca)
	{
		var value = DecodeAttribute (ca);
		bool reqFiveWayNav = false;
		bool reqHardKeyboard = false;
		string? reqKeyboardType = null;
		string? reqNavigation = null;
		string? reqTouchScreen = null;
		foreach (var named in value.NamedArguments) {
			switch (named.Name) {
			case "ReqFiveWayNav" when named.Value is bool b: reqFiveWayNav = b; break;
			case "ReqHardKeyboard" when named.Value is bool b: reqHardKeyboard = b; break;
			case "ReqKeyboardType" when named.Value is string s: reqKeyboardType = s; break;
			case "ReqNavigation" when named.Value is string s: reqNavigation = s; break;
			case "ReqTouchScreen" when named.Value is string s: reqTouchScreen = s; break;
			}
		}
		return new UsesConfigurationInfo {
			ReqFiveWayNav = reqFiveWayNav,
			ReqHardKeyboard = reqHardKeyboard,
			ReqKeyboardType = reqKeyboardType,
			ReqNavigation = reqNavigation,
			ReqTouchScreen = reqTouchScreen,
		};
	}

	PropertyInfo ParsePropertyAttribute (CustomAttribute ca)
	{
		var value = DecodeAttribute (ca);
		string name = "";
		string? propValue = null;
		string? resource = null;
		if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is string n) {
			name = n;
		}
		foreach (var named in value.NamedArguments) {
			switch (named.Name) {
			case "Name" when named.Value is string s: name = s; break;
			case "Value" when named.Value is string s: propValue = s; break;
			case "Resource" when named.Value is string s: resource = s; break;
			}
		}
		return new PropertyInfo { Name = name, Value = propValue, Resource = resource };
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
