using System;
using System.Collections.Generic;
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
	internal readonly CustomAttributeTypeProvider customAttributeTypeProvider;

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

	/// <summary>
	/// Type names of attributes that implement <c>Java.Interop.IJniNameProviderAttribute</c>
	/// in this assembly. Used to detect JNI name providers without hardcoding attribute names.
	/// </summary>
	public HashSet<string> JniNameProviderAttributes { get; } = new (StringComparer.Ordinal);

	/// <summary>
	/// Merged set of all JNI name provider attribute type names across all loaded assemblies.
	/// Set by <see cref="JavaPeerScanner"/> after all assemblies are indexed.
	/// </summary>
	HashSet<string>? allJniNameProviderAttributes;

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
		FindJniNameProviderAttributes ();

		foreach (var typeHandle in Reader.TypeDefinitions) {
			var typeDef = Reader.GetTypeDefinition (typeHandle);

			var fullName = GetFullName (typeDef, Reader);
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
			}
		}
	}

	/// <summary>
	/// Finds all types in this assembly that implement <c>Java.Interop.IJniNameProviderAttribute</c>.
	/// </summary>
	void FindJniNameProviderAttributes ()
	{
		foreach (var typeHandle in Reader.TypeDefinitions) {
			var typeDef = Reader.GetTypeDefinition (typeHandle);
			if (ImplementsIJniNameProviderAttribute (typeDef)) {
				var name = Reader.GetString (typeDef.Name);
				JniNameProviderAttributes.Add (name);
			}
		}
	}

	bool ImplementsIJniNameProviderAttribute (TypeDefinition typeDef)
	{
		foreach (var implHandle in typeDef.GetInterfaceImplementations ()) {
			var impl = Reader.GetInterfaceImplementation (implHandle);
			if (impl.Interface.Kind == HandleKind.TypeReference) {
				var typeRef = Reader.GetTypeReference ((TypeReferenceHandle)impl.Interface);
				var name = Reader.GetString (typeRef.Name);
				var ns = Reader.GetString (typeRef.Namespace);
				if (name == "IJniNameProviderAttribute" && ns == "Java.Interop") {
					return true;
				}
			} else if (impl.Interface.Kind == HandleKind.TypeDefinition) {
				var ifaceTypeDef = Reader.GetTypeDefinition ((TypeDefinitionHandle)impl.Interface);
				var name = Reader.GetString (ifaceTypeDef.Name);
				var ns = Reader.GetString (ifaceTypeDef.Namespace);
				if (name == "IJniNameProviderAttribute" && ns == "Java.Interop") {
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Sets the merged set of JNI name provider attributes from all loaded assemblies
	/// and re-classifies any attributes that weren't recognized in the initial pass.
	/// </summary>
	public void ReclassifyAttributes (HashSet<string> mergedJniNameProviders)
	{
		allJniNameProviderAttributes = mergedJniNameProviders;

		foreach (var typeHandle in Reader.TypeDefinitions) {
			var typeDef = Reader.GetTypeDefinition (typeHandle);

			// Skip types that already have component attribute info
			if (AttributesByType.TryGetValue (typeHandle, out var existing) && existing.HasComponentAttribute) {
				continue;
			}

			// Re-check custom attributes with the full set of known providers
			foreach (var caHandle in typeDef.GetCustomAttributes ()) {
				var ca = Reader.GetCustomAttribute (caHandle);
				var attrName = GetCustomAttributeName (ca, Reader);

				if (attrName == null || attrName == "RegisterAttribute" || attrName == "ExportAttribute") {
					continue;
				}

				if (mergedJniNameProviders.Contains (attrName) && !IsKnownComponentAttribute (attrName)) {
					var componentName = TryGetNameProperty (ca);
					if (componentName != null) {
						var attrInfo = existing ?? new TypeAttributeInfo ();
						attrInfo.HasComponentAttribute = true;
						attrInfo.ComponentAttributeJniName = componentName.Replace ('.', '/');
						AttributesByType [typeHandle] = attrInfo;
					}
				}
			}
		}
	}

	internal static string GetFullName (TypeDefinition typeDef, MetadataReader reader)
	{
		var name = reader.GetString (typeDef.Name);
		var ns = reader.GetString (typeDef.Namespace);

		if (typeDef.IsNested) {
			var declaringType = reader.GetTypeDefinition (typeDef.GetDeclaringType ());
			var parentName = GetFullName (declaringType, reader);
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
			var attrName = GetCustomAttributeName (ca, Reader);

			if (attrName == null) {
				continue;
			}

			if (attrName == "RegisterAttribute") {
				registerInfo = ParseRegisterAttribute (ca, customAttributeTypeProvider);
			} else if (attrName == "ExportAttribute") {
				// [Export] methods are detected per-method in CollectMarshalMethods
			} else if (IsJniNameProviderAttribute (attrName)) {
				attrInfo ??= new TypeAttributeInfo ();
				attrInfo.HasComponentAttribute = true;
				var componentName = TryGetNameProperty (ca);
				if (componentName != null) {
					attrInfo.ComponentAttributeJniName = componentName.Replace ('.', '/');
				}
				if (attrName == "ApplicationAttribute") {
					attrInfo.ApplicationBackupAgent = TryGetTypeProperty (ca, "BackupAgent");
					attrInfo.ApplicationManageSpaceActivity = TryGetTypeProperty (ca, "ManageSpaceActivity");
				}
			}
		}

		return (registerInfo, attrInfo);
	}

	/// <summary>
	/// Checks if an attribute type name is a known <c>IJniNameProviderAttribute</c> implementor.
	/// Uses the local set first (from this assembly), then falls back to the merged set
	/// (populated after all assemblies are loaded), then falls back to hardcoded names
	/// for the well-known Android component attributes.
	/// </summary>
	bool IsJniNameProviderAttribute (string attrName)
	{
		if (JniNameProviderAttributes.Contains (attrName)) {
			return true;
		}

		if (allJniNameProviderAttributes != null && allJniNameProviderAttributes.Contains (attrName)) {
			return true;
		}

		// Fallback for the case where we haven't loaded the assembly defining the attribute yet.
		// This covers the common case where user assemblies reference Mono.Android attributes.
		return IsKnownComponentAttribute (attrName);
	}

	static bool IsKnownComponentAttribute (string attrName)
	{
		return attrName == "ActivityAttribute"
			|| attrName == "ServiceAttribute"
			|| attrName == "BroadcastReceiverAttribute"
			|| attrName == "ContentProviderAttribute"
			|| attrName == "ApplicationAttribute"
			|| attrName == "InstrumentationAttribute";
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

	internal static RegisterInfo ParseRegisterAttribute (CustomAttribute ca, ICustomAttributeTypeProvider<string> provider)
	{
		var value = ca.DecodeValue (provider);

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

		if (TryGetNamedBooleanArgument (value, "DoNotGenerateAcw", out var doNotGenerateAcwValue)) {
			doNotGenerateAcw = doNotGenerateAcwValue;
		}

		return new RegisterInfo (jniName, signature, connector, doNotGenerateAcw);
	}

	string? TryGetTypeProperty (CustomAttribute ca, string propertyName)
	{
		var value = ca.DecodeValue (customAttributeTypeProvider);
		var typeName = TryGetNamedStringArgument (value, propertyName);
		if (!string.IsNullOrEmpty (typeName)) {
			return typeName;
		}
		return null;
	}

	string? TryGetNameProperty (CustomAttribute ca)
	{
		var value = ca.DecodeValue (customAttributeTypeProvider);

		// Check named arguments first (e.g., [Activity(Name = "...")])
		var name = TryGetNamedStringArgument (value, "Name");
		if (!string.IsNullOrEmpty (name)) {
			return name;
		}

		// Fall back to first constructor argument (e.g., [CustomJniName("...")])
		if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is string ctorName && !string.IsNullOrEmpty (ctorName)) {
			return ctorName;
		}

		return null;
	}

	static bool TryGetNamedBooleanArgument (CustomAttributeValue<string> value, string argumentName, out bool argumentValue)
	{
		foreach (var named in value.NamedArguments) {
			if (named.Name == argumentName && named.Value is bool boolValue) {
				argumentValue = boolValue;
				return true;
			}
		}

		argumentValue = false;
		return false;
	}

	static string? TryGetNamedStringArgument (CustomAttributeValue<string> value, string argumentName)
	{
		foreach (var named in value.NamedArguments) {
			if (named.Name == argumentName && named.Value is string stringValue) {
				return stringValue;
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
	/// If the type has [Application(BackupAgent = typeof(X))],
	/// this is the full name of X.
	/// </summary>
	public string? ApplicationBackupAgent { get; set; }

	/// <summary>
	/// If the type has [Application(ManageSpaceActivity = typeof(X))],
	/// this is the full name of X.
	/// </summary>
	public string? ApplicationManageSpaceActivity { get; set; }
}
