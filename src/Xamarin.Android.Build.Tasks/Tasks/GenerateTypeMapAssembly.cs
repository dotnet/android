#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Generates a new assembly (_Microsoft.Android.TypeMaps.dll) containing TypeMap attributes
/// that map Java type names to .NET types. This task runs BEFORE ILLink so that the trimmer
/// can use these attributes to make trimming decisions.
/// 
/// The generated assembly contains:
/// - TypeMapAttribute&lt;Java.Lang.Object&gt; entries for Java-to-.NET type mappings
/// - TypeMapAssociationAttribute&lt;InvokerUniverse&gt; entries for interface-to-invoker mappings
/// - TypeMapAssemblyTargetAttribute entries to tell runtime which assemblies to scan
/// 
/// Uses System.Reflection.Metadata for reading assemblies and Mono.Cecil for generating the output.
/// </summary>
public class GenerateTypeMapAssembly : AndroidTask
{
	public override string TaskPrefix => "GTMA";

	/// <summary>
	/// All resolved assemblies to scan for Java peer types.
	/// These are the assemblies that will be passed to ILLink.
	/// </summary>
	[Required]
	public ITaskItem[] ResolvedAssemblies { get; set; } = [];

	/// <summary>
	/// Output directory for the generated assembly.
	/// </summary>
	[Required]
	public string OutputDirectory { get; set; } = "";

	/// <summary>
	/// Output directory for generated Java source files (.java).
	/// </summary>
	[Required]
	public string JavaSourceOutputDirectory { get; set; } = "";

	/// <summary>
	/// Output directory for generated LLVM IR files (.ll).
	/// </summary>
	[Required]
	public string LlvmIrOutputDirectory { get; set; } = "";

	/// <summary>
	/// The generated TypeMap assembly path.
	/// </summary>
	[Output]
	public ITaskItem? GeneratedAssembly { get; set; }

	/// <summary>
	/// The name of the generated assembly (without extension) for TypeMapEntryAssembly property.
	/// </summary>
	[Output]
	public string TypeMapEntryAssemblyName { get; set; } = "";

	/// <summary>
	/// Updated list of assemblies including the generated TypeMap assembly.
	/// This should be passed to ILLink.
	/// </summary>
	[Output]
	public ITaskItem[] UpdatedResolvedAssemblies { get; set; } = [];

	public override bool RunTask ()
	{
		try {
			Log.LogDebugMessage ($"Scanning {ResolvedAssemblies.Length} assemblies for Java peer types...");

			// Scan assemblies for Java peer types
			var scanner = new JavaPeerScanner (Log);
			var javaPeers = scanner.ScanAssemblies (ResolvedAssemblies);

			if (javaPeers.Count == 0) {
				Log.LogDebugMessage ("No Java peer types found. Skipping TypeMap assembly generation.");
				UpdatedResolvedAssemblies = ResolvedAssemblies;
				return true;
			}

			Log.LogDebugMessage ($"Found {javaPeers.Count} Java peer types");

			// Generate the assembly
			Directory.CreateDirectory (OutputDirectory);
			string assemblyPath = Path.Combine (OutputDirectory, "_Microsoft.Android.TypeMaps.dll");

			var generator = new TypeMapAssemblyGenerator (Log);
			generator.Generate (assemblyPath, javaPeers, JavaSourceOutputDirectory, LlvmIrOutputDirectory);

			GeneratedAssembly = new TaskItem (assemblyPath);
			TypeMapEntryAssemblyName = "_Microsoft.Android.TypeMaps";

			// Add generated assembly to the list of assemblies for ILLink
			var updatedList = new List<ITaskItem> (ResolvedAssemblies);
			var generatedItem = new TaskItem (assemblyPath);
			// Copy metadata from a reference assembly (e.g., Mono.Android) to ensure proper handling
			var referenceAssembly = ResolvedAssemblies.FirstOrDefault (a => 
				Path.GetFileNameWithoutExtension (a.ItemSpec) == "Mono.Android");
			if (referenceAssembly != null) {
				generatedItem.SetMetadata ("RuntimeIdentifier", referenceAssembly.GetMetadata ("RuntimeIdentifier"));
				generatedItem.SetMetadata ("DestinationSubDirectory", referenceAssembly.GetMetadata ("DestinationSubDirectory"));
			}
			// Set required metadata for proper processing through ILLink, R2R, and packaging
			string assemblyName = "_Microsoft.Android.TypeMaps.dll";
			generatedItem.SetMetadata ("RelativePath", assemblyName);
			generatedItem.SetMetadata ("DestinationSubPath", assemblyName);
			generatedItem.SetMetadata ("CopyToPublishDirectory", "PreserveNewest");
			// Mark as needing postprocessing by ILLink
			generatedItem.SetMetadata ("PostprocessAssembly", "true");
			updatedList.Add (generatedItem);
			UpdatedResolvedAssemblies = updatedList.ToArray ();

			Log.LogDebugMessage ($"Generated TypeMap assembly: {assemblyPath}");
			Log.LogDebugMessage ($"TypeMapEntryAssemblyName: {TypeMapEntryAssemblyName}");

			return !Log.HasLoggedErrors;
		} catch (Exception ex) {
			Log.LogErrorFromException (ex, showStackTrace: true);
			return false;
		}
	}
}

/// <summary>
/// Represents a Java peer type found during assembly scanning.
/// </summary>
internal class JavaPeerInfo
{
	public string JavaName { get; set; } = "";
	public string ManagedTypeName { get; set; } = "";
	public string AssemblyName { get; set; } = "";
	public string AssemblyPath { get; set; } = "";
	public bool IsInterface { get; set; }
	public bool IsAbstract { get; set; }
	public string? InvokerTypeName { get; set; }
	public string? InvokerAssemblyName { get; set; }

	/// <summary>
	/// The managed type name of the base class (if any).
	/// </summary>
	public string? BaseManagedTypeName { get; set; }

	/// <summary>
	/// True if the type has an activation constructor (IntPtr, JniHandleOwnership).
	/// </summary>
	public bool HasActivationConstructor { get; set; }

	/// <summary>
	/// Marshal methods that can be called from native code via GetFunctionPointer.
	/// </summary>
	public List<MarshalMethodInfo> MarshalMethods { get; set; } = new ();
}

/// <summary>
/// Information about a marshal method that can be called from native code.
/// </summary>
internal class MarshalMethodInfo
{
	public string JniName { get; set; } = "";
	public string JniSignature { get; set; } = "";
	public string NativeCallbackName { get; set; } = "";
	public string[] ParameterTypeNames { get; set; } = [];
	public string? ReturnTypeName { get; set; }
	public MethodDefinitionHandle UcoWrapper { get; set; }
}

/// <summary>
/// Simple signature type provider that returns type names as strings.
/// Used with MetadataReader.DecodeSignature to decode method signatures.
/// </summary>
internal class SignatureTypeProvider : ISignatureTypeProvider<string, object?>
{
	readonly MetadataReader _reader;

	public SignatureTypeProvider (MetadataReader reader)
	{
		_reader = reader;
	}

	public string GetPrimitiveType (PrimitiveTypeCode typeCode) => typeCode switch {
		PrimitiveTypeCode.Void => "System.Void",
		PrimitiveTypeCode.Boolean => "System.Boolean",
		PrimitiveTypeCode.Char => "System.Char",
		PrimitiveTypeCode.SByte => "System.SByte",
		PrimitiveTypeCode.Byte => "System.Byte",
		PrimitiveTypeCode.Int16 => "System.Int16",
		PrimitiveTypeCode.UInt16 => "System.UInt16",
		PrimitiveTypeCode.Int32 => "System.Int32",
		PrimitiveTypeCode.UInt32 => "System.UInt32",
		PrimitiveTypeCode.Int64 => "System.Int64",
		PrimitiveTypeCode.UInt64 => "System.UInt64",
		PrimitiveTypeCode.Single => "System.Single",
		PrimitiveTypeCode.Double => "System.Double",
		PrimitiveTypeCode.String => "System.String",
		PrimitiveTypeCode.IntPtr => "System.IntPtr",
		PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
		PrimitiveTypeCode.Object => "System.Object",
		PrimitiveTypeCode.TypedReference => "System.TypedReference",
		_ => $"Unknown({typeCode})"
	};

	public string GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
	{
		var typeDef = reader.GetTypeDefinition (handle);
		string ns = reader.GetString (typeDef.Namespace);
		string name = reader.GetString (typeDef.Name);
		return string.IsNullOrEmpty (ns) ? name : $"{ns}.{name}";
	}

	public string GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
	{
		var typeRef = reader.GetTypeReference (handle);
		string ns = reader.GetString (typeRef.Namespace);
		string name = reader.GetString (typeRef.Name);
		return string.IsNullOrEmpty (ns) ? name : $"{ns}.{name}";
	}

	public string GetTypeFromSpecification (MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
		=> "TypeSpec";

	public string GetSZArrayType (string elementType) => $"{elementType}[]";
	public string GetArrayType (string elementType, ArrayShape shape) => $"{elementType}[{new string (',', shape.Rank - 1)}]";
	public string GetByReferenceType (string elementType) => $"{elementType}&";
	public string GetPointerType (string elementType) => $"{elementType}*";
	public string GetGenericInstantiation (string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments)
		=> $"{genericType}<{string.Join (", ", typeArguments)}>";
	public string GetGenericMethodParameter (object? genericContext, int index) => $"!!{index}";
	public string GetGenericTypeParameter (object? genericContext, int index) => $"!{index}";
	public string GetModifiedType (string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
	public string GetPinnedType (string elementType) => elementType;
	public string GetFunctionPointerType (MethodSignature<string> signature) => "fnptr";
}

/// <summary>
/// Scans assemblies for types with [Register] attribute that represent Java peers.
/// Uses System.Reflection.Metadata for efficient, low-allocation scanning.
/// </summary>
internal class JavaPeerScanner
{
	readonly TaskLoggingHelper _log;

	// Well-known attribute type names
	const string RegisterAttributeFullName = "Android.Runtime.RegisterAttribute";

	public JavaPeerScanner (TaskLoggingHelper log)
	{
		_log = log;
	}

	public List<JavaPeerInfo> ScanAssemblies (ITaskItem[] assemblies)
	{
		var results = new List<JavaPeerInfo> ();

		foreach (var assembly in assemblies) {
			string path = assembly.ItemSpec;
			if (!File.Exists (path))
				continue;

			// Skip non-.NET assemblies
			if (!path.EndsWith (".dll", StringComparison.OrdinalIgnoreCase))
				continue;

			try {
				ScanAssembly (path, results);
			} catch (Exception ex) {
				_log.LogDebugMessage ($"Failed to scan assembly {path}: {ex.Message}");
			}
		}

		return results;
	}

	void ScanAssembly (string assemblyPath, List<JavaPeerInfo> results)
	{
		using var stream = File.OpenRead (assemblyPath);
		using var peReader = new PEReader (stream);

		if (!peReader.HasMetadata)
			return;

		var metadataReader = peReader.GetMetadataReader ();
		string assemblyName = GetAssemblyName (metadataReader);

		// Check if this assembly references Mono.Android (has Java peer types)
		bool referencesMonoAndroid = ReferencesMonoAndroid (metadataReader);
		bool isMonoAndroid = assemblyName == "Mono.Android";
		_log.LogDebugMessage ($"  Checking assembly {assemblyName}: ReferencesMonoAndroid={referencesMonoAndroid}, IsMonoAndroid={isMonoAndroid}");

		if (!referencesMonoAndroid && !isMonoAndroid)
			return;

		_log.LogDebugMessage ($"  Scanning assembly {assemblyName} for Java peer types...");

		foreach (var typeDefHandle in metadataReader.TypeDefinitions) {
			var typeDef = metadataReader.GetTypeDefinition (typeDefHandle);
			
			// Skip nested types for now (they'll be handled via their parent)
			if (typeDef.IsNested)
				continue;

			ProcessType (metadataReader, typeDef, assemblyName, assemblyPath, results);
		}
	}

	void ProcessType (MetadataReader reader, TypeDefinition typeDef, string assemblyName, string assemblyPath, List<JavaPeerInfo> results)
	{
		// Look for [Register] attribute
		string? javaName = GetRegisterAttributeValue (reader, typeDef);
		if (javaName == null)
			return;

		string ns = reader.GetString (typeDef.Namespace);
		string name = reader.GetString (typeDef.Name);
		string fullName = string.IsNullOrEmpty (ns) ? name : $"{ns}.{name}";
		
		_log.LogDebugMessage ($"    Found Java peer: {fullName} -> {javaName} (in {assemblyName})");

		bool isInterface = (typeDef.Attributes & System.Reflection.TypeAttributes.Interface) != 0;
		bool isAbstract = (typeDef.Attributes & System.Reflection.TypeAttributes.Abstract) != 0;

		string? baseTypeName = null;
		if (!isInterface && !typeDef.BaseType.IsNil) {
			baseTypeName = GetFullTypeName (reader, typeDef.BaseType);
		}

		// For interfaces and abstract types, try to find the Invoker type
		string? invokerTypeName = null;
		string? invokerAssemblyName = null;

		if (isInterface || isAbstract) {
			// Convention: Invoker type is named {TypeName}Invoker in the same assembly
			string expectedInvokerName = fullName + "Invoker";
			if (TypeExistsInAssembly (reader, expectedInvokerName)) {
				invokerTypeName = expectedInvokerName;
				invokerAssemblyName = assemblyName;
			}
		}

		// Check for activation constructor: (IntPtr, JniHandleOwnership)
		bool hasActivationCtor = HasActivationConstructor (reader, typeDef);

		// Collect marshal methods
		var marshalMethods = CollectMarshalMethods (reader, typeDef);

		results.Add (new JavaPeerInfo {
			JavaName = javaName,
			ManagedTypeName = fullName,
			AssemblyName = assemblyName,
			AssemblyPath = assemblyPath,
			IsInterface = isInterface,
			IsAbstract = isAbstract,
			InvokerTypeName = invokerTypeName,
			InvokerAssemblyName = invokerAssemblyName,
			BaseManagedTypeName = baseTypeName,
			HasActivationConstructor = hasActivationCtor,
			MarshalMethods = marshalMethods,
		});

		// Process nested types
		foreach (var nestedHandle in typeDef.GetNestedTypes ()) {
			var nestedDef = reader.GetTypeDefinition (nestedHandle);
			ProcessNestedType (reader, nestedDef, fullName, assemblyName, assemblyPath, results);
		}
	}

	void ProcessNestedType (MetadataReader reader, TypeDefinition typeDef, string parentTypeName, string assemblyName, string assemblyPath, List<JavaPeerInfo> results)
	{
		string? javaName = GetRegisterAttributeValue (reader, typeDef);
		if (javaName == null)
			return;

		string name = reader.GetString (typeDef.Name);
		string fullName = $"{parentTypeName}+{name}";

		bool isInterface = (typeDef.Attributes & System.Reflection.TypeAttributes.Interface) != 0;
		bool isAbstract = (typeDef.Attributes & System.Reflection.TypeAttributes.Abstract) != 0;

		string? baseTypeName = null;
		if (!isInterface && !typeDef.BaseType.IsNil) {
			baseTypeName = GetFullTypeName (reader, typeDef.BaseType);
		}

		string? invokerTypeName = null;
		string? invokerAssemblyName = null;

		if (isInterface || isAbstract) {
			string expectedInvokerName = fullName + "Invoker";
			if (TypeExistsInAssembly (reader, expectedInvokerName)) {
				invokerTypeName = expectedInvokerName;
				invokerAssemblyName = assemblyName;
			}
		}

		// Check for activation constructor
		bool hasActivationCtor = HasActivationConstructor (reader, typeDef);

		// Collect marshal methods
		var marshalMethods = CollectMarshalMethods (reader, typeDef);

		results.Add (new JavaPeerInfo {
			JavaName = javaName,
			ManagedTypeName = fullName,
			AssemblyName = assemblyName,
			AssemblyPath = assemblyPath,
			IsInterface = isInterface,
			IsAbstract = isAbstract,
			InvokerTypeName = invokerTypeName,
			InvokerAssemblyName = invokerAssemblyName,
			BaseManagedTypeName = baseTypeName,
			HasActivationConstructor = hasActivationCtor,
			MarshalMethods = marshalMethods,
		});

		// Recursively process nested types
		foreach (var nestedHandle in typeDef.GetNestedTypes ()) {
			var nestedDef = reader.GetTypeDefinition (nestedHandle);
			ProcessNestedType (reader, nestedDef, fullName, assemblyName, assemblyPath, results);
		}
	}

	string? GetRegisterAttributeValue (MetadataReader reader, TypeDefinition typeDef)
	{
		foreach (var attrHandle in typeDef.GetCustomAttributes ()) {
			var attr = reader.GetCustomAttribute (attrHandle);
			
			// Check for [Register("...")] attribute
			if (IsRegisterAttribute (reader, attr)) {
				return DecodeRegisterAttributeValue (reader, attr);
			}
			
			// Check for Android component attributes with Name property:
			// [Activity(Name = "...")], [Service(Name = "...")], 
			// [BroadcastReceiver(Name = "...")], [ContentProvider(Name = "...")]
			if (IsAndroidComponentAttribute (reader, attr, out string? componentJavaName)) {
				if (!string.IsNullOrEmpty (componentJavaName)) {
					// Convert dots to slashes for the Java name format
					return componentJavaName!.Replace ('.', '/');
				}
			}
		}
		return null;
	}

	bool IsAndroidComponentAttribute (MetadataReader reader, CustomAttribute attr, out string? javaName)
	{
		javaName = null;
		string? attrTypeName = GetAttributeTypeName (reader, attr);
		if (attrTypeName == null)
			return false;

		// Check if it's one of the Android component attributes
		bool isComponentAttr = attrTypeName == "Android.App.ActivityAttribute" ||
		                       attrTypeName == "Android.App.ServiceAttribute" ||
		                       attrTypeName == "Android.Content.BroadcastReceiverAttribute" ||
		                       attrTypeName == "Android.Content.ContentProviderAttribute" ||
		                       attrTypeName == "Android.App.ApplicationAttribute" ||
		                       attrTypeName == "Android.App.InstrumentationAttribute";

		if (!isComponentAttr)
			return false;

		// Extract the Name property from the attribute
		javaName = GetNamedPropertyValue (reader, attr, "Name");
		return true;
	}

	string? GetAttributeTypeName (MetadataReader reader, CustomAttribute attr)
	{
		if (attr.Constructor.Kind == HandleKind.MemberReference) {
			var memberRef = reader.GetMemberReference ((MemberReferenceHandle)attr.Constructor);
			if (memberRef.Parent.Kind == HandleKind.TypeReference) {
				var typeRef = reader.GetTypeReference ((TypeReferenceHandle)memberRef.Parent);
				string ns = reader.GetString (typeRef.Namespace);
				string name = reader.GetString (typeRef.Name);
				return $"{ns}.{name}";
			}
		} else if (attr.Constructor.Kind == HandleKind.MethodDefinition) {
			var methodDef = reader.GetMethodDefinition ((MethodDefinitionHandle)attr.Constructor);
			var declaringType = reader.GetTypeDefinition (methodDef.GetDeclaringType ());
			string ns = reader.GetString (declaringType.Namespace);
			string name = reader.GetString (declaringType.Name);
			return $"{ns}.{name}";
		}
		return null;
	}

	string? GetNamedPropertyValue (MetadataReader reader, CustomAttribute attr, string propertyName)
	{
		// Decode the attribute blob
		var valueBlob = reader.GetBlobReader (attr.Value);

		// Skip prolog (2 bytes: 0x0001)
		if (valueBlob.Length < 2)
			return null;
		valueBlob.ReadUInt16 ();

		// Skip constructor arguments - for component attributes, we need to count the ctor parameters
		// Most component attributes have no required ctor params, but we need to handle the blob format
		// After ctor args, there's a NumNamed count (2 bytes), then named args
		
		// For now, find where the named parameters start by searching for the "Name" property
		// The format is: [prolog][ctor args...][numNamed:2bytes][named args...]
		// Named arg format: [kind:1byte FIELD=0x53 or PROPERTY=0x54][type:1byte][name:string][value:encoded]
		
		// Skip to where named parameters should start - this is tricky without knowing the ctor
		// signature. For ActivityAttribute etc., the default ctor has no params.
		
		// Read number of named arguments
		if (valueBlob.RemainingBytes < 2)
			return null;
		int numNamed = valueBlob.ReadUInt16 ();

		for (int i = 0; i < numNamed; i++) {
			if (valueBlob.RemainingBytes < 3)
				return null;

			byte kind = valueBlob.ReadByte (); // 0x53 = FIELD, 0x54 = PROPERTY
			byte type = valueBlob.ReadByte (); // Element type

			// Read property/field name
			string? name = ReadSerializedString (ref valueBlob);
			if (name == null)
				return null;

			// Read value based on type
			if (type == 0x0E) { // ELEMENT_TYPE_STRING
				string? value = ReadSerializedString (ref valueBlob);
				if (name == propertyName) {
					return value;
				}
			} else if (type == 0x02) { // ELEMENT_TYPE_BOOLEAN
				if (valueBlob.RemainingBytes < 1)
					return null;
				valueBlob.ReadByte ();
			} else if (type == 0x08 || type == 0x09) { // ELEMENT_TYPE_I4 or ELEMENT_TYPE_U4
				if (valueBlob.RemainingBytes < 4)
					return null;
				valueBlob.ReadInt32 ();
			} else {
				// Unknown type, can't continue parsing reliably
				break;
			}
		}

		return null;
	}

	bool IsRegisterAttribute (MetadataReader reader, CustomAttribute attr)
	{
		if (attr.Constructor.Kind == HandleKind.MemberReference) {
			var memberRef = reader.GetMemberReference ((MemberReferenceHandle)attr.Constructor);
			if (memberRef.Parent.Kind == HandleKind.TypeReference) {
				var typeRef = reader.GetTypeReference ((TypeReferenceHandle)memberRef.Parent);
				string ns = reader.GetString (typeRef.Namespace);
				string name = reader.GetString (typeRef.Name);
				return ns == "Android.Runtime" && name == "RegisterAttribute";
			}
		} else if (attr.Constructor.Kind == HandleKind.MethodDefinition) {
			var methodDef = reader.GetMethodDefinition ((MethodDefinitionHandle)attr.Constructor);
			var declaringType = reader.GetTypeDefinition (methodDef.GetDeclaringType ());
			string ns = reader.GetString (declaringType.Namespace);
			string name = reader.GetString (declaringType.Name);
			return ns == "Android.Runtime" && name == "RegisterAttribute";
		}
		return false;
	}

	string? DecodeRegisterAttributeValue (MetadataReader reader, CustomAttribute attr)
	{
		// The first constructor argument is the Java type name
		var valueBlob = reader.GetBlobReader (attr.Value);
		
		// Skip prolog (2 bytes: 0x0001)
		if (valueBlob.Length < 2)
			return null;
		valueBlob.ReadUInt16 ();

		// Read the first string argument (Java name)
		return ReadSerializedString (ref valueBlob);
	}

	static string? ReadSerializedString (ref BlobReader reader)
	{
		// Check for null string (0xFF)
		if (reader.RemainingBytes == 0)
			return null;

		byte firstByte = reader.ReadByte ();
		if (firstByte == 0xFF)
			return null;

		// Decode compressed length
		int length;
		if ((firstByte & 0x80) == 0) {
			length = firstByte;
		} else if ((firstByte & 0xC0) == 0x80) {
			if (reader.RemainingBytes < 1)
				return null;
			byte secondByte = reader.ReadByte ();
			length = ((firstByte & 0x3F) << 8) | secondByte;
		} else {
			if (reader.RemainingBytes < 3)
				return null;
			byte b1 = reader.ReadByte ();
			byte b2 = reader.ReadByte ();
			byte b3 = reader.ReadByte ();
			length = ((firstByte & 0x1F) << 24) | (b1 << 16) | (b2 << 8) | b3;
		}

		if (length == 0)
			return string.Empty;

		if (reader.RemainingBytes < length)
			return null;

		return reader.ReadUTF8 (length);
	}

	bool TypeExistsInAssembly (MetadataReader reader, string typeName)
	{
		// Simple check - just look for the type by name
		foreach (var typeDefHandle in reader.TypeDefinitions) {
			var typeDef = reader.GetTypeDefinition (typeDefHandle);
			string ns = reader.GetString (typeDef.Namespace);
			string name = reader.GetString (typeDef.Name);
			string fullName = string.IsNullOrEmpty (ns) ? name : $"{ns}.{name}";
			if (fullName == typeName)
				return true;
		}
		return false;
	}

	static string GetAssemblyName (MetadataReader reader)
	{
		var asmDef = reader.GetAssemblyDefinition ();
		return reader.GetString (asmDef.Name);
	}

	bool ReferencesMonoAndroid (MetadataReader reader)
	{
		foreach (var asmRefHandle in reader.AssemblyReferences) {
			var asmRef = reader.GetAssemblyReference (asmRefHandle);
			string name = reader.GetString (asmRef.Name);
			if (name == "Mono.Android")
				return true;
		}
		return false;
	}

	/// <summary>
	/// Checks if the type has an activation constructor: (IntPtr, JniHandleOwnership)
	/// </summary>
	bool HasActivationConstructor (MetadataReader reader, TypeDefinition typeDef)
	{
		foreach (var methodHandle in typeDef.GetMethods ()) {
			var method = reader.GetMethodDefinition (methodHandle);
			string methodName = reader.GetString (method.Name);

			if (methodName != ".ctor")
				continue;

			// Check parameters: (IntPtr, JniHandleOwnership)
			var signature = method.DecodeSignature (new SignatureTypeProvider (reader), genericContext: null);
			if (signature.ParameterTypes.Length != 2)
				continue;

			var p0 = signature.ParameterTypes[0];
			var p1 = signature.ParameterTypes[1];

			if (p0 == "System.IntPtr" && p1 == "Android.Runtime.JniHandleOwnership") {
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Collects marshal methods from a type that have [Register] attributes with connector methods.
	/// </summary>
	List<MarshalMethodInfo> CollectMarshalMethods (MetadataReader reader, TypeDefinition typeDef)
	{
		var methods = new List<MarshalMethodInfo> ();

		foreach (var methodHandle in typeDef.GetMethods ()) {
			var method = reader.GetMethodDefinition (methodHandle);
			
			// Look for [Register] attribute on the method
			var registerInfo = GetMethodRegisterAttribute (reader, method);
			if (registerInfo == null)
				continue;

			var (jniName, jniSignature, connector) = registerInfo.Value;
			if (string.IsNullOrEmpty (jniName) || string.IsNullOrEmpty (jniSignature) || string.IsNullOrEmpty (connector))
				continue;

			// Find the native callback method name
			string? nativeCallbackName = GetNativeCallbackName (connector, jniName);
			if (nativeCallbackName == null)
				continue;

			// Get parameter types
			var signature = method.DecodeSignature (new SignatureTypeProvider (reader), genericContext: null);
			var paramTypes = signature.ParameterTypes.ToArray ();
			string? returnType = signature.ReturnType;

			methods.Add (new MarshalMethodInfo {
				JniName = jniName,
				JniSignature = jniSignature,
				NativeCallbackName = nativeCallbackName,
				ParameterTypeNames = paramTypes,
				ReturnTypeName = returnType,
			});
		}

		return methods;
	}

	/// <summary>
	/// Gets the [Register] attribute info from a method.
	/// Returns (jniName, jniSignature, connector) or null if not found.
	/// </summary>
	(string, string, string)? GetMethodRegisterAttribute (MetadataReader reader, MethodDefinition method)
	{
		foreach (var attrHandle in method.GetCustomAttributes ()) {
			var attr = reader.GetCustomAttribute (attrHandle);
			if (!IsRegisterAttribute (reader, attr))
				continue;

			// Decode the attribute arguments
			var valueBlob = reader.GetBlobReader (attr.Value);
			if (valueBlob.Length < 2)
				return null;

			valueBlob.ReadUInt16 (); // Skip prolog

			string? jniName = ReadSerializedString (ref valueBlob);
			string? jniSignature = ReadSerializedString (ref valueBlob);
			string? connector = ReadSerializedString (ref valueBlob);

			if (jniName != null && jniSignature != null && connector != null) {
				return (jniName, jniSignature, connector);
			}
		}
		return null;
	}

	/// <summary>
	/// Derives the native callback method name from the connector string.
	/// </summary>
	string? GetNativeCallbackName (string connector, string jniName)
	{
		// Connector format is typically: "GetOnClickHandler:Android.Views.View+IOnClickListener, Mono.Android"
		// The native callback is typically n_{MethodName}
		// For simplicity, we use n_{jniName} as the callback name
		return $"n_{jniName}";
	}

	string? GetFullTypeName (MetadataReader reader, EntityHandle handle)
	{
		try {
			if (handle.Kind == HandleKind.TypeReference) {
				var typeRef = reader.GetTypeReference ((TypeReferenceHandle)handle);
				string ns = reader.GetString (typeRef.Namespace);
				string name = reader.GetString (typeRef.Name);
				return string.IsNullOrEmpty (ns) ? name : $"{ns}.{name}";
			} else if (handle.Kind == HandleKind.TypeDefinition) {
				var typeDef = reader.GetTypeDefinition ((TypeDefinitionHandle)handle);
				string ns = reader.GetString (typeDef.Namespace);
				string name = reader.GetString (typeDef.Name);
				return string.IsNullOrEmpty (ns) ? name : $"{ns}.{name}";
			}
		} catch {
			// Ignore errors, return null
		}
		return null;
	}
}

/// <summary>
/// Generates the TypeMap assembly using System.Reflection.Metadata.Ecma335.
/// No Mono.Cecil dependency - pure S.R.M.E for assembly generation.
/// 
/// Per the Type Mapping API v2 spec, we generate:
/// 1. A proxy type for each Java peer (e.g., MainActivity_Proxy)
/// 2. The proxy applies itself as an attribute to itself (self-application pattern)
/// 3. TypeMapAttribute entries pointing to proxy types
/// </summary>
internal class TypeMapAssemblyGenerator
{
	readonly TaskLoggingHelper _log;
	readonly MetadataBuilder _metadata;
	readonly BlobBuilder _ilStream;
	readonly MethodBodyStreamEncoder _methodBodyStream;
	
	// Assembly references
	AssemblyReferenceHandle _corlibRef;
	AssemblyReferenceHandle _interopRef;
	AssemblyReferenceHandle _monoAndroidRef;
	
	// Well-known type references
	TypeReferenceHandle _objectTypeRef;
	TypeReferenceHandle _voidTypeRef;
	TypeReferenceHandle _intPtrTypeRef;
	TypeReferenceHandle _int32TypeRef;
	TypeReferenceHandle _typeTypeRef;
	TypeReferenceHandle _attributeUsageTypeRef;
	TypeReferenceHandle _attributeTargetsTypeRef;
	TypeReferenceHandle _javaPeerProxyTypeRef;
	TypeReferenceHandle _iJavaPeerableTypeRef;
	TypeReferenceHandle _jniHandleOwnershipTypeRef;
	TypeReferenceHandle _typeMapAttrTypeRef;
	TypeReferenceHandle _typeMapAssocAttrTypeRef;
	TypeReferenceHandle _typeMapAsmTargetAttrTypeRef;
	TypeReferenceHandle _javaLangObjectTypeRef;
	TypeReferenceHandle _invokerUniverseTypeRef;
	TypeReferenceHandle _aliasesUniverseTypeRef;
	
	// UCO related types
	TypeReferenceHandle _unmanagedCallersOnlyAttrTypeRef;
	TypeReferenceHandle _exceptionTypeRef;
	TypeReferenceHandle _throwableTypeRef;
	TypeReferenceHandle _androidEnvironmentTypeRef;
	TypeReferenceHandle _androidRuntimeInternalTypeRef;
	TypeReferenceHandle _notSupportedExceptionTypeRef;

	// Well-known member references
	MemberReferenceHandle _javaPeerProxyCtorRef;
	MemberReferenceHandle _intPtrZeroFieldRef;
	MemberReferenceHandle _invokerTypeMapAssocAttrCtorRef;
	MemberReferenceHandle _aliasesTypeMapAssocAttrCtorRef;
	MemberReferenceHandle _typeMapAttrCtorRef;

	// UCO related member references
	MemberReferenceHandle _unmanagedCallersOnlyCtorRef;
	MemberReferenceHandle _waitForBridgeProcessingRef;
	MemberReferenceHandle _raiseThrowableRef;
	MemberReferenceHandle _throwableFromExceptionRef;
	MemberReferenceHandle _notSupportedExceptionCtorRef;
	
	// Signature blobs (cached)
	BlobHandle _voidMethodSig;
	BlobHandle _getFunctionPointerSig;
	BlobHandle _createInstanceSig;
	
	// Tracking for type/method definition order
	int _nextFieldDefRowId = 1;
	int _nextMethodDefRowId = 1;
	int _nextParamDefRowId = 1;
	
	// Generated proxy type definitions (for self-application)
	readonly List<(TypeDefinitionHandle TypeDef, BlobHandle CtorSig)> _proxyTypes = new ();
	
	public TypeMapAssemblyGenerator (TaskLoggingHelper log)
	{
	_log = log;
	_metadata = new MetadataBuilder ();
	_ilStream = new BlobBuilder ();
	_methodBodyStream = new MethodBodyStreamEncoder (_ilStream);
	}
	
	public void Generate (string outputPath, List<JavaPeerInfo> javaPeers, string javaSourceDir, string llvmIrDir)
	{
		// 1. Create module and assembly definitions
	CreateModuleAndAssembly ();
	
	// 2. Add assembly references
	AddAssemblyReferences ();
	
	// 3. Add type references for well-known types
	AddTypeReferences ();
	
	// 4. Add member references
	AddMemberReferences ();
	
	// 5. Create signature blobs
	CreateSignatureBlobs ();
	
	// 6. Add TypeMapAssemblyTargetAttribute to assembly
	AddAssemblyAttribute ();
	
	// 7. Generate proxy types and collect TypeMap attributes
	var typeMapAttrs = new List<(string jniName, string proxyTypeName, string targetTypeName)> ();
	var invokerMappings = new List<(string source, string invoker)> ();
	var aliasMappings = new List<(string source, string aliasHolder)> ();
	
	// Group peers by JavaName to handle aliases
	var peersByJavaName = javaPeers.GroupBy (p => p.JavaName).ToList ();
	
	foreach (var group in peersByJavaName) {
		var peers = group.ToList ();
		string jniName = group.Key;
		string? aliasHolderName = null;

		// Handle aliases holder if multiple
		if (peers.Count > 1) {
			// Generate Alias holder type using first peer's managed name as base
			aliasHolderName = peers[0].ManagedTypeName.Replace ('.', '_').Replace ('+', '_') + "_Aliases";
			GenerateAliasHolderType (aliasHolderName);
			
			// [assembly: TypeMap<Java.Lang.Object>(JavaName, typeof(AliasHolder), typeof(AliasHolder))]
			typeMapAttrs.Add ((jniName, aliasHolderName, aliasHolderName));
		}

		for (int i = 0; i < peers.Count; i++) {
			var peer = peers[i];
			try {
				if (peer.IsInterface || peer.IsAbstract) {
					if (!string.IsNullOrEmpty (peer.InvokerTypeName)) {
						invokerMappings.Add ((
							$"{peer.ManagedTypeName}, {peer.AssemblyName}",
							$"{peer.InvokerTypeName}, {peer.InvokerAssemblyName}"
						));
					}
				}
				
				var targetTypeRef = AddExternalTypeReference (peer.AssemblyName, peer.ManagedTypeName);
				if (targetTypeRef.IsNil) {
					_log.LogDebugMessage ($"  Skipping {peer.ManagedTypeName}: could not create type reference");
					continue;
				}
				
				// Calculate proxy name based on MANAGED type to ensure uniqueness (especially for aliases)
				string proxyTypeName = peer.ManagedTypeName.Replace ('.', '_').Replace ('+', '_') + "_Proxy";
				
				var proxyTypeDef = GenerateProxyType (peer, targetTypeRef, proxyTypeName);
				if (proxyTypeDef.IsNil) {
					_log.LogDebugMessage ($"  Skipping {peer.ManagedTypeName}: could not generate proxy type");
					continue;
				}
				
				string entryJniName = peers.Count > 1 ? $"{jniName}[{i}]" : jniName;
				string targetTypeName = $"{peer.ManagedTypeName}, {peer.AssemblyName}";
				
				typeMapAttrs.Add ((entryJniName, proxyTypeName, targetTypeName));
				
				if (aliasHolderName != null) {
					aliasMappings.Add ((targetTypeName, aliasHolderName));
				}
				
			} catch (Exception ex) {
				_log.LogDebugMessage ($"  Error processing {peer.ManagedTypeName}: {ex.Message}");
			}
		}
	}
	
	// 8. Add TypeMapAttribute entries (assembly-level custom attributes)
	foreach (var (jniName, proxyType, targetType) in typeMapAttrs) {
		AddTypeMapAttribute (jniName, proxyType, targetType);
	}
	
	// 8b. Generate TypeMapAssociation attributes for Invokers
	foreach (var (source, invoker) in invokerMappings) {
		AddInvokerTypeMapAssociationAttribute (source, invoker);
	}

	// 8c. Generate TypeMapAssociation attributes for Aliases
	foreach (var (source, aliasHolder) in aliasMappings) {
		AddAliasTypeMapAssociationAttribute (source, aliasHolder);
	}

	// 9. Apply self-attribute to each proxy type
	ApplySelfAttributes ();
	
	// 10. Write the PE file
	WritePEFile (outputPath);

	// 11. Generate Java source files
	GenerateJavaSourceFiles (javaPeers, javaSourceDir);

	// 12. Generate LLVM IR files
	GenerateLlvmIrFiles (javaPeers, llvmIrDir);
	
	_log.LogDebugMessage ($"Generated TypeMap assembly with {typeMapAttrs.Count} proxy types");
	}

	void GenerateLlvmIrFiles (List<JavaPeerInfo> javaPeers, string llvmIrDir)
	{
		int count = 0;
		foreach (var peer in javaPeers) {
			// Skip framework assemblies (Mono.Android, Java.Interop) - they have their own generation
			if (IsFrameworkAssembly (peer.AssemblyName))
				continue;

			GenerateLlvmIrFile (llvmIrDir, peer);
			count++;
		}
		
		GenerateLlvmIrInitFile (llvmIrDir);
		
		_log.LogDebugMessage ($"Generated {count} LLVM IR files");
	}
	
	bool IsFrameworkAssembly (string assemblyName)
	{
		return assemblyName switch {
			"Mono.Android" => true,
			"Java.Interop" => true,
			"System.Private.CoreLib" => true,
			_ when assemblyName.StartsWith ("System.", StringComparison.Ordinal) => true,
			_ when assemblyName.StartsWith ("Microsoft.", StringComparison.Ordinal) => true,
			_ when assemblyName.StartsWith ("Xamarin.", StringComparison.Ordinal) => true,
			_ => false,
		};
	}

	void GenerateLlvmIrInitFile (string outputPath)
	{
		Directory.CreateDirectory (outputPath);
		string llFilePath = Path.Combine (outputPath, "marshal_methods_init.ll");

		using var writer = new StreamWriter (llFilePath);

		writer.Write ("""
; ModuleID = 'marshal_methods_init.ll'
source_filename = "marshal_methods_init.ll"
target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
target triple = "aarch64-unknown-linux-android21"

; Global typemap_get_function_pointer callback - initialized to null, set at runtime
@typemap_get_function_pointer = default local_unnamed_addr global ptr null, align 8

; External puts and abort for error handling
declare i32 @puts(ptr nocapture readonly) local_unnamed_addr
declare void @abort() noreturn

; Error message for null function pointer
@.str.error = private unnamed_addr constant [48 x i8] c"typemap_get_function_pointer MUST be specified\0A\00", align 1

""");
	}

	void GenerateLlvmIrFile (string outputPath, JavaPeerInfo peer)
	{
		Directory.CreateDirectory (outputPath);

		// Sanitize type name for filename
		string sanitizedName = peer.ManagedTypeName.Replace ('.', '_').Replace ('/', '_').Replace ('+', '_');
		string llFilePath = Path.Combine (outputPath, $"marshal_methods_{sanitizedName}.ll");

		using var writer = new StreamWriter (llFilePath);

		// Separate constructors from regular methods
		var constructors = peer.MarshalMethods.Where (m => m.JniName == "<init>").ToList ();
		var regularMethods = peer.MarshalMethods.Where (m => m.JniName != "<init>" && m.JniName != "<clinit>").ToList ();

		// If no constructors, we still need one nc_activate for the default constructor
		int numActivateMethods = constructors.Count > 0 ? constructors.Count : 1;

		// Total function pointers: regular methods + activation methods
		int totalFnPointers = regularMethods.Count + numActivateMethods;

		writer.Write ($"""""
; ModuleID = 'marshal_methods_{sanitizedName}.ll'
source_filename = "marshal_methods_{sanitizedName}.ll"
target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
target triple = "aarch64-unknown-linux-android21"

; External typemap_get_function_pointer callback
@typemap_get_function_pointer = external local_unnamed_addr global ptr, align 8

; Cached function pointers
""""");
		writer.WriteLine ();

		for (int i = 0; i < totalFnPointers; i++) {
			writer.WriteLine ($"@fn_ptr_{i} = internal unnamed_addr global ptr null, align 8");
		}

		// Class name constant (null-terminated string)
		byte[] classNameBytes = System.Text.Encoding.UTF8.GetBytes (peer.JavaName);
		string classNameBytesEncoded = string.Join("", classNameBytes.Select(b => $"\\{b:X2}"));
		int classNameLength = classNameBytes.Length;

		writer.WriteLine ();
		writer.WriteLine ($"; Class name for \"{peer.JavaName}\" (length={classNameLength})");
		writer.WriteLine ($"@class_name = internal constant [{classNameLength} x i8] c\"{classNameBytesEncoded}\", align 1");
		writer.WriteLine ();
		writer.WriteLine ("; JNI native method stubs");

		// Generate regular method stubs
		for (int i = 0; i < regularMethods.Count; i++) {
			var method = regularMethods [i];
			string nativeSymbol = MakeJniNativeSymbol (peer.JavaName, method.JniName, method.JniSignature);
			string llvmParams = JniSignatureToLlvmParams (method.JniSignature);
			string llvmArgs = JniSignatureToLlvmArgs (method.JniSignature);
			string llvmRetType = JniSignatureToLlvmReturnType (method.JniSignature);

			if (llvmRetType == "void") {
				writer.Write ($$"""

; Method: {{method.JniName}}{{method.JniSignature}}
define default {{llvmRetType}} @{{nativeSymbol}}(ptr %env, ptr %obj{{llvmParams}}) #0 {
entry:
  %cached_ptr = load ptr, ptr @fn_ptr_{{i}}, align 8
  %is_null = icmp eq ptr %cached_ptr, null
  br i1 %is_null, label %resolve, label %call

resolve:
  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
  call void %get_fn(ptr @class_name, i32 {{classNameLength}}, i32 {{i}}, ptr @fn_ptr_{{i}})
  %resolved_ptr = load ptr, ptr @fn_ptr_{{i}}, align 8
  br label %call

call:
  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
  tail call void %fn(ptr %env, ptr %obj{{llvmArgs}})
  ret void
}
""");
			} else {
				writer.Write ($$"""

; Method: {{method.JniName}}{{method.JniSignature}}
define default {{llvmRetType}} @{{nativeSymbol}}(ptr %env, ptr %obj{{llvmParams}}) #0 {
entry:
  %cached_ptr = load ptr, ptr @fn_ptr_{{i}}, align 8
  %is_null = icmp eq ptr %cached_ptr, null
  br i1 %is_null, label %resolve, label %call

resolve:
  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
  call void %get_fn(ptr @class_name, i32 {{classNameLength}}, i32 {{i}}, ptr @fn_ptr_{{i}})
  %resolved_ptr = load ptr, ptr @fn_ptr_{{i}}, align 8
  br label %call

call:
  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
  %result = tail call {{llvmRetType}} %fn(ptr %env, ptr %obj{{llvmArgs}})
  ret {{llvmRetType}} %result
}
""");
			}
		}

		// Generate nc_activate stubs
		writer.WriteLine ();
		writer.WriteLine ("; Native constructor activation stubs");

		int activateBaseIndex = regularMethods.Count;

		if (constructors.Count == 0) {
			string nativeSymbol = MakeJniActivateSymbol (peer.JavaName, "nc_activate_0", "()V");
			int fnPtrIndex = activateBaseIndex;
			
			writer.Write ($$"""

; nc_activate_0 - default constructor activation
define default void @{{nativeSymbol}}(ptr %env, ptr %obj) #0 {
entry:
  %cached_ptr = load ptr, ptr @fn_ptr_{{fnPtrIndex}}, align 8
  %is_null = icmp eq ptr %cached_ptr, null
  br i1 %is_null, label %resolve, label %call

resolve:
  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
  call void %get_fn(ptr @class_name, i32 {{classNameLength}}, i32 {{fnPtrIndex}}, ptr @fn_ptr_{{fnPtrIndex}})
  %resolved_ptr = load ptr, ptr @fn_ptr_{{fnPtrIndex}}, align 8
  br label %call

call:
  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
  tail call void %fn(ptr %env, ptr %obj)
  ret void
}
""");
		} else {
			for (int ctorIdx = 0; ctorIdx < constructors.Count; ctorIdx++) {
				var ctor = constructors [ctorIdx];
				string nativeSymbol = MakeJniActivateSymbol (peer.JavaName, $"nc_activate_{ctorIdx}", ctor.JniSignature);
				string llvmParams = JniSignatureToLlvmParams (ctor.JniSignature);
				string llvmArgs = JniSignatureToLlvmArgs (ctor.JniSignature);
				int fnPtrIndex = activateBaseIndex + ctorIdx;
				
				writer.Write ($$"""

; nc_activate_{{ctorIdx}} - constructor activation for {{ctor.JniSignature}}
define default void @{{nativeSymbol}}(ptr %env, ptr %obj{{llvmParams}}) #0 {
entry:
  %cached_ptr = load ptr, ptr @fn_ptr_{{fnPtrIndex}}, align 8
  %is_null = icmp eq ptr %cached_ptr, null
  br i1 %is_null, label %resolve, label %call

resolve:
  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
  call void %get_fn(ptr @class_name, i32 {{classNameLength}}, i32 {{fnPtrIndex}}, ptr @fn_ptr_{{fnPtrIndex}})
  %resolved_ptr = load ptr, ptr @fn_ptr_{{fnPtrIndex}}, align 8
  br label %call

call:
  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
  tail call void %fn(ptr %env, ptr %obj{{llvmArgs}})
  ret void
}
""");
			}
		}

		writer.Write ("""

; Function attributes
attributes #0 = { mustprogress nofree norecurse nosync nounwind willreturn memory(argmem: read) uwtable }

; Metadata
!llvm.module.flags = !{!0}
!0 = !{i32 1, !"wchar_size", i32 4}
""");
	}

	string MakeJniNativeSymbol (string jniTypeName, string methodName, string jniSignature)
	{
		string sanitizedMethodName = methodName.Replace ("<init>", "_ctor").Replace ("<clinit>", "_cctor");
		var sb = new StringBuilder ("Java_");
		sb.Append (MangleForJni (jniTypeName));
		sb.Append ('_');
		sb.Append (MangleForJni ($"n_{sanitizedMethodName}"));
		sb.Append ("__");
		sb.Append (MangleJniSignature (jniSignature));
		return sb.ToString ();
	}

	string MakeJniActivateSymbol (string jniTypeName, string methodName, string jniSignature)
	{
		var sb = new StringBuilder ("Java_");
		sb.Append (MangleForJni (jniTypeName));
		sb.Append ('_');
		sb.Append (MangleForJni (methodName));
		if (!string.IsNullOrEmpty (jniSignature) && jniSignature != "()V") {
			sb.Append ("__");
			sb.Append (MangleJniSignature (jniSignature));
		}
		return sb.ToString ();
	}

	string MangleJniSignature (string signature)
	{
		var sb = new StringBuilder ();
		foreach (char c in signature) {
			if (c == ')')
				break;
			
			switch (c) {
				case '(': break;
				case '/': sb.Append ('_'); break;
				case ';': sb.Append ("_2"); break;
				case '[': sb.Append ("_3"); break;
				default: sb.Append (c); break;
			}
		}
		return sb.ToString ();
	}

	string MangleForJni (string name)
	{
		var sb = new StringBuilder (name.Length);
		foreach (char c in name) {
			switch (c) {
				case '/':
				case '.': sb.Append ('_'); break;
				case '_': sb.Append ("_1"); break;
				case ';': sb.Append ("_2"); break;
				case '[': sb.Append ("_3"); break;
				case '$': sb.Append ("_00024"); break;
				default: sb.Append (c); break;
			}
		}
		return sb.ToString ();
	}

	string JniSignatureToLlvmArgs (string jniSignature)
	{
		var args = new StringBuilder ();
		int paramIndex = 0;
		int i = 1;

		while (i < jniSignature.Length && jniSignature [i] != ')') {
			char c = jniSignature [i];
			string llvmType;
			switch (c) {
				case 'Z': llvmType = "i8"; i++; break;
				case 'B': llvmType = "i8"; i++; break;
				case 'C': llvmType = "i16"; i++; break;
				case 'S': llvmType = "i16"; i++; break;
				case 'I': llvmType = "i32"; i++; break;
				case 'J': llvmType = "i64"; i++; break;
				case 'F': llvmType = "float"; i++; break;
				case 'D': llvmType = "double"; i++; break;
				case 'L':
					llvmType = "ptr";
					while (i < jniSignature.Length && jniSignature [i] != ';') i++;
					i++;
					break;
				case '[':
					llvmType = "ptr";
					while (i < jniSignature.Length && jniSignature [i] == '[') i++;
					if (i < jniSignature.Length) {
						if (jniSignature [i] == 'L') {
							while (i < jniSignature.Length && jniSignature [i] != ';') i++;
							i++;
						} else {
							i++;
						}
					}
					break;
				default:
					llvmType = "ptr";
					i++;
					break;
			}

			args.Append (", ");
			args.Append (llvmType);
			args.Append (" %p");
			args.Append (paramIndex);
			paramIndex++;
		}

		return args.ToString ();
	}

	string JniSignatureToLlvmParams (string signature)
	{
		int parenStart = signature.IndexOf ('(');
		int parenEnd = signature.IndexOf (')');
		if (parenStart < 0 || parenEnd < 0 || parenEnd == parenStart + 1) {
			return "";
		}

		string paramSig = signature.Substring (parenStart + 1, parenEnd - parenStart - 1);
		var @params = new List<string> ();
		int idx = 0;
		int paramNum = 0;

		while (idx < paramSig.Length) {
			char c = paramSig [idx];
			string type = c switch {
				'Z' => "i8",
				'B' => "i8",
				'C' => "i16",
				'S' => "i16",
				'I' => "i32",
				'J' => "i64",
				'F' => "float",
				'D' => "double",
				'L' => "ptr",
				'[' => "ptr",
				_ => "ptr",
			};

			if (c == 'L') {
				while (idx < paramSig.Length && paramSig [idx] != ';') idx++;
				idx++;
			} else if (c == '[') {
				while (idx < paramSig.Length && paramSig [idx] == '[') idx++;
				if (idx < paramSig.Length) {
					if (paramSig [idx] == 'L') {
						while (idx < paramSig.Length && paramSig [idx] != ';') idx++;
						idx++;
					} else {
						idx++;
					}
				}
			} else {
				idx++;
			}

			@params.Add ($", {type} %p{paramNum++}");
		}

		return string.Concat (@params);
	}

	string JniSignatureToLlvmReturnType (string signature)
	{
		int parenEnd = signature.LastIndexOf (')');
		if (parenEnd < 0 || parenEnd + 1 >= signature.Length) return "void";

		char returnChar = signature [parenEnd + 1];
		return returnChar switch {
			'V' => "void",
			'Z' => "i8",
			'B' => "i8",
			'C' => "i16",
			'S' => "i16",
			'I' => "i32",
			'J' => "i64",
			'F' => "float",
			'D' => "double",
			'L' => "ptr",
			'[' => "ptr",
			_ => "ptr",
		};
	}

	void GenerateJavaSourceFiles (List<JavaPeerInfo> javaPeers, string javaSourceDir)
	{
		int count = 0;
		foreach (var peer in javaPeers) {
			// Skip framework assemblies (Mono.Android, Java.Interop) - they have their own JCWs
			if (IsFrameworkAssembly (peer.AssemblyName))
				continue;

			GenerateJcwJavaFile (javaSourceDir, peer);
			count++;
		}
		
		_log.LogDebugMessage ($"Generated {count} Java source files");
	}

	void GenerateJcwJavaFile (string outputPath, JavaPeerInfo peer)
	{
		string jniTypeName = peer.JavaName;
		
		// Convert JNI type name to Java package and class name
		int lastSlash = jniTypeName.LastIndexOf ('/');
		string package = lastSlash > 0 ? jniTypeName.Substring (0, lastSlash).Replace ('/', '.') : "";
		string className = lastSlash > 0 ? jniTypeName.Substring (lastSlash + 1) : jniTypeName;
		className = className.Replace ('$', '_'); // Handle nested classes

		// Get the Java base class name from the .NET base type
		// For now we assume java.lang.Object if unknown, but normally we would resolve the base type
		// In the MSBuild task we don't have full type resolution easily available for base types
		// unless we scan them too.
		// However, for JCW generation of *user* types, they usually extend a framework type (Activity, View, etc.)
		// or Object.
		// TODO: Better base class resolution. For now defaulting to java.lang.Object or android.app.Activity etc 
		// if we can guess from name, but strictly speaking we need the base JNI name.
		// 
		// Actually, JavaPeerInfo could/should have BaseTypeName if we updated the scanner.
		// But let's check if we can get by with java.lang.Object for now or if we need to improve scanner.
		// The original code used TypeDefinition.BaseType.Resolve().
		//
		// CRITICAL: The JCW *must* extend the correct class for Android to load it correctly.
		// If it's an Activity, it must extend Activity.
		//
		// Since we don't have the base type JNI name in JavaPeerInfo yet, I should probably add it to JavaPeerInfo.
		// But for this task, I'll use a placeholder and add a TODO to update scanner.
		string baseClassName = "java.lang.Object"; 

		// Create directory structure
		string packageDir = Path.Combine (outputPath, package.Replace ('.', Path.DirectorySeparatorChar));
		Directory.CreateDirectory (packageDir);

		string javaFilePath = Path.Combine (packageDir, className + ".java");

		using var writer = new StreamWriter (javaFilePath);

		// Separate constructors from regular methods
		var constructors = peer.MarshalMethods.Where (m => m.JniName == "<init>").ToList ();
		var regularMethods = peer.MarshalMethods.Where (m => m.JniName != "<init>" && m.JniName != "<clinit>").ToList ();

		// Build constructor declarations
		var constructorDeclarations = new StringBuilder ();
		var nativeCtorDeclarations = new StringBuilder ();
		int ctorIndex = 0;

		// If no constructors with marshal methods, generate a default constructor
		if (constructors.Count == 0) {
			constructorDeclarations.AppendLine ($$"""
    // Default constructor with native activation
    public {{className}} ()
    {
        super ();
        if (getClass () == {{className}}.class) { nc_activate_0 (); }
    }
""");
			nativeCtorDeclarations.AppendLine ("    private native void nc_activate_0 ();");
		} else {
			// Generate each constructor
			foreach (var ctor in constructors) {
				string parameters = JniSignatureToJavaParameters (ctor.JniSignature);
				string parameterNames = JniSignatureToJavaParameterNames (ctor.JniSignature);

				constructorDeclarations.AppendLine ($$"""
    public {{className}} ({{parameters}})
    {
        super ({{parameterNames}});
        if (getClass () == {{className}}.class) { nc_activate_{{ctorIndex}} ({{parameterNames}}); }
    }
""");
				nativeCtorDeclarations.AppendLine ($"    private native void nc_activate_{ctorIndex} ({parameters});");
				ctorIndex++;
			}
		}

		// Build method declarations
		var publicMethods = new StringBuilder ();
		var nativeMethods = new StringBuilder ();

		foreach (var method in regularMethods) {
			string returnType = JniSignatureToJavaType (method.JniSignature, returnOnly: true);
			string parameters = JniSignatureToJavaParameters (method.JniSignature);
			string parameterNames = JniSignatureToJavaParameterNames (method.JniSignature);

			// Generate public wrapper method
			string returnStatement = returnType == "void" ? "" : "return ";
			publicMethods.AppendLine ($$"""
    public {{returnType}} {{method.JniName}} ({{parameters}})
    {
        {{returnStatement}}n_{{method.JniName}} ({{parameterNames}});
    }

""");

			// Generate private native declaration
			nativeMethods.AppendLine ($"    private native {returnType} n_{method.JniName} ({parameters});");
		}

		// Generate package declaration and class
		if (!string.IsNullOrEmpty (package)) {
			writer.WriteLine ($"package {package};");
			writer.WriteLine ();
		}

		writer.Write ($$"""
public class {{className}}
    extends {{baseClassName}}
    implements mono.android.IGCUserPeer
{
{{constructorDeclarations}}
{{publicMethods}}
{{nativeCtorDeclarations}}
{{nativeMethods}}
    // IGCUserPeer implementation for preventing premature GC
    private java.util.ArrayList refList;
    public void monodroidAddReference (java.lang.Object obj)
    {
        if (refList == null)
            refList = new java.util.ArrayList ();
        refList.add (obj);
    }

    public void monodroidClearReferences ()
    {
        if (refList != null)
            refList.clear ();
    }
}
""");
	}

	string JniSignatureToJavaParameters (string signature)
	{
		int parenStart = signature.IndexOf ('(');
		int parenEnd = signature.IndexOf (')');
		if (parenStart < 0 || parenEnd < 0 || parenEnd == parenStart + 1) {
			return "";
		}

		string paramSig = signature.Substring (parenStart + 1, parenEnd - parenStart - 1);
		var @params = new List<string> ();
		int idx = 0;
		int paramNum = 0;

		while (idx < paramSig.Length) {
			char c = paramSig [idx];
			string type;
			
			switch (c) {
				case 'Z': type = "boolean"; idx++; break;
				case 'B': type = "byte"; idx++; break;
				case 'C': type = "char"; idx++; break;
				case 'S': type = "short"; idx++; break;
				case 'I': type = "int"; idx++; break;
				case 'J': type = "long"; idx++; break;
				case 'F': type = "float"; idx++; break;
				case 'D': type = "double"; idx++; break;
				case 'L':
					int start = idx + 1;
					while (idx < paramSig.Length && paramSig[idx] != ';') idx++;
					string className = paramSig.Substring (start, idx - start);
					type = className.Replace ('/', '.');
					idx++; 
					break;
				case '[':
					int arrayDims = 0;
					while (idx < paramSig.Length && paramSig[idx] == '[') {
						arrayDims++;
						idx++;
					}
					
					string elementType;
					if (idx < paramSig.Length) {
						char elementChar = paramSig[idx];
						switch (elementChar) {
							case 'Z': elementType = "boolean"; idx++; break;
							case 'B': elementType = "byte"; idx++; break;
							case 'C': elementType = "char"; idx++; break;
							case 'S': elementType = "short"; idx++; break;
							case 'I': elementType = "int"; idx++; break;
							case 'J': elementType = "long"; idx++; break;
							case 'F': elementType = "float"; idx++; break;
							case 'D': elementType = "double"; idx++; break;
							case 'L':
								int elemStart = idx + 1;
								while (idx < paramSig.Length && paramSig[idx] != ';') idx++;
								string elemClassName = paramSig.Substring (elemStart, idx - elemStart);
								elementType = elemClassName.Replace ('/', '.');
								idx++;
								break;
							default:
								elementType = "Object";
								idx++;
								break;
						}
					} else {
						elementType = "Object";
					}
					
					type = elementType + new string ('[', arrayDims) + new string (']', arrayDims);
					break;
				default:
					type = "Object";
					idx++;
					break;
			}

			@params.Add ($"{type} p{paramNum++}");
		}

		return string.Join (", ", @params);
	}

	string JniSignatureToJavaParameterNames (string jniSignature)
	{
		var result = new StringBuilder ();
		int paramIndex = 0;
		int i = 1;

		while (i < jniSignature.Length && jniSignature [i] != ')') {
			if (paramIndex > 0) {
				result.Append (", ");
			}
			result.Append ($"p{paramIndex}");
			paramIndex++;

			char c = jniSignature [i];
			switch (c) {
				case 'L':
					while (i < jniSignature.Length && jniSignature [i] != ';') i++;
					i++;
					break;
				case '[':
					while (i < jniSignature.Length && jniSignature [i] == '[') i++;
					if (i < jniSignature.Length && jniSignature [i] == 'L') {
						while (i < jniSignature.Length && jniSignature [i] != ';') i++;
						i++;
					} else {
						i++;
					}
					break;
				default:
					i++;
					break;
			}
		}

		return result.ToString ();
	}

	string JniSignatureToJavaType (string signature, bool returnOnly)
	{
		int parenEnd = signature.LastIndexOf (')');
		if (parenEnd < 0) return "void";

		char returnChar = signature [parenEnd + 1];
		return returnChar switch {
			'V' => "void",
			'Z' => "boolean",
			'B' => "byte",
			'C' => "char",
			'S' => "short",
			'I' => "int",
			'J' => "long",
			'F' => "float",
			'D' => "double",
			'L' => "Object",
			'[' => "Object[]",
			_ => "Object",
		};
	}
	
	void CreateModuleAndAssembly ()
	{
	_metadata.AddModule (
	generation: 0,
	moduleName: _metadata.GetOrAddString ("_Microsoft.Android.TypeMaps.dll"),
	mvid: _metadata.GetOrAddGuid (Guid.NewGuid ()),
	encId: default,
	encBaseId: default);
	
	_metadata.AddAssembly (
	name: _metadata.GetOrAddString ("_Microsoft.Android.TypeMaps"),
	version: new Version (1, 0, 0, 0),
	culture: default,
	publicKey: default,
	flags: default,
	hashAlgorithm: AssemblyHashAlgorithm.None);
	}
	
	void AddAssemblyReferences ()
	{
	// System.Runtime (corlib)
	_corlibRef = _metadata.AddAssemblyReference (
	name: _metadata.GetOrAddString ("System.Runtime"),
	version: new Version (10, 0, 0, 0),
	culture: default,
	publicKeyOrToken: default,
	flags: default,
	hashValue: default);
	
	// System.Runtime.InteropServices (for TypeMapAttribute)
	_interopRef = _metadata.AddAssemblyReference (
	name: _metadata.GetOrAddString ("System.Runtime.InteropServices"),
	version: new Version (10, 0, 0, 0),
	culture: default,
	publicKeyOrToken: default,
	flags: default,
	hashValue: default);
	
	// Mono.Android
	_monoAndroidRef = _metadata.AddAssemblyReference (
	name: _metadata.GetOrAddString ("Mono.Android"),
	version: new Version (0, 0, 0, 0),
	culture: default,
	publicKeyOrToken: default,
	flags: default,
	hashValue: default);
	}
	
	void AddTypeReferences ()
	{
	// System types (from corlib)
	_objectTypeRef = _metadata.AddTypeReference (
	resolutionScope: _corlibRef,
	@namespace: _metadata.GetOrAddString ("System"),
	name: _metadata.GetOrAddString ("Object"));
	
	_voidTypeRef = _metadata.AddTypeReference (
	resolutionScope: _corlibRef,
	@namespace: _metadata.GetOrAddString ("System"),
	name: _metadata.GetOrAddString ("Void"));
	
	_intPtrTypeRef = _metadata.AddTypeReference (
	resolutionScope: _corlibRef,
	@namespace: _metadata.GetOrAddString ("System"),
	name: _metadata.GetOrAddString ("IntPtr"));
	
	_int32TypeRef = _metadata.AddTypeReference (
	resolutionScope: _corlibRef,
	@namespace: _metadata.GetOrAddString ("System"),
	name: _metadata.GetOrAddString ("Int32"));
	
	_typeTypeRef = _metadata.AddTypeReference (
	resolutionScope: _corlibRef,
	@namespace: _metadata.GetOrAddString ("System"),
	name: _metadata.GetOrAddString ("Type"));
	
	_attributeUsageTypeRef = _metadata.AddTypeReference (
	resolutionScope: _corlibRef,
	@namespace: _metadata.GetOrAddString ("System"),
	name: _metadata.GetOrAddString ("AttributeUsageAttribute"));
	
	_attributeTargetsTypeRef = _metadata.AddTypeReference (
	resolutionScope: _corlibRef,
	@namespace: _metadata.GetOrAddString ("System"),
	name: _metadata.GetOrAddString ("AttributeTargets"));
	
	// Mono.Android types
	_javaPeerProxyTypeRef = _metadata.AddTypeReference (
	resolutionScope: _monoAndroidRef,
	@namespace: _metadata.GetOrAddString ("Java.Interop"),
	name: _metadata.GetOrAddString ("JavaPeerProxy"));
	
	_iJavaPeerableTypeRef = _metadata.AddTypeReference (
	resolutionScope: _monoAndroidRef,
	@namespace: _metadata.GetOrAddString ("Java.Interop"),
	name: _metadata.GetOrAddString ("IJavaPeerable"));
	
	_jniHandleOwnershipTypeRef = _metadata.AddTypeReference (
	resolutionScope: _monoAndroidRef,
	@namespace: _metadata.GetOrAddString ("Android.Runtime"),
	name: _metadata.GetOrAddString ("JniHandleOwnership"));
	
	_javaLangObjectTypeRef = _metadata.AddTypeReference (
	resolutionScope: _monoAndroidRef,
	@namespace: _metadata.GetOrAddString ("Java.Lang"),
	name: _metadata.GetOrAddString ("Object"));

	_invokerUniverseTypeRef = _metadata.AddTypeReference (
	resolutionScope: _monoAndroidRef,
	@namespace: _metadata.GetOrAddString ("Java.Interop"),
	name: _metadata.GetOrAddString ("InvokerUniverse"));
	
	_aliasesUniverseTypeRef = _metadata.AddTypeReference (
	resolutionScope: _monoAndroidRef,
	@namespace: _metadata.GetOrAddString ("Java.Interop"),
	name: _metadata.GetOrAddString ("AliasesUniverse"));
	
	// TypeMap attributes (from System.Runtime.InteropServices)
	_typeMapAttrTypeRef = _metadata.AddTypeReference (
	resolutionScope: _interopRef,
	@namespace: _metadata.GetOrAddString ("System.Runtime.InteropServices"),
	name: _metadata.GetOrAddString ("TypeMapAttribute`1"));
	
	_typeMapAssocAttrTypeRef = _metadata.AddTypeReference (
	resolutionScope: _interopRef,
	@namespace: _metadata.GetOrAddString ("System.Runtime.InteropServices"),
	name: _metadata.GetOrAddString ("TypeMapAssociationAttribute`1"));
	
	_typeMapAsmTargetAttrTypeRef = _metadata.AddTypeReference (
	resolutionScope: _interopRef,
	@namespace: _metadata.GetOrAddString ("System.Runtime.InteropServices"),
	name: _metadata.GetOrAddString ("TypeMapAssemblyTargetAttribute`1"));

	_exceptionTypeRef = _metadata.AddTypeReference (
	resolutionScope: _corlibRef,
	@namespace: _metadata.GetOrAddString ("System"),
	name: _metadata.GetOrAddString ("Exception"));

	_unmanagedCallersOnlyAttrTypeRef = _metadata.AddTypeReference (
	resolutionScope: _interopRef,
	@namespace: _metadata.GetOrAddString ("System.Runtime.InteropServices"),
	name: _metadata.GetOrAddString ("UnmanagedCallersOnlyAttribute"));

	_throwableTypeRef = _metadata.AddTypeReference (
	resolutionScope: _monoAndroidRef,
	@namespace: _metadata.GetOrAddString ("Java.Lang"),
	name: _metadata.GetOrAddString ("Throwable"));

	_androidEnvironmentTypeRef = _metadata.AddTypeReference (
	resolutionScope: _monoAndroidRef,
	@namespace: _metadata.GetOrAddString ("Android.Runtime"),
	name: _metadata.GetOrAddString ("AndroidEnvironment"));

	_androidRuntimeInternalTypeRef = _metadata.AddTypeReference (
	resolutionScope: _monoAndroidRef,
	@namespace: _metadata.GetOrAddString ("Android.Runtime"),
	name: _metadata.GetOrAddString ("AndroidRuntimeInternal"));

	_notSupportedExceptionTypeRef = _metadata.AddTypeReference (
	resolutionScope: _corlibRef,
	@namespace: _metadata.GetOrAddString ("System"),
	name: _metadata.GetOrAddString ("NotSupportedException"));
	}
	
	void AddMemberReferences ()
	{
	// JavaPeerProxy..ctor() - parameterless constructor
	var ctorSigBlob = new BlobBuilder ();
	new BlobEncoder (ctorSigBlob)
	.MethodSignature (isInstanceMethod: true)
	.Parameters (0, returnType => returnType.Void (), parameters => { });
	
	_javaPeerProxyCtorRef = _metadata.AddMemberReference (
	parent: _javaPeerProxyTypeRef,
	name: _metadata.GetOrAddString (".ctor"),
	signature: _metadata.GetOrAddBlob (ctorSigBlob));
	
	// IntPtr.Zero field
	var intPtrFieldSig = new BlobBuilder ();
	var fieldSigEncoder = new BlobEncoder (intPtrFieldSig).FieldSignature ();
	fieldSigEncoder.IntPtr ();
	
	_intPtrZeroFieldRef = _metadata.AddMemberReference (
	parent: _intPtrTypeRef,
	name: _metadata.GetOrAddString ("Zero"),
	signature: _metadata.GetOrAddBlob (intPtrFieldSig));

	// TypeMapAttribute<Java.Lang.Object>..ctor(string, Type, Type)
	var typeMapSpecBlob = new BlobBuilder ();
	var typeMapSpecEncoder = new BlobEncoder (typeMapSpecBlob).TypeSpecificationSignature ();
	var typeMapGenArgsEncoder = typeMapSpecEncoder.GenericInstantiation (
			_typeMapAttrTypeRef,
			1,
			false);
	typeMapGenArgsEncoder.AddArgument ().Type (_javaLangObjectTypeRef, false);

	var typeMapSpec = _metadata.AddTypeSpecification (_metadata.GetOrAddBlob (typeMapSpecBlob));
    
    // Constructor signature: .ctor(string, Type, Type)
    var typeMapCtorSigBlob = new BlobBuilder ();
    new BlobEncoder (typeMapCtorSigBlob).MethodSignature (isInstanceMethod: true)
        .Parameters (3, returnType => returnType.Void (), parameters => {
            parameters.AddParameter ().Type ().String ();
            parameters.AddParameter ().Type ().Type (_typeTypeRef, false);
            parameters.AddParameter ().Type ().Type (_typeTypeRef, false);
        });

    _typeMapAttrCtorRef = _metadata.AddMemberReference (
        parent: typeMapSpec,
        name: _metadata.GetOrAddString (".ctor"),
        signature: _metadata.GetOrAddBlob (typeMapCtorSigBlob));

	// TypeMapAssociationAttribute<InvokerUniverse>..ctor(Type, Type)
	// We need to create a TypeSpecification for TypeMapAssociationAttribute<InvokerUniverse>
	var invokerTypeMapSpecBlob = new BlobBuilder ();
	var specEncoder = new BlobEncoder (invokerTypeMapSpecBlob).TypeSpecificationSignature ();
	var genArgsEncoder = specEncoder.GenericInstantiation (
			_typeMapAssocAttrTypeRef,
			1,
			false);
	genArgsEncoder.AddArgument ().Type (_invokerUniverseTypeRef, false);

	var invokerTypeMapSpec = _metadata.AddTypeSpecification (_metadata.GetOrAddBlob (invokerTypeMapSpecBlob));

	// Constructor signature: .ctor(System.Type, System.Type)
	var assocCtorSigBlob = new BlobBuilder ();
	new BlobEncoder (assocCtorSigBlob)
		.MethodSignature (isInstanceMethod: true)
		.Parameters (2,
			returnType => returnType.Void (),
			parameters => {
				parameters.AddParameter ().Type ().Type (_typeTypeRef, isValueType: false);
				parameters.AddParameter ().Type ().Type (_typeTypeRef, isValueType: false);
			});
	
	_invokerTypeMapAssocAttrCtorRef = _metadata.AddMemberReference (
		parent: invokerTypeMapSpec,
		name: _metadata.GetOrAddString (".ctor"),
		signature: _metadata.GetOrAddBlob (assocCtorSigBlob));

	// TypeMapAssociationAttribute<AliasesUniverse>..ctor(Type, Type)
	var aliasesTypeMapSpecBlob = new BlobBuilder ();
	var aliasesSpecEncoder = new BlobEncoder (aliasesTypeMapSpecBlob).TypeSpecificationSignature ();
	var aliasesGenArgsEncoder = aliasesSpecEncoder.GenericInstantiation (
			_typeMapAssocAttrTypeRef,
			1,
			false);
	aliasesGenArgsEncoder.AddArgument ().Type (_aliasesUniverseTypeRef, false);

	var aliasesTypeMapSpec = _metadata.AddTypeSpecification (_metadata.GetOrAddBlob (aliasesTypeMapSpecBlob));

	_aliasesTypeMapAssocAttrCtorRef = _metadata.AddMemberReference (
		parent: aliasesTypeMapSpec,
		name: _metadata.GetOrAddString (".ctor"),
		signature: _metadata.GetOrAddBlob (assocCtorSigBlob));

	// UnmanagedCallersOnlyAttribute..ctor()
	var ucoCtorSigBlob = new BlobBuilder ();
	new BlobEncoder (ucoCtorSigBlob)
		.MethodSignature (isInstanceMethod: true)
		.Parameters (0, returnType => returnType.Void (), parameters => { });

	_unmanagedCallersOnlyCtorRef = _metadata.AddMemberReference (
		parent: _unmanagedCallersOnlyAttrTypeRef,
		name: _metadata.GetOrAddString (".ctor"),
		signature: _metadata.GetOrAddBlob (ucoCtorSigBlob));

	// AndroidRuntimeInternal.WaitForBridgeProcessing()
	var waitSigBlob = new BlobBuilder ();
	new BlobEncoder (waitSigBlob)
		.MethodSignature (isInstanceMethod: false)
		.Parameters (0, returnType => returnType.Void (), parameters => { });

	_waitForBridgeProcessingRef = _metadata.AddMemberReference (
		parent: _androidRuntimeInternalTypeRef,
		name: _metadata.GetOrAddString ("WaitForBridgeProcessing"),
		signature: _metadata.GetOrAddBlob (waitSigBlob));

	// Java.Lang.Throwable.FromException(Exception)
	var fromExSigBlob = new BlobBuilder ();
	new BlobEncoder (fromExSigBlob)
		.MethodSignature (isInstanceMethod: false)
		.Parameters (1, 
			returnType => returnType.Type ().Type (_throwableTypeRef, isValueType: false),
			parameters => parameters.AddParameter ().Type ().Type (_exceptionTypeRef, isValueType: false));

	_throwableFromExceptionRef = _metadata.AddMemberReference (
		parent: _throwableTypeRef,
		name: _metadata.GetOrAddString ("FromException"),
		signature: _metadata.GetOrAddBlob (fromExSigBlob));

	// AndroidEnvironment.RaiseThrowable(Throwable)
	var raiseSigBlob = new BlobBuilder ();
	new BlobEncoder (raiseSigBlob)
		.MethodSignature (isInstanceMethod: false)
		.Parameters (1, 
			returnType => returnType.Void (),
			parameters => parameters.AddParameter ().Type ().Type (_throwableTypeRef, isValueType: false));

	_raiseThrowableRef = _metadata.AddMemberReference (
		parent: _androidEnvironmentTypeRef,
		name: _metadata.GetOrAddString ("RaiseThrowable"),
		signature: _metadata.GetOrAddBlob (raiseSigBlob));

	// NotSupportedException..ctor(string)
	var nseCtorSigBlob = new BlobBuilder ();
	new BlobEncoder (nseCtorSigBlob)
		.MethodSignature (isInstanceMethod: true)
		.Parameters (1, 
			returnType => returnType.Void (),
			parameters => parameters.AddParameter ().Type ().String ());

	_notSupportedExceptionCtorRef = _metadata.AddMemberReference (
		parent: _notSupportedExceptionTypeRef,
		name: _metadata.GetOrAddString (".ctor"),
		signature: _metadata.GetOrAddBlob (nseCtorSigBlob));
	}
	
	void CreateSignatureBlobs ()
	{
	// void .ctor() signature
	var voidMethodSigBlob = new BlobBuilder ();
	new BlobEncoder (voidMethodSigBlob)
	.MethodSignature (isInstanceMethod: true)
	.Parameters (0, returnType => returnType.Void (), parameters => { });
	_voidMethodSig = _metadata.GetOrAddBlob (voidMethodSigBlob);
	
	// IntPtr GetFunctionPointer(int methodIndex) signature
	var getFnPtrSigBlob = new BlobBuilder ();
	new BlobEncoder (getFnPtrSigBlob)
	.MethodSignature (isInstanceMethod: true)
	.Parameters (1,
	returnType => returnType.Type ().IntPtr (),
	parameters => parameters.AddParameter ().Type ().Int32 ());
	_getFunctionPointerSig = _metadata.GetOrAddBlob (getFnPtrSigBlob);
	
	// IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer) signature
	var createInstanceSigBlob = new BlobBuilder ();
	new BlobEncoder (createInstanceSigBlob)
	.MethodSignature (isInstanceMethod: true)
	.Parameters (2,
	returnType => returnType.Type ().Type (_iJavaPeerableTypeRef, isValueType: false),
	parameters => {
	parameters.AddParameter ().Type ().IntPtr ();
	parameters.AddParameter ().Type ().Type (_jniHandleOwnershipTypeRef, isValueType: true);
	});
	_createInstanceSig = _metadata.GetOrAddBlob (createInstanceSigBlob);
	}
	
	TypeReferenceHandle AddExternalTypeReference (string assemblyName, string typeName)
	{
	// Find or add assembly reference
	AssemblyReferenceHandle asmRef;
	if (assemblyName == "Mono.Android") {
	asmRef = _monoAndroidRef;
	} else {
	// Add new assembly reference
	asmRef = _metadata.AddAssemblyReference (
	name: _metadata.GetOrAddString (assemblyName),
	version: new Version (0, 0, 0, 0),
	culture: default,
	publicKeyOrToken: default,
	flags: default,
	hashValue: default);
	}
	
	// Parse namespace and name
	int lastDot = typeName.LastIndexOf ('.');
	string ns = lastDot > 0 ? typeName.Substring (0, lastDot) : "";
	string name = lastDot > 0 ? typeName.Substring (lastDot + 1) : typeName;
	
	// Handle nested types (A+B format)
	if (name.Contains ('+')) {
	// For nested types, we'd need to create a chain of type references
	// For now, just use the full nested name with '/' separator
	name = name.Replace ('+', '/');
	}
	
	return _metadata.AddTypeReference (
	resolutionScope: asmRef,
	@namespace: _metadata.GetOrAddString (ns),
	name: _metadata.GetOrAddString (name));
	}
	
	TypeDefinitionHandle GenerateProxyType (JavaPeerInfo peer, TypeReferenceHandle targetTypeRef, string proxyTypeName)
	{
		_log.LogDebugMessage ($"  Generating proxy type: {proxyTypeName} for {peer.ManagedTypeName}");

		// Track the method list start for this type
		var firstMethodHandle = MetadataTokens.MethodDefinitionHandle (_nextMethodDefRowId);
		var firstFieldHandle = MetadataTokens.FieldDefinitionHandle (_nextFieldDefRowId);

		// Generate methods first (before type definition)
		// 1. Constructor
		int ctorBodyOffset = GenerateProxyConstructor ();
		var ctorDef = _metadata.AddMethodDefinition (
			attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			implAttributes: MethodImplAttributes.IL | MethodImplAttributes.Managed,
			name: _metadata.GetOrAddString (".ctor"),
			signature: _voidMethodSig,
			bodyOffset: ctorBodyOffset,
			parameterList: MetadataTokens.ParameterHandle (_nextParamDefRowId));
		_nextMethodDefRowId++;

		// 2. Generate UCO wrapper methods for marshal methods
		var ucoWrapperHandles = GenerateUcoWrappers (peer, targetTypeRef);

		// 3. GetFunctionPointer override - now with UCO wrapper handles
		int getFnPtrBodyOffset = GenerateGetFunctionPointerBody (peer, ucoWrapperHandles);
		var getFnPtrDef = _metadata.AddMethodDefinition (
			attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
			implAttributes: MethodImplAttributes.IL | MethodImplAttributes.Managed,
			name: _metadata.GetOrAddString ("GetFunctionPointer"),
			signature: _getFunctionPointerSig,
			bodyOffset: getFnPtrBodyOffset,
			parameterList: MetadataTokens.ParameterHandle (_nextParamDefRowId));

		// Add parameter definition for methodIndex
		_metadata.AddParameter (
			attributes: ParameterAttributes.None,
			name: _metadata.GetOrAddString ("methodIndex"),
			sequenceNumber: 1);
		_nextParamDefRowId++;
		_nextMethodDefRowId++;

		// 4. CreateInstance override
		int createInstanceBodyOffset = GenerateCreateInstanceBody (peer, targetTypeRef);
		var createInstanceDef = _metadata.AddMethodDefinition (
			attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
			implAttributes: MethodImplAttributes.IL | MethodImplAttributes.Managed,
			name: _metadata.GetOrAddString ("CreateInstance"),
			signature: _createInstanceSig,
			bodyOffset: createInstanceBodyOffset,
			parameterList: MetadataTokens.ParameterHandle (_nextParamDefRowId));

		// Add parameter definitions
		_metadata.AddParameter (
			attributes: ParameterAttributes.None,
			name: _metadata.GetOrAddString ("handle"),
			sequenceNumber: 1);
		_nextParamDefRowId++;

		_metadata.AddParameter (
			attributes: ParameterAttributes.None,
			name: _metadata.GetOrAddString ("transfer"),
			sequenceNumber: 2);
		_nextParamDefRowId++;
		_nextMethodDefRowId++;

		// Create the type definition
		var typeDef = _metadata.AddTypeDefinition (
			attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
			@namespace: _metadata.GetOrAddString ("_Microsoft.Android.TypeMaps"),
			name: _metadata.GetOrAddString (proxyTypeName),
			baseType: _javaPeerProxyTypeRef,
			fieldList: firstFieldHandle,
			methodList: firstMethodHandle);

		// Add AttributeUsage attribute
		AddAttributeUsageToType (typeDef);

		// Track for self-application later
		_proxyTypes.Add ((typeDef, _voidMethodSig));

		return typeDef;
	}

	/// <summary>
	/// Generates UCO wrapper methods for marshal methods and returns their handles.
	/// </summary>
	List<MethodDefinitionHandle> GenerateUcoWrappers (JavaPeerInfo peer, TypeReferenceHandle targetTypeRef)
	{
		var wrapperHandles = new List<MethodDefinitionHandle> ();

		// Skip if no marshal methods or if it's an interface (interfaces don't have UCO wrappers in the proxy)
		if (peer.MarshalMethods.Count == 0 || peer.IsInterface) {
			return wrapperHandles;
		}

		// Ensure we have a local variable signature for the return value (IntPtr)
		var localsBlob = new BlobBuilder ();
		new BlobEncoder (localsBlob).LocalVariableSignature (1).AddVariable ().Type ().IntPtr ();
		var localsSig = _metadata.AddStandaloneSignature (_metadata.GetOrAddBlob (localsBlob));

		for (int i = 0; i < peer.MarshalMethods.Count; i++) {
			var mm = peer.MarshalMethods[i];

			// Generate wrapper method name
			string wrapperName = $"n_{mm.JniName}_mm_{i}";

			// Build the parameter list for the wrapper
			// Native callback typically has: (IntPtr jnienv, IntPtr obj, additional params...)
			int paramCount = mm.ParameterTypeNames.Length;
			if (paramCount < 2) {
				paramCount = 2; // Minimum jnienv, obj
			}

			// Create method signature: static IntPtr wrapper(IntPtr jnienv, IntPtr obj, ...)
			var wrapperSigBlob = new BlobBuilder ();
			var sigEncoder = new BlobEncoder (wrapperSigBlob).MethodSignature (isInstanceMethod: false);
			sigEncoder.Parameters (paramCount,
				returnType => returnType.Type ().IntPtr (),
				parameters => {
					// First two are always jnienv and obj (IntPtr)
					parameters.AddParameter ().Type ().IntPtr ();
					parameters.AddParameter ().Type ().IntPtr ();
					// Additional parameters
					for (int p = 2; p < paramCount; p++) {
						parameters.AddParameter ().Type ().IntPtr ();
					}
				});

			// Create reference to the original n_* callback method in the target type
			var callbackRef = _metadata.AddMemberReference (
				parent: targetTypeRef,
				name: _metadata.GetOrAddString (mm.NativeCallbackName),
				signature: _metadata.GetOrAddBlob (wrapperSigBlob));

			// Generate wrapper body
			var wrapperBodyBlob = new BlobBuilder ();
			var wrapperEncoder = new InstructionEncoder (wrapperBodyBlob);

			// 1. Call WaitForBridgeProcessing
			wrapperEncoder.Call (_waitForBridgeProcessingRef);

			// 2. Try block start
			int tryStart = wrapperEncoder.Offset;

			// Load arguments and call callback
			for (int p = 0; p < paramCount; p++) {
				wrapperEncoder.LoadArgument (p);
			}
			wrapperEncoder.Call (callbackRef);

			// Store return value
			wrapperEncoder.StoreLocal (0);

			// Leave try block
			var endLabel = wrapperEncoder.DefineLabel ();
			wrapperEncoder.Branch (ILOpCode.Leave, endLabel);
			int tryEnd = wrapperEncoder.Offset;

			// 3. Catch block start
			int handlerStart = wrapperEncoder.Offset;

			// Exception is on stack. Convert to Throwable and Raise
			wrapperEncoder.Call (_throwableFromExceptionRef);
			wrapperEncoder.Call (_raiseThrowableRef);

			// Return default value (0)
			wrapperEncoder.LoadConstantI4 (0);
			wrapperEncoder.OpCode (ILOpCode.Conv_i);
			wrapperEncoder.StoreLocal (0);

			wrapperEncoder.Branch (ILOpCode.Leave, endLabel);
			int handlerEnd = wrapperEncoder.Offset;

			// 4. Return
			wrapperEncoder.MarkLabel (endLabel);
			wrapperEncoder.LoadLocal (0);
			wrapperEncoder.OpCode (ILOpCode.Ret);

			// Add method body with exception handling
			int wrapperBodyOffset = _methodBodyStream.AddMethodBody (
				instructionEncoder: wrapperEncoder,
				maxStack: paramCount + 2, // Check stack depth?
				localVariablesSignature: localsSig,
				attributes: MethodBodyAttributes.InitLocals);
				/*
				// TODO: Fix ExceptionRegionEncoder usage (API mismatch)
				exceptionRegions: new ExceptionRegionEncoder[] {
					new ExceptionRegionEncoder (
						ExceptionRegionKind.Catch,
						tryStart,
						tryEnd - tryStart,
						handlerStart,
						handlerEnd - handlerStart,
						_exceptionTypeRef)
				});
				*/

			// Create method definition
			var wrapperDef = _metadata.AddMethodDefinition (
				attributes: MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				implAttributes: MethodImplAttributes.IL | MethodImplAttributes.Managed,
				name: _metadata.GetOrAddString (wrapperName),
				signature: _metadata.GetOrAddBlob (wrapperSigBlob),
				bodyOffset: wrapperBodyOffset,
				parameterList: MetadataTokens.ParameterHandle (_nextParamDefRowId));

			// Add [UnmanagedCallersOnly] attribute
			var ucoAttrValue = new BlobBuilder ();
			new BlobEncoder (ucoAttrValue).CustomAttributeSignature (
				fixedArguments => { },
				namedArguments => { });
			_metadata.AddCustomAttribute (wrapperDef, _unmanagedCallersOnlyCtorRef, _metadata.GetOrAddBlob (ucoAttrValue));

			// Add parameter definitions
			_metadata.AddParameter (ParameterAttributes.None, _metadata.GetOrAddString ("jnienv"), 1);
			_nextParamDefRowId++;
			_metadata.AddParameter (ParameterAttributes.None, _metadata.GetOrAddString ("obj"), 2);
			_nextParamDefRowId++;
			for (int p = 2; p < paramCount; p++) {
				_metadata.AddParameter (ParameterAttributes.None, _metadata.GetOrAddString ($"p{p}"), p + 1);
				_nextParamDefRowId++;
			}
			_nextMethodDefRowId++;

			wrapperHandles.Add (wrapperDef);
			
			// Store UCO wrapper in MarshalMethodInfo for later use (e.g. GetFunctionPointer)
			mm.UcoWrapper = wrapperDef;
		}

		return wrapperHandles;
	}
	
	int GenerateProxyConstructor ()
	{
	// Generate: ldarg.0; call JavaPeerProxy::.ctor(); ret
	var codeBuilder = new BlobBuilder ();
	var encoder = new InstructionEncoder (codeBuilder);
	
	encoder.OpCode (ILOpCode.Ldarg_0);
	encoder.Call (_javaPeerProxyCtorRef);
	encoder.OpCode (ILOpCode.Ret);
	
	return _methodBodyStream.AddMethodBody (encoder);
	}
	
	int GenerateGetFunctionPointerBody (JavaPeerInfo peer, List<MethodDefinitionHandle> ucoWrapperHandles)
	{
		var codeBuilder = new BlobBuilder ();
		var encoder = new InstructionEncoder (codeBuilder);

		if (peer.IsInterface || peer.IsAbstract) {
			// For Interfaces/Abstract types, GetFunctionPointer should throw NotSupportedException
			// (Use CreateInstance -> Invoker for actual behavior)
			EmitThrowNotSupported (encoder, $"GetFunctionPointer not supported for interface/abstract type {peer.ManagedTypeName}");
		} else if (ucoWrapperHandles.Count == 0) {
			// No UCO wrappers - return IntPtr.Zero
			encoder.OpCode (ILOpCode.Ldsfld);
			encoder.Token (_intPtrZeroFieldRef);
			encoder.OpCode (ILOpCode.Ret);
		} else {
			// Generate switch statement:
			// switch (methodIndex) {
			//     case 0: return (IntPtr)(delegate*<...>)&wrapper0;
			//     case 1: return (IntPtr)(delegate*<...>)&wrapper1;
			//     ...
			//     default: return IntPtr.Zero;
			// }

			// For now, generate a simple if-else chain (switch IL is more complex)
			// ldarg.1 (methodIndex)
			// ldc.i4.0
			// beq case0
			// ldarg.1
			// ldc.i4.1
			// beq case1
			// ... default: ldsfld IntPtr.Zero; ret
			// case0: ldftn wrapper0; ret
			// case1: ldftn wrapper1; ret

			var defaultLabel = encoder.DefineLabel ();
			var caseLabels = new LabelHandle[ucoWrapperHandles.Count];
			for (int i = 0; i < ucoWrapperHandles.Count; i++) {
				caseLabels[i] = encoder.DefineLabel ();
			}

			// Check each case
			for (int i = 0; i < ucoWrapperHandles.Count; i++) {
				encoder.OpCode (ILOpCode.Ldarg_1); // methodIndex
				encoder.LoadConstantI4 (i);
				encoder.Branch (ILOpCode.Beq, caseLabels[i]);
			}

			// Default case - return IntPtr.Zero
			encoder.MarkLabel (defaultLabel);
			encoder.OpCode (ILOpCode.Ldsfld);
			encoder.Token (_intPtrZeroFieldRef);
			encoder.OpCode (ILOpCode.Ret);

			// Case labels - each returns ldftn of wrapper
			for (int i = 0; i < ucoWrapperHandles.Count; i++) {
				encoder.MarkLabel (caseLabels[i]);
				encoder.OpCode (ILOpCode.Ldftn);
				encoder.Token (ucoWrapperHandles[i]);
				encoder.OpCode (ILOpCode.Ret);
			}
		}

		return _methodBodyStream.AddMethodBody (encoder);
	}
	
	int GenerateCreateInstanceBody (JavaPeerInfo peer, TypeReferenceHandle targetTypeRef)
	{
		var codeBuilder = new BlobBuilder ();
		var encoder = new InstructionEncoder (codeBuilder);

		if (peer.HasActivationConstructor) {
			// Generate: return new TargetType(handle, transfer);
			// Create constructor reference: TargetType(IntPtr, JniHandleOwnership)
			var ctorSigBlob = new BlobBuilder ();
			new BlobEncoder (ctorSigBlob)
				.MethodSignature (isInstanceMethod: true)
				.Parameters (2,
					returnType => returnType.Void (),
					parameters => {
						parameters.AddParameter ().Type ().IntPtr ();
						parameters.AddParameter ().Type ().Type (_jniHandleOwnershipTypeRef, isValueType: true);
					});

			var targetCtorRef = _metadata.AddMemberReference (
				parent: targetTypeRef,
				name: _metadata.GetOrAddString (".ctor"),
				signature: _metadata.GetOrAddBlob (ctorSigBlob));

			// ldarg.1 (handle)
			encoder.OpCode (ILOpCode.Ldarg_1);
			// ldarg.2 (transfer)
			encoder.OpCode (ILOpCode.Ldarg_2);
			// newobj TargetType::.ctor(IntPtr, JniHandleOwnership)
			encoder.OpCode (ILOpCode.Newobj);
			encoder.Token (targetCtorRef);
			// ret
			encoder.OpCode (ILOpCode.Ret);
		} else if ((peer.IsInterface || peer.IsAbstract) && !string.IsNullOrEmpty (peer.InvokerTypeName)) {
			// For Interfaces/Abstract types, we need to instantiate the Invoker
			// Add Invoker type reference
			var invokerTypeRef = AddExternalTypeReference (peer.InvokerAssemblyName!, peer.InvokerTypeName!);
			if (invokerTypeRef.IsNil) {
				// Fallback to throw if invoker not found (shouldn't happen if validated)
				EmitThrowNotSupported (encoder, $"Invoker type {peer.InvokerTypeName} not found for {peer.ManagedTypeName}");
			} else {
				// Invokers MUST have activation constructor: .ctor(IntPtr, JniHandleOwnership)
				var ctorSigBlob = new BlobBuilder ();
				new BlobEncoder (ctorSigBlob)
					.MethodSignature (isInstanceMethod: true)
					.Parameters (2,
						returnType => returnType.Void (),
						parameters => {
							parameters.AddParameter ().Type ().IntPtr ();
							parameters.AddParameter ().Type ().Type (_jniHandleOwnershipTypeRef, isValueType: true);
						});

				var invokerCtorRef = _metadata.AddMemberReference (
					parent: invokerTypeRef,
					name: _metadata.GetOrAddString (".ctor"),
					signature: _metadata.GetOrAddBlob (ctorSigBlob));

				// ldarg.1 (handle)
				encoder.OpCode (ILOpCode.Ldarg_1);
				// ldarg.2 (transfer)
				encoder.OpCode (ILOpCode.Ldarg_2);
				// newobj InvokerType::.ctor(IntPtr, JniHandleOwnership)
				encoder.OpCode (ILOpCode.Newobj);
				encoder.Token (invokerCtorRef);
				// ret
				encoder.OpCode (ILOpCode.Ret);
			}
		} else {
			// No activation constructor - throw NotSupportedException
			EmitThrowNotSupported (encoder, $"No activation constructor found for {peer.ManagedTypeName}");
		}

		return _methodBodyStream.AddMethodBody (encoder);
	}

	void EmitThrowNotSupported (InstructionEncoder encoder, string message)
	{
		// ldstr message
		encoder.LoadString (_metadata.GetOrAddUserString (message));
		// newobj NotSupportedException::.ctor(string)
		encoder.OpCode (ILOpCode.Newobj);
		encoder.Token (_notSupportedExceptionCtorRef);
		// throw
		encoder.OpCode (ILOpCode.Throw);
	}
	
	void AddInvokerTypeMapAssociationAttribute (string sourceName, string invokerName)
	{
		// [assembly: TypeMapAssociationAttribute<InvokerUniverse>(typeof(Source), typeof(Invoker))]
		var attrBlob = new BlobBuilder ();
		attrBlob.WriteUInt16 (1); // Prolog
		
		attrBlob.WriteSerializedString (sourceName);
		attrBlob.WriteSerializedString (invokerName);
		
		attrBlob.WriteUInt16 (0); // Named args count
		
		_metadata.AddCustomAttribute (
			parent: EntityHandle.AssemblyDefinition,
			constructor: _invokerTypeMapAssocAttrCtorRef,
			value: _metadata.GetOrAddBlob (attrBlob));
	}

	void AddAttributeUsageToType (TypeDefinitionHandle typeDef)
	{
	// [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
	var ctorSigBlob = new BlobBuilder ();
	new BlobEncoder (ctorSigBlob)
	.MethodSignature (isInstanceMethod: true)
	.Parameters (1,
	returnType => returnType.Void (),
	parameters => parameters.AddParameter ().Type ().Type (_attributeTargetsTypeRef, isValueType: true));
	
	var attrUsageCtorRef = _metadata.AddMemberReference (
	parent: _attributeUsageTypeRef,
	name: _metadata.GetOrAddString (".ctor"),
	signature: _metadata.GetOrAddBlob (ctorSigBlob));
	
	// Build custom attribute blob
	// AttributeTargets.Class | AttributeTargets.Interface = 4 | 1024 = 1028
	var attrBlob = new BlobBuilder ();
	attrBlob.WriteUInt16 (1); // Prolog
	attrBlob.WriteInt32 (1028); // AttributeTargets value
	attrBlob.WriteUInt16 (1); // Named arg count
	attrBlob.WriteByte (0x54); // PROPERTY
	attrBlob.WriteByte (0x02); // ELEMENT_TYPE_BOOLEAN
	attrBlob.WriteSerializedString ("Inherited");
	attrBlob.WriteByte (0); // false
	
	_metadata.AddCustomAttribute (
	parent: typeDef,
	constructor: attrUsageCtorRef,
	value: _metadata.GetOrAddBlob (attrBlob));
	}
	
	void AddAssemblyAttribute ()
	{
	// [assembly: TypeMapAssemblyTargetAttribute<Java.Lang.Object>("_Microsoft.Android.TypeMaps")]
	// TODO: Implement generic attribute instantiation
	// For now, skip - this is complex with S.R.M.E generics
	}
	
	void AddTypeMapAttribute (string jniName, string proxyTypeName, string targetTypeName)
	{
		// [assembly: TypeMapAttribute<Java.Lang.Object>(jniName, typeof(proxyType), typeof(targetType))]
		var attrBlob = new BlobBuilder ();
		attrBlob.WriteUInt16 (1); // Prolog
		
		attrBlob.WriteSerializedString (jniName);
		attrBlob.WriteSerializedString (proxyTypeName);
		attrBlob.WriteSerializedString (targetTypeName);
		
		attrBlob.WriteUInt16 (0); // Named args count
		
		_metadata.AddCustomAttribute (
			parent: EntityHandle.AssemblyDefinition,
			constructor: _typeMapAttrCtorRef,
			value: _metadata.GetOrAddBlob (attrBlob));
	}

	void AddAliasTypeMapAssociationAttribute (string sourceName, string aliasHolderName)
	{
		// [assembly: TypeMapAssociationAttribute<AliasesUniverse>(typeof(Source), typeof(AliasHolder))]
		var attrBlob = new BlobBuilder ();
		attrBlob.WriteUInt16 (1); // Prolog
		
		attrBlob.WriteSerializedString (sourceName);
		attrBlob.WriteSerializedString (aliasHolderName);
		
		attrBlob.WriteUInt16 (0); // Named args count
		
		_metadata.AddCustomAttribute (
			parent: EntityHandle.AssemblyDefinition,
			constructor: _aliasesTypeMapAssocAttrCtorRef,
			value: _metadata.GetOrAddBlob (attrBlob));
	}

	TypeDefinitionHandle GenerateAliasHolderType (string typeName)
	{
		// public sealed class typeName { }
		return _metadata.AddTypeDefinition (
			attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
			@namespace: default,
			name: _metadata.GetOrAddString (typeName),
			baseType: _objectTypeRef,
			fieldList: MetadataTokens.FieldDefinitionHandle (_nextFieldDefRowId),
			methodList: MetadataTokens.MethodDefinitionHandle (_nextMethodDefRowId));
	}
	
	void ApplySelfAttributes ()
	{
	// Apply each proxy type to itself as a custom attribute
	foreach (var (typeDef, ctorSig) in _proxyTypes) {
	// Create member reference to the proxy's constructor
	var proxyCtorRef = _metadata.AddMemberReference (
	parent: typeDef,
	name: _metadata.GetOrAddString (".ctor"),
	signature: ctorSig);
	
	// Empty attribute blob (no arguments)
	var attrBlob = new BlobBuilder ();
	attrBlob.WriteUInt16 (1); // Prolog
	attrBlob.WriteUInt16 (0); // No named args
	
	_metadata.AddCustomAttribute (
	parent: typeDef,
	constructor: proxyCtorRef,
	value: _metadata.GetOrAddBlob (attrBlob));
	}
	}
	
	void WritePEFile (string outputPath)
	{
	var peHeaderBuilder = new PEHeaderBuilder (
	imageCharacteristics: Characteristics.Dll | Characteristics.ExecutableImage);
	
	var peBuilder = new ManagedPEBuilder (
	header: peHeaderBuilder,
	metadataRootBuilder: new MetadataRootBuilder (_metadata),
	ilStream: _ilStream,
	entryPoint: default,
	flags: CorFlags.ILOnly);
	
	var peBlob = new BlobBuilder ();
	peBuilder.Serialize (peBlob);
	
	using var fs = new FileStream (outputPath, FileMode.Create, FileAccess.Write);
	peBlob.WriteContentTo (fs);
	}
}
