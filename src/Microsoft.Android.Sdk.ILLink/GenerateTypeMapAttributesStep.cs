using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Android.Runtime;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers.Utilities;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Linker;
using Mono.Linker.Steps;

namespace Microsoft.Android.Sdk.ILLink;

/// <summary>
/// Reads TypeMap information from the pre-generated _Microsoft.Android.TypeMaps assembly
/// and generates JCW .java files and LLVM IR .ll files for marshal methods.
/// 
/// This step does NOT modify any assemblies - the TypeMap assembly is generated BEFORE ILLink
/// by the GenerateTypeMapAssembly MSBuild task. This step only reads the attribute data and
/// generates build artifacts (JCW and LLVM IR).
/// 
/// This approach avoids ILLink crashes that were caused by modifying assemblies during the linker step.
/// </summary>
public class GenerateTypeMapAttributesStep : BaseStep
{
	// Custom data from MSBuild targets
	string? JavaOutputPath { get; set; }
	string? LlvmIrOutputPath { get; set; }
	string? TargetArch { get; set; }
	string? TypeMapEntryAssembly { get; set; }

	// Collected type information for JCW/LLVM IR generation
	List<TypeMapEntry> TypeMapEntries { get; } = new ();
	List<TypeMapInvokerEntry> InvokerEntries { get; } = new ();

	protected override void Process ()
	{
		try {
			// Get custom data from MSBuild targets
			Context.TryGetCustomData ("JavaOutputPath", out string? javaOutputPath);
			Context.TryGetCustomData ("LlvmIrOutputPath", out string? llvmIrOutputPath);
			Context.TryGetCustomData ("TargetArch", out string? targetArch);
			Context.TryGetCustomData ("TypeMapEntryAssembly", out string? typeMapEntryAssembly);

			JavaOutputPath = javaOutputPath ?? "";
			LlvmIrOutputPath = llvmIrOutputPath ?? "";
			TargetArch = targetArch ?? "";
			TypeMapEntryAssembly = typeMapEntryAssembly ?? "_Microsoft.Android.TypeMaps";

			Context.LogMessage (MessageContainer.CreateInfoMessage (
				$"GenerateTypeMapAttributesStep: TypeMapEntryAssembly={TypeMapEntryAssembly}, JavaOutputPath={JavaOutputPath}, LlvmIrOutputPath={LlvmIrOutputPath}"));

			// Process assemblies to find Java peer types
			// We scan the actual assemblies rather than reading TypeMap attributes because
			// we need detailed type information (methods, constructors, etc.) for JCW/LLVM IR generation
			base.Process ();

		} catch (Exception ex) {
			throw new InvalidOperationException ($"GenerateTypeMapAttributesStep.Process crashed: {ex}");
		}
	}

	protected override void ProcessAssembly (AssemblyDefinition assembly)
	{
		foreach (var type in assembly.MainModule.Types) {
			ProcessType (assembly, type);
		}
	}

	/// <summary>
	/// Iterates through all types to find types that map to/from Java types,
	/// and collects them for JCW/LLVM IR generation.
	/// </summary>
	void ProcessType (AssemblyDefinition assembly, TypeDefinition type)
	{
		if (type.HasJavaPeer (Context)) {
			string javaName = JavaNativeTypeManager.ToJniName (type, Context);
			
			// Skip invalid Java names (array type signatures like [I, [Ljava/lang/Object;)
			if (string.IsNullOrEmpty (javaName) || javaName.StartsWith ("[", StringComparison.Ordinal)) {
				if (!type.HasNestedTypes)
					return;
				foreach (TypeDefinition nested in type.NestedTypes)
					ProcessType (assembly, nested);
				return;
			}

			// Skip types with invalid characters in their Java names
			if (javaName.Contains (';') || javaName.Contains ('[')) {
				if (!type.HasNestedTypes)
					return;
				foreach (TypeDefinition nested in type.NestedTypes)
					ProcessType (assembly, nested);
				return;
			}

			var entry = new TypeMapEntry {
				JavaName = javaName,
				Type = type,
				Assembly = assembly,
				IsInterface = type.IsInterface,
				IsAbstract = type.IsAbstract,
				IsImplementorType = javaName.StartsWith ("mono/android/", StringComparison.Ordinal) && 
				                    type.Name.EndsWith ("Implementor", StringComparison.Ordinal),
			};

			// Get base type info
			if (type.BaseType != null) {
				var baseType = type.BaseType.Resolve ();
				if (baseType != null) {
					entry.BaseType = baseType;
					entry.BaseTypeJavaName = JavaNativeTypeManager.ToJniName (baseType, Context);
				}
			}

			// Collect marshal methods
			CollectMarshalMethods (type, entry);

			// Find activation constructor info
			CollectActivationConstructorInfo (type, entry);

			TypeMapEntries.Add (entry);

			// For interfaces and abstract types, find their Invoker type
			if (type.IsInterface || type.IsAbstract) {
				var invokerType = GetInvokerType (type);
				if (invokerType != null) {
					InvokerEntries.Add (new TypeMapInvokerEntry {
						InterfaceType = type,
						InvokerType = invokerType,
					});
				}
			}
		}

		if (!type.HasNestedTypes)
			return;

		foreach (TypeDefinition nested in type.NestedTypes)
			ProcessType (assembly, nested);
	}

	/// <summary>
	/// Collects marshal methods from a type that have [Register] attributes with connector methods.
	/// </summary>
	void CollectMarshalMethods (TypeDefinition type, TypeMapEntry entry)
	{
		var seen = new HashSet<string> ();

		foreach (var method in type.Methods) {
			if (!CecilExtensions.HasMethodRegistrationAttributes (method)) {
				continue;
			}

			foreach (var attr in CecilExtensions.GetMethodRegistrationAttributes (method)) {
				if (string.IsNullOrEmpty (attr.Name) || string.IsNullOrEmpty (attr.Signature)) {
					continue;
				}

				string key = $"{attr.Name}{attr.Signature}";
				if (seen.Contains (key)) {
					continue;
				}

				// Find the native callback method
				string? nativeCallbackName = GetNativeCallbackName (attr.Connector, attr.Name, attr.Signature);
				MethodDefinition? nativeCallback = nativeCallbackName != null
					? type.Methods.FirstOrDefault (m => m.Name == nativeCallbackName && m.IsStatic)
					: null;

				if (nativeCallback == null) {
					continue;
				}

				seen.Add (key);
				entry.MarshalMethods.Add (new TypeMapMarshalMethod {
					JniName = attr.Name,
					JniSignature = attr.Signature,
					NativeCallback = nativeCallback,
					DeclaringType = type,
					IsConstructor = false,
				});
			}
		}

		// Collect exported constructors
		foreach (var ctor in type.Methods.Where (m => m.IsConstructor && !m.IsStatic)) {
			foreach (var attr in CecilExtensions.GetMethodRegistrationAttributes (ctor)) {
				if (string.IsNullOrEmpty (attr.Signature)) {
					continue;
				}

				string key = $"<init>{attr.Signature}";
				if (seen.Contains (key)) {
					continue;
				}

				var nativeCallback = type.Methods.FirstOrDefault (m =>
					m.IsStatic && (m.Name == "n_<init>" || m.Name.StartsWith ("n_", StringComparison.Ordinal)));

				if (nativeCallback != null) {
					seen.Add (key);
					entry.MarshalMethods.Add (new TypeMapMarshalMethod {
						JniName = "<init>",
						JniSignature = attr.Signature,
						NativeCallback = nativeCallback,
						DeclaringType = type,
						IsConstructor = true,
					});
				}
			}
		}

		// For Implementor types, collect methods from interfaces
		if (type.HasInterfaces && entry.IsImplementorType) {
			CollectInterfaceMarshalMethods (type, entry, seen);
		}
	}

	/// <summary>
	/// Collects marshal methods from implemented interfaces for Implementor types.
	/// </summary>
	void CollectInterfaceMarshalMethods (TypeDefinition type, TypeMapEntry entry, HashSet<string> seen)
	{
		foreach (var iface in type.Interfaces) {
			var ifaceType = iface.InterfaceType.Resolve ();
			if (ifaceType == null) {
				continue;
			}

			foreach (var method in ifaceType.Methods) {
				if (!CecilExtensions.HasMethodRegistrationAttributes (method)) {
					continue;
				}

				foreach (var attr in CecilExtensions.GetMethodRegistrationAttributes (method)) {
					if (string.IsNullOrEmpty (attr.Name) || string.IsNullOrEmpty (attr.Signature) || string.IsNullOrEmpty (attr.Connector)) {
						continue;
					}

					string key = $"{attr.Name}{attr.Signature}";
					if (seen.Contains (key)) {
						continue;
					}

					// Parse connector to find the Invoker type
					var invokerInfo = ParseConnectorToInvokerType (attr.Connector, type.Module);
					if (invokerInfo == null) {
						continue;
					}

					var (invokerType, handlerMethodName) = invokerInfo.Value;

					// Find the native callback in the Invoker type
					string? nativeCallbackName = GetNativeCallbackNameFromHandler (handlerMethodName, attr.Name, attr.Signature);
					MethodDefinition? nativeCallback = nativeCallbackName != null
						? invokerType.Methods.FirstOrDefault (m => m.Name == nativeCallbackName && m.IsStatic)
						: null;

					if (nativeCallback == null) {
						nativeCallback = invokerType.Methods.FirstOrDefault (m =>
							m.IsStatic && m.Name.StartsWith ("n_", StringComparison.Ordinal) &&
							m.Name.Contains (attr.Name, StringComparison.OrdinalIgnoreCase));
					}

					if (nativeCallback == null) {
						continue;
					}

					seen.Add (key);
					entry.MarshalMethods.Add (new TypeMapMarshalMethod {
						JniName = attr.Name,
						JniSignature = attr.Signature,
						NativeCallback = nativeCallback,
						DeclaringType = invokerType,
						IsConstructor = false,
					});
				}
			}
		}
	}

	/// <summary>
	/// Collects information about the activation constructor for a type.
	/// </summary>
	void CollectActivationConstructorInfo (TypeDefinition type, TypeMapEntry entry)
	{
		if (type.IsInterface || (type.IsAbstract && type.IsSealed)) {
			return; // Interfaces and static classes can't be activated
		}

		// Find activation constructor by walking up the type hierarchy
		TypeDefinition? current = type;
		while (current != null) {
			// XI-style: (IntPtr, JniHandleOwnership)
			var xiCtor = current.Methods.FirstOrDefault (m =>
				m.IsConstructor && !m.IsStatic &&
				m.Parameters.Count == 2 &&
				m.Parameters[0].ParameterType.FullName == "System.IntPtr" &&
				m.Parameters[1].ParameterType.FullName == "Android.Runtime.JniHandleOwnership");

			if (xiCtor != null) {
				entry.ActivationConstructor = xiCtor;
				entry.ActivationCtorKind = "XA";
				entry.ActivationCtorIsOnBaseType = current != type;
				return;
			}

			// JI-style: (ref JniObjectReference, JniObjectReferenceOptions)
			var jiCtor = current.Methods.FirstOrDefault (m =>
				m.IsConstructor && !m.IsStatic &&
				m.Parameters.Count == 2 &&
				m.Parameters[0].ParameterType.IsByReference &&
				m.Parameters[0].ParameterType.GetElementType ().FullName == "Java.Interop.JniObjectReference" &&
				m.Parameters[1].ParameterType.FullName == "Java.Interop.JniObjectReferenceOptions");

			if (jiCtor != null) {
				entry.ActivationConstructor = jiCtor;
				entry.ActivationCtorKind = "JI";
				entry.ActivationCtorIsOnBaseType = current != type;
				return;
			}

			// Move to base type
			if (current.BaseType == null) {
				break;
			}
			current = current.BaseType.Resolve ();
		}

		// No activation constructor found
		entry.ActivationCtorKind = "None";
	}

	/// <summary>
	/// Finds the Invoker type for an interface or abstract type.
	/// </summary>
	TypeDefinition? GetInvokerType (TypeDefinition type)
	{
		const string suffix = "Invoker";
		string fullname = type.FullName;

		if (type.HasGenericParameters) {
			var pos = fullname.IndexOf ('`');
			if (pos == -1)
				return null;
			fullname = fullname.Substring (0, pos) + suffix + fullname.Substring (pos);
		} else {
			fullname = fullname + suffix;
		}

		return FindTypeInModule (type.Module, fullname);
	}

	// Cache for type lookups
	Dictionary<ModuleDefinition, Dictionary<string, TypeDefinition>> moduleTypesCache = new ();

	TypeDefinition? FindTypeInModule (ModuleDefinition module, string fullname)
	{
		if (!moduleTypesCache.TryGetValue (module, out var types)) {
			types = GetAllTypesInModule (module);
			moduleTypesCache[module] = types;
		}

		types.TryGetValue (fullname, out var result);
		return result;
	}

	static Dictionary<string, TypeDefinition> GetAllTypesInModule (ModuleDefinition module)
	{
		var types = module.Types.ToDictionary (p => p.FullName);
		foreach (var t in module.Types)
			AddNestedTypes (types, t);
		return types;
	}

	static void AddNestedTypes (Dictionary<string, TypeDefinition> types, TypeDefinition type)
	{
		if (!type.HasNestedTypes)
			return;
		foreach (var t in type.NestedTypes) {
			types[t.FullName] = t;
			AddNestedTypes (types, t);
		}
	}

	/// <summary>
	/// Parses a connector string to find the Invoker type and handler method name.
	/// </summary>
	(TypeDefinition invokerType, string handlerMethodName)? ParseConnectorToInvokerType (string connector, ModuleDefinition module)
	{
		int colonIdx = connector.IndexOf (':');
		if (colonIdx < 0) {
			return null;
		}

		string handlerMethodName = connector.Substring (0, colonIdx);
		string typeInfo = connector.Substring (colonIdx + 1);

		int commaIdx = typeInfo.IndexOf (',');
		if (commaIdx < 0) {
			return null;
		}

		string typeName = typeInfo.Substring (0, commaIdx).Trim ();
		string assemblyName = typeInfo.Substring (commaIdx + 1).Split (',')[0].Trim ();

		// Find the assembly
		AssemblyDefinition? assembly = null;
		foreach (var asmRef in module.AssemblyReferences) {
			if (asmRef.Name == assemblyName) {
				try {
					assembly = Context.Resolve (asmRef);
				} catch {
					continue;
				}
				break;
			}
		}

		if (assembly == null && module.Assembly.Name.Name == assemblyName) {
			assembly = module.Assembly;
		}

		if (assembly == null) {
			return null;
		}

		// Find the type
		TypeDefinition? invokerType = null;
		foreach (var mod in assembly.Modules) {
			invokerType = FindTypeInModule (mod, typeName);
			if (invokerType != null) {
				break;
			}
		}

		if (invokerType == null) {
			return null;
		}

		return (invokerType, handlerMethodName);
	}

	static string? GetNativeCallbackName (string? connector, string jniName, string jniSignature)
	{
		if (string.IsNullOrEmpty (connector)) {
			return null;
		}

		if (connector!.StartsWith ("Get", StringComparison.Ordinal) && connector.EndsWith ("Handler", StringComparison.Ordinal)) {
			return $"n_{jniName}";
		}

		return $"n_{jniName}";
	}

	static string? GetNativeCallbackNameFromHandler (string handlerMethodName, string jniName, string jniSignature)
	{
		if (handlerMethodName.StartsWith ("Get", StringComparison.Ordinal) && handlerMethodName.EndsWith ("Handler", StringComparison.Ordinal)) {
			string middle = handlerMethodName.Substring (3, handlerMethodName.Length - 3 - 7);
			return $"n_{middle}";
		}
		return null;
	}

	protected override void EndProcess ()
	{
		try {
			Context.LogMessage (MessageContainer.CreateInfoMessage (
				$"GenerateTypeMapAttributesStep: Found {TypeMapEntries.Count} Java peer types, {InvokerEntries.Count} invoker mappings"));

			// NOTE: JCW files are NOT generated here - the existing GenerateJavaStubs task handles this correctly.
			// We only generate LLVM IR files for marshal methods (for NativeAOT runtime).

			// Generate LLVM IR files if output path is specified
			if (!string.IsNullOrEmpty (LlvmIrOutputPath) && !string.IsNullOrEmpty (TargetArch)) {
				GenerateLlvmIrFiles ();
			}

		} catch (Exception ex) {
			throw new InvalidOperationException ($"GenerateTypeMapAttributesStep.EndProcess crashed: {ex}");
		}
	}

	void GenerateJcwFiles ()
	{
		Directory.CreateDirectory (JavaOutputPath!);
		int count = 0;

		foreach (var entry in TypeMapEntries) {
			// Skip interfaces and abstract types (they don't need JCW)
			if (entry.IsInterface) {
				continue;
			}

			// Skip types without any marshal methods
			if (entry.MarshalMethods.Count == 0 && entry.ActivationCtorKind == "None") {
				continue;
			}

			try {
				var jcw = GenerateJcwForType (entry);
				if (jcw != null) {
					string javaPath = Path.Combine (JavaOutputPath!, entry.JavaName.Replace ('/', Path.DirectorySeparatorChar) + ".java");
					Directory.CreateDirectory (Path.GetDirectoryName (javaPath)!);
					File.WriteAllText (javaPath, jcw);
					count++;
				}
			} catch (Exception ex) {
				Context.LogMessage (MessageContainer.CreateInfoMessage (
					$"Warning: Failed to generate JCW for {entry.Type.FullName}: {ex.Message}"));
			}
		}

		Context.LogMessage (MessageContainer.CreateInfoMessage ($"Generated {count} JCW files to {JavaOutputPath}"));
	}

	string? GenerateJcwForType (TypeMapEntry entry)
	{
		// Parse the Java name to get package and class name
		string javaName = entry.JavaName;
		int lastSlash = javaName.LastIndexOf ('/');
		string packageName = lastSlash >= 0 ? javaName.Substring (0, lastSlash).Replace ('/', '.') : "";
		string className = lastSlash >= 0 ? javaName.Substring (lastSlash + 1) : javaName;
		
		// Handle nested classes ($ separator in Java)
		className = className.Replace ('$', '_');

		var sb = new StringBuilder ();

		// Package declaration
		if (!string.IsNullOrEmpty (packageName)) {
			sb.AppendLine ($"package {packageName};");
			sb.AppendLine ();
		}

		// Determine base class
		string baseClass = "java.lang.Object";
		if (entry.BaseType != null && !string.IsNullOrEmpty (entry.BaseTypeJavaName)) {
			baseClass = entry.BaseTypeJavaName.Replace ('/', '.').Replace ('$', '.');
		}

		// Class declaration
		string classModifier = entry.IsAbstract ? "abstract " : "";
		sb.AppendLine ($"public {classModifier}class {className} extends {baseClass} {{");
		sb.AppendLine ();

		// Static initializer to ensure native library is loaded
		sb.AppendLine ("\tstatic {");
		sb.AppendLine ("\t\tmono.android.Runtime.register();");
		sb.AppendLine ("\t}");
		sb.AppendLine ();

		// Generate native method declarations for marshal methods
		foreach (var mm in entry.MarshalMethods) {
			if (mm.IsConstructor) {
				continue; // Constructors are handled separately
			}
			
			// Generate native method declaration
			string nativeMethodName = $"n_{mm.JniName}";
			string returnType = JniSignatureToJavaType (GetReturnTypeFromSignature (mm.JniSignature));
			var paramTypes = GetParameterTypesFromSignature (mm.JniSignature);
			
			sb.Append ($"\tprivate static native {returnType} {nativeMethodName}(");
			for (int i = 0; i < paramTypes.Count; i++) {
				if (i > 0) sb.Append (", ");
				sb.Append ($"{JniSignatureToJavaType (paramTypes[i])} p{i}");
			}
			sb.AppendLine (");");
		}

		if (entry.MarshalMethods.Count > 0) {
			sb.AppendLine ();
		}

		// Generate wrapper methods that call native methods
		foreach (var mm in entry.MarshalMethods) {
			if (mm.IsConstructor) {
				continue;
			}

			string returnType = JniSignatureToJavaType (GetReturnTypeFromSignature (mm.JniSignature));
			var paramTypes = GetParameterTypesFromSignature (mm.JniSignature);
			bool hasReturn = returnType != "void";

			sb.Append ($"\tpublic {returnType} {mm.JniName}(");
			for (int i = 0; i < paramTypes.Count; i++) {
				if (i > 0) sb.Append (", ");
				sb.Append ($"{JniSignatureToJavaType (paramTypes[i])} p{i}");
			}
			sb.AppendLine (") {");
			
			sb.Append ("\t\t");
			if (hasReturn) sb.Append ("return ");
			sb.Append ($"n_{mm.JniName}(");
			for (int i = 0; i < paramTypes.Count; i++) {
				if (i > 0) sb.Append (", ");
				sb.Append ($"p{i}");
			}
			sb.AppendLine (");");
			sb.AppendLine ("\t}");
			sb.AppendLine ();
		}

		sb.AppendLine ("}");

		return sb.ToString ();
	}

	static string GetReturnTypeFromSignature (string jniSignature)
	{
		int parenClose = jniSignature.IndexOf (')');
		if (parenClose < 0 || parenClose + 1 >= jniSignature.Length) {
			return "V";
		}
		return jniSignature.Substring (parenClose + 1);
	}

	static List<string> GetParameterTypesFromSignature (string jniSignature)
	{
		var result = new List<string> ();
		int parenOpen = jniSignature.IndexOf ('(');
		int parenClose = jniSignature.IndexOf (')');
		if (parenOpen < 0 || parenClose < 0) {
			return result;
		}

		int i = parenOpen + 1;
		while (i < parenClose) {
			char c = jniSignature[i];
			if (c == '[') {
				// Array type
				int start = i;
				i++;
				while (i < parenClose && jniSignature[i] == '[') i++;
				if (i < parenClose) {
					if (jniSignature[i] == 'L') {
						int semi = jniSignature.IndexOf (';', i);
						if (semi > 0) {
							result.Add (jniSignature.Substring (start, semi - start + 1));
							i = semi + 1;
						} else {
							break;
						}
					} else {
						result.Add (jniSignature.Substring (start, i - start + 1));
						i++;
					}
				}
			} else if (c == 'L') {
				int semi = jniSignature.IndexOf (';', i);
				if (semi > 0) {
					result.Add (jniSignature.Substring (i, semi - i + 1));
					i = semi + 1;
				} else {
					break;
				}
			} else {
				result.Add (c.ToString ());
				i++;
			}
		}
		return result;
	}

	static string JniSignatureToJavaType (string jniType)
	{
		if (string.IsNullOrEmpty (jniType)) return "void";
		
		char first = jniType[0];
		return first switch {
			'V' => "void",
			'Z' => "boolean",
			'B' => "byte",
			'C' => "char",
			'S' => "short",
			'I' => "int",
			'J' => "long",
			'F' => "float",
			'D' => "double",
			'[' => JniSignatureToJavaType (jniType.Substring (1)) + "[]",
			'L' => jniType.Substring (1, jniType.Length - 2).Replace ('/', '.').Replace ('$', '.'),
			_ => "Object"
		};
	}

	void GenerateLlvmIrFiles ()
	{
		Directory.CreateDirectory (LlvmIrOutputPath!);
		
		// Map RuntimeIdentifier to target triple
		string targetTriple = TargetArch switch {
			"android-arm64" => "aarch64-linux-android",
			"android-arm" => "armv7-linux-androideabi",
			"android-x64" => "x86_64-linux-android",
			"android-x86" => "i686-linux-android",
			_ => "aarch64-linux-android"
		};

		var sb = new StringBuilder ();
		sb.AppendLine ($"; ModuleID = 'marshal_methods_typemap'");
		sb.AppendLine ($"target triple = \"{targetTriple}\"");
		sb.AppendLine ();

		// Define the global function pointer for the resolver (per spec section 8.1)
		// This is a global function pointer that managed code sets during initialization via host.cc
		// Must be defined here (not external) because it's set by the runtime and called by JNI stubs
		sb.AppendLine ("; Global function pointer for typemap resolver - set by managed code at runtime");
		sb.AppendLine ("@typemap_get_function_pointer = dso_local global ptr null, align 8");
		sb.AppendLine ();

		// Generate function pointer cache globals for each marshal method
		// Also collect all entries to generate class name constants
		var allMethods = new List<(TypeMapEntry entry, TypeMapMarshalMethod mm, string funcName, int methodIndex)> ();
		var classNameGlobals = new Dictionary<string, string> (); // javaName -> global name
		int methodCount = 0;
		var generatedFuncNames = new HashSet<string> ();

		// First pass: collect all methods and assign method indices per type
		var typeMethodIndices = new Dictionary<string, int> (); // javaName -> next method index
		foreach (var entry in TypeMapEntries) {
			if (!typeMethodIndices.ContainsKey (entry.JavaName)) {
				typeMethodIndices[entry.JavaName] = 0;
			}
			
			foreach (var mm in entry.MarshalMethods) {
				// Skip constructors for LLVM IR generation - they're handled differently
				if (mm.IsConstructor || mm.JniName == "<init>") {
					continue;
				}
				
				string javaClassName = entry.JavaName.Replace ('/', '_').Replace ('$', '_').Replace ('-', '_');
				string methodName = mm.JniName.Replace ('<', '_').Replace ('>', '_').Replace ('-', '_');
				string baseFuncName = $"Java_{javaClassName}_{methodName}";
				
				string funcName = baseFuncName;
				int suffix = 0;
				while (!generatedFuncNames.Add (funcName)) {
					suffix++;
					funcName = $"{baseFuncName}_{suffix}";
				}
				
				int methodIndex = typeMethodIndices[entry.JavaName]++;
				allMethods.Add ((entry, mm, funcName, methodIndex));
			}
		}

		// Generate class name constant globals (UTF-16 encoded per spec section 8.2)
		int globalIndex = 0;
		foreach (var entry in TypeMapEntries) {
			if (classNameGlobals.ContainsKey (entry.JavaName)) {
				continue;
			}
			
			string globalName = $"@class_name_{globalIndex++}";
			classNameGlobals[entry.JavaName] = globalName;
			
			// Generate UTF-16 encoded class name (no null terminator per spec)
			string className = entry.JavaName;
			sb.Append ($"; Class name: \"{className}\" ({className.Length} chars)");
			sb.AppendLine ();
			sb.Append ($"{globalName} = internal constant [{className.Length * 2} x i8] c\"");
			foreach (char c in className) {
				// UTF-16 LE encoding
				sb.Append ($"\\{(byte)c:X2}\\{(byte)(c >> 8):X2}");
			}
			sb.AppendLine ("\", align 2");
		}
		sb.AppendLine ();

		// Generate function pointer cache globals
		sb.AppendLine ("; Function pointer caches (one per marshal method)");
		for (int i = 0; i < allMethods.Count; i++) {
			sb.AppendLine ($"@fn_ptr_{i} = internal unnamed_addr global ptr null, align 8");
		}
		sb.AppendLine ();

		// Generate the actual JNI stubs
		for (int i = 0; i < allMethods.Count; i++) {
			var (entry, mm, funcName, methodIndex) = allMethods[i];
			GenerateMarshalMethodLlvmIrWithCaching (sb, entry, mm, funcName, methodIndex, i, classNameGlobals[entry.JavaName]);
			methodCount++;
		}

		// Add attributes for the generated functions
		sb.AppendLine ();
		sb.AppendLine ("attributes #0 = { noinline nounwind \"frame-pointer\"=\"non-leaf\" }");

		string outputPath = Path.Combine (LlvmIrOutputPath!, "marshal_methods_typemap.ll");
		File.WriteAllText (outputPath, sb.ToString ());

		Context.LogMessage (MessageContainer.CreateInfoMessage (
			$"Generated LLVM IR with {methodCount} marshal methods to {outputPath}"));
	}

	void GenerateMarshalMethodLlvmIrWithCaching (StringBuilder sb, TypeMapEntry entry, TypeMapMarshalMethod mm, 
		string funcName, int methodIndex, int globalCacheIndex, string classNameGlobal)
	{
		// Get parameter types from JNI signature
		var paramTypes = GetParameterTypesFromSignature (mm.JniSignature);
		string returnType = GetLlvmReturnTypeFromSignature (mm.JniSignature);
		
		// Generate function signature
		// All JNI functions take JNIEnv* as first arg and jobject/jclass as second
		sb.Append ($"define default {returnType} @{funcName}(ptr %env, ptr %obj");
		for (int i = 0; i < paramTypes.Count; i++) {
			sb.Append ($", ptr %p{i}");
		}
		sb.AppendLine (") #0 {");
		
		sb.AppendLine ("entry:");
		
		// Load cached function pointer
		sb.AppendLine ($"  %cached_ptr = load ptr, ptr @fn_ptr_{globalCacheIndex}, align 8");
		sb.AppendLine ($"  %is_null = icmp eq ptr %cached_ptr, null");
		sb.AppendLine ($"  br i1 %is_null, label %resolve, label %call");
		sb.AppendLine ();
		
		// Resolve block: call typemap_get_function_pointer
		sb.AppendLine ("resolve:");
		sb.AppendLine ($"  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8");
		// Call signature: void fn(char16_t* className, int32_t length, int32_t methodIndex, intptr_t* fnptr)
		int classNameLength = entry.JavaName.Length;
		sb.AppendLine ($"  call void %get_fn(ptr {classNameGlobal}, i32 {classNameLength}, i32 {methodIndex}, ptr @fn_ptr_{globalCacheIndex})");
		sb.AppendLine ($"  %resolved_ptr = load ptr, ptr @fn_ptr_{globalCacheIndex}, align 8");
		sb.AppendLine ($"  br label %call");
		sb.AppendLine ();
		
		// Call block: invoke the managed function
		sb.AppendLine ("call:");
		sb.AppendLine ($"  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]");
		
		// Build the call to the resolved function
		sb.Append ($"  ");
		if (returnType != "void") {
			sb.Append ($"%result = ");
		}
		sb.Append ($"tail call {returnType} %fn(ptr %env, ptr %obj");
		for (int i = 0; i < paramTypes.Count; i++) {
			sb.Append ($", ptr %p{i}");
		}
		sb.AppendLine (")");
		
		// Return
		if (returnType == "void") {
			sb.AppendLine ($"  ret void");
		} else {
			sb.AppendLine ($"  ret {returnType} %result");
		}
		sb.AppendLine ("}");
		sb.AppendLine ();
	}

	string GetLlvmReturnTypeFromSignature (string jniSignature)
	{
		// JNI signature format: (params)return
		// Find the return type after the closing paren
		int parenClose = jniSignature.LastIndexOf (')');
		if (parenClose < 0 || parenClose >= jniSignature.Length - 1) {
			return "void";
		}
		
		char returnTypeChar = jniSignature[parenClose + 1];
		return returnTypeChar switch {
			'V' => "void",
			'Z' => "i8",      // boolean
			'B' => "i8",      // byte
			'C' => "i16",     // char
			'S' => "i16",     // short
			'I' => "i32",     // int
			'J' => "i64",     // long
			'F' => "float",   // float
			'D' => "double",  // double
			'L' => "ptr",     // object reference
			'[' => "ptr",     // array reference
			_ => "ptr"        // default to ptr for unknown
		};
	}
}

#region Data Classes

/// <summary>
/// Represents a type mapping entry collected during processing.
/// </summary>
class TypeMapEntry
{
	public string JavaName { get; set; } = "";
	public TypeDefinition Type { get; set; } = null!;
	public AssemblyDefinition Assembly { get; set; } = null!;
	public bool IsInterface { get; set; }
	public bool IsAbstract { get; set; }
	public bool IsImplementorType { get; set; }
	public TypeDefinition? BaseType { get; set; }
	public string? BaseTypeJavaName { get; set; }
	public List<TypeMapMarshalMethod> MarshalMethods { get; set; } = new ();
	public MethodDefinition? ActivationConstructor { get; set; }
	public string ActivationCtorKind { get; set; } = "None";
	public bool ActivationCtorIsOnBaseType { get; set; }
}

/// <summary>
/// Represents a marshal method that bridges Java calls to .NET.
/// </summary>
class TypeMapMarshalMethod
{
	public string JniName { get; set; } = "";
	public string JniSignature { get; set; } = "";
	public MethodDefinition NativeCallback { get; set; } = null!;
	public TypeDefinition DeclaringType { get; set; } = null!;
	public bool IsConstructor { get; set; }
}

/// <summary>
/// Represents an interface-to-invoker mapping.
/// </summary>
class TypeMapInvokerEntry
{
	public TypeDefinition InterfaceType { get; set; } = null!;
	public TypeDefinition InvokerType { get; set; } = null!;
}

#endregion
