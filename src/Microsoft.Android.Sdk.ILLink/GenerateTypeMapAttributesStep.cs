using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Linker;
using Mono.Linker.Steps;

namespace Microsoft.Android.Sdk.ILLink;

/// <summary>
/// Generates TypeMap attributes using the .NET 10 TypeMapAttribute and TypeMapAssociationAttribute
/// Find the best .NET type that maps to each Java type, and create the following code:
/// <code>
/// [assembly: TypeMapAttribute("java/lang/JavaClas", typeof(Java.Lang.JavaClass), typeof(Java.Lang.JavaClass))]
/// [assembly: TypeMapAssociationAttribute(typeof(Java.Lang.JavaClass), typeof(Java.Lang.JavaClassProxy))]
///
/// [TypeMapProxy("java/lang/JavaClass")]
/// class JavaClassProxy {
/// Target
/// }
/// </code>
/// </summary>
public class GenerateTypeMapAttributesStep : BaseStep
{
	const string TypeMapAttributeTypeName = "System.Runtime.InteropServices.TypeMapAttribute`1";
	MethodReference TypeMapAttributeCtor;

	const string TypeMapAssociationAttributeTypeName = "System.Runtime.InteropServices.TypeMapAssociationAttribute`1";
	MethodReference TypeMapAssociationAttributeCtor;

	const string TypeMapProxyAttributeTypeName = "Java.Interop.TypeMapProxyAttribute";
	MethodReference TypeMapProxyAttributeCtor;

	const string JavaPeerProxyTypeName = "Java.Interop.JavaPeerProxy";
	TypeReference JavaPeerProxyType { get; set; }
	MethodReference JavaPeerProxyDefaultCtor;

	const string JavaInteropAliasesAttributeTypeName = "Java.Interop.JavaInteropAliasesAttribute";
	MethodReference JavaInteropAliasesAttributeCtor;

	const string TypeMapAssemblyTargetAttributeTypeName = "System.Runtime.InteropServices.TypeMapAssemblyTargetAttribute`1";
	MethodReference TypeMapAssemblyTargetAttributeCtor;

	const string JavaTypeMapUniverseTypeName = "Java.Lang.Object";
	TypeReference JavaTypeMapUniverseType { get; set; }

	const string InvokerUniverseTypeName = "Java.Interop.InvokerUniverse";
	TypeReference InvokerUniverseType { get; set; }
	MethodReference InvokerTypeMapAssociationAttributeCtor;

	TypeReference SystemTypeType { get; set; }
	TypeReference SystemStringType { get; set; }

	AssemblyDefinition AssemblyToInjectTypeMap { get; set; }
	AssemblyDefinition MonoAndroidAssembly { get; set; }

	/// <summary>
	/// Generates the MethodReference for the given TypeMap attribute constructor,
	/// adding the necessary TypeRefs into the given assembly.
	/// </summary>
	void GetTypeMapAttributeReferences (
		string attributeTypeName,
		Func<MethodDefinition, bool> ctorSelector,
		AssemblyDefinition addReferencesTo,
		TypeReference typeMapUniverse,
		out MethodReference ctor)
	{
		var typeMapAttributeDefinition = Context.GetType (attributeTypeName);
		var attributeType = addReferencesTo.MainModule.ImportReference (typeMapAttributeDefinition.MakeGenericInstanceType (typeMapUniverse));

		var typeMapAttributeCtorDefinition = typeMapAttributeDefinition.Methods
			.FirstOrDefault (ctorSelector) ?? throw new InvalidOperationException ($"Couldn't find {attributeTypeName}..ctor()");
		var typeMapAttributeCtor = new MethodReference (
			typeMapAttributeCtorDefinition.Name,
			typeMapAttributeCtorDefinition.ReturnType,
			attributeType) {
			HasThis = typeMapAttributeCtorDefinition.HasThis,
			ExplicitThis = typeMapAttributeCtorDefinition.ExplicitThis,
			CallingConvention = typeMapAttributeCtorDefinition.CallingConvention,
		};
		foreach (var param in typeMapAttributeCtorDefinition.Parameters) {
			typeMapAttributeCtor.Parameters.Add (new ParameterDefinition (
				param.Name,
				param.Attributes,
				addReferencesTo.MainModule.ImportReference (param.ParameterType)));
		}
		ctor = addReferencesTo.MainModule.ImportReference (typeMapAttributeCtor);
	}

	protected override void Process ()
	{
		var javaTypeMapUniverseTypeDefinition = Context.GetType (JavaTypeMapUniverseTypeName);

		// AssemblyToInjectTypeMap = Context.Annotations.GetType ().GetMethod ("GetEntryPointAssembly")?.Invoke (Context.Annotations, null) as AssemblyDefinition ?? throw new NotImplementedException ("asdfasdf NoEntryPoint");
		MonoAndroidAssembly = javaTypeMapUniverseTypeDefinition.Module.Assembly;
        AssemblyToInjectTypeMap = MonoAndroidAssembly;
		JavaTypeMapUniverseType = AssemblyToInjectTypeMap.MainModule.ImportReference (javaTypeMapUniverseTypeDefinition);

		var invokerUniverseTypeDefinition = Context.GetType (InvokerUniverseTypeName);
		InvokerUniverseType = AssemblyToInjectTypeMap.MainModule.ImportReference (invokerUniverseTypeDefinition);

		GetTypeMapAttributeReferences (TypeMapAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [
				{ ParameterType.FullName: "System.String" },
				{ ParameterType.FullName: "System.Type" },
				{ ParameterType.FullName: "System.Type" }],
			AssemblyToInjectTypeMap,
			JavaTypeMapUniverseType,
			out TypeMapAttributeCtor);

		GetTypeMapAttributeReferences (TypeMapAssociationAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [
				{ ParameterType.FullName: "System.Type" },
				{ ParameterType.FullName: "System.Type" }],
			AssemblyToInjectTypeMap,
			JavaTypeMapUniverseType,
			out TypeMapAssociationAttributeCtor);

		// TypeMapAssociation<InvokerUniverse> for interface-to-invoker mappings
		GetTypeMapAttributeReferences (TypeMapAssociationAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [
				{ ParameterType.FullName: "System.Type" },
				{ ParameterType.FullName: "System.Type" }],
			AssemblyToInjectTypeMap,
			InvokerUniverseType,
			out InvokerTypeMapAssociationAttributeCtor);

		GetTypeMapAttributeReferences (TypeMapAssemblyTargetAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [{ ParameterType.FullName: "System.String" }],
			MonoAndroidAssembly,
			JavaTypeMapUniverseType,
			out TypeMapAssemblyTargetAttributeCtor);

		var typeMapProxyAttrTypeDef = Context.GetType (TypeMapProxyAttributeTypeName);
		var typeMapProxyAttribute = AssemblyToInjectTypeMap.MainModule.ImportReference (typeMapProxyAttrTypeDef);
		var typeMapProxyAttrCtor = typeMapProxyAttrTypeDef.Methods.Single (m => m.IsConstructor && !m.IsStatic);
		TypeMapProxyAttributeCtor = AssemblyToInjectTypeMap.MainModule.ImportReference (typeMapProxyAttrCtor);

		var javaPeerProxyTypeDef = Context.GetType (JavaPeerProxyTypeName);
		JavaPeerProxyType = AssemblyToInjectTypeMap.MainModule.ImportReference (javaPeerProxyTypeDef);
		var javaPeerProxyDefaultCtorDef = javaPeerProxyTypeDef.Methods.Single (m => m.IsConstructor && !m.IsStatic && !m.HasParameters);
		JavaPeerProxyDefaultCtor = AssemblyToInjectTypeMap.MainModule.ImportReference (javaPeerProxyDefaultCtorDef);

		var javaInteropAliasesAttrTypeDef = Context.GetType (JavaInteropAliasesAttributeTypeName);
		var javaInteropAliasesAttrCtor = javaInteropAliasesAttrTypeDef.Methods.Single (m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 1);
		JavaInteropAliasesAttributeCtor = AssemblyToInjectTypeMap.MainModule.ImportReference (javaInteropAliasesAttrCtor);

		SystemTypeType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.Type"));
		SystemStringType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.String"));
	}


	protected override void ProcessAssembly (AssemblyDefinition assembly)
	{
		foreach (var type in assembly.MainModule.Types) {
			ProcessType (assembly, type);
		}
	}

	// Need to find all possible mappings and pick the best before emitting
	// Maps Java name -> list of .NET types (for aliasing when multiple types share the same Java name)
	Dictionary<string, List<TypeDefinition>> externalMappings = new ();
	// Maps target type -> proxy attribute type (proxy will be applied to target type as custom attribute)
	Dictionary<TypeDefinition, TypeDefinition> proxyMappings = new ();
	// list of proxy types to inject into AssemblyToInjectInto in EndProcess
	List<TypeDefinition> typesToInject = new ();
	// Maps interfaces/abstract types to their Invoker types for TypeMapAssociation<InvokerUniverse>
	Dictionary<TypeDefinition, TypeDefinition> invokerMappings = new ();
	// Cache of all types in each module for quick lookup
	Dictionary<ModuleDefinition, Dictionary<string, TypeDefinition>> moduleTypesCache = new ();

	/// <summary>
	/// Iterates through all types to find types that map to/from java types, and stores
	/// them for modifying the assemblies during EndProcess
	/// </summary>
	private void ProcessType (AssemblyDefinition assembly, TypeDefinition type)
	{
		if (type.HasJavaPeer (Context)) {
			string javaName = JavaNativeTypeManager.ToJniName (type, Context);
			if (!externalMappings.TryGetValue (javaName, out var typeList)) {
				typeList = new List<TypeDefinition> ();
				externalMappings.Add (javaName, typeList);
			}
			typeList.Add (type);

			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Type '{type.FullName}' has peer '{javaName}'"));
			var proxyType = GenerateTypeMapProxyType (javaName, type);
			typesToInject.Add (proxyType);
			proxyMappings.Add (type, proxyType);

			// For interfaces and abstract types, find their Invoker type
			if (type.IsInterface || type.IsAbstract) {
				var invokerType = GetInvokerType (type);
				if (invokerType != null) {
					invokerMappings [type] = invokerType;
					Context.LogMessage (MessageContainer.CreateInfoMessage ($"Found invoker '{invokerType.FullName}' for type '{type.FullName}'"));
				}
			}
		} else {
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Type '{type.FullName}' has no peer"));
		}

		if (!type.HasNestedTypes)
			return;

		foreach (TypeDefinition nested in type.NestedTypes)
			ProcessType (assembly, nested);
	}

	/// <summary>
	/// Finds the Invoker type for an interface or abstract type.
	/// Follows the naming convention: IMyInterface -> IMyInterfaceInvoker, MyAbstractClass -> MyAbstractClassInvoker
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

	/// <summary>
	/// Finds a type by its full name in the given module, using a cached lookup.
	/// </summary>
	TypeDefinition? FindTypeInModule (ModuleDefinition module, string fullname)
	{
		if (!moduleTypesCache.TryGetValue (module, out var types)) {
			types = GetAllTypesInModule (module);
			moduleTypesCache [module] = types;
		}

		types.TryGetValue (fullname, out var result);
		return result;
	}

	/// <summary>
	/// Gets all types in a module, including nested types, for quick lookup.
	/// </summary>
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
			types [t.FullName] = t;
			AddNestedTypes (types, t);
		}
	}

	protected override void EndProcess ()
	{
		// HACK ALERT
		// We override the entry_assembly so that the TypeMapHandler in illink can have a starting point for TypeMapTargetAssemblies.
		// Mono.Android should be the entrypoint assembly so that we can call Assembly.SetEntryAssembly() during application initialization.
		// TODO:
		// - Add support for "EntryPointAssembly"s that don't have a .entrypoint or Main() method
		// - Use MSBuild logic to set the EntryPointAssembly to Mono.Android
		Context.Annotations.GetType ().GetField ("entry_assembly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue (Context.Annotations, MonoAndroidAssembly);
		foreach (var type in typesToInject) {
			AssemblyToInjectTypeMap.MainModule.Types.Add (type);
			Debug.Assert (type.Module.Assembly is not null);
		}

		// Generate TypeMap attributes for external mappings
		// When multiple types share the same Java name, generate an alias type
		foreach (var mapping in externalMappings) {
			var javaName = mapping.Key;
			var types = mapping.Value;

			if (types.Count == 1) {
				// Single type - simple mapping
				var attr = GenerateTypeMapAttribute (types [0], javaName);
				Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting [{attr.AttributeType.FullName}({string.Join (", ", attr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {AssemblyToInjectTypeMap.Name}"));
				AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
			} else {
				// Multiple types - generate alias type and indexed mappings
				var aliasKeys = new string [types.Count];
				for (int i = 0; i < types.Count; i++) {
					aliasKeys [i] = $"{javaName}[{i}]";

					// Generate TypeMap for each aliased type: "javaName[i]" -> type
					var attr = GenerateTypeMapAttribute (types [i], aliasKeys [i]);
					Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting aliased [{attr.AttributeType.FullName}({string.Join (", ", attr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {AssemblyToInjectTypeMap.Name}"));
					AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
				}

				// Generate the alias type with [JavaInteropAliases("javaName[0]", "javaName[1]", ...)]
				var aliasType = GenerateAliasType (javaName, aliasKeys);
				AssemblyToInjectTypeMap.MainModule.Types.Add (aliasType);

				// Generate TypeMap for the main Java name -> alias type
				var mainAttr = GenerateTypeMapAttribute (aliasType, javaName);
				Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting alias [{mainAttr.AttributeType.FullName}({string.Join (", ", mainAttr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {AssemblyToInjectTypeMap.Name}"));
				AssemblyToInjectTypeMap.CustomAttributes.Add (mainAttr);
			}
		}

		// Apply proxy attributes directly to target types (AOT-safe: uses GetCustomAttribute instead of Activator.CreateInstance)
		foreach (var mapping in proxyMappings) {
			ApplyProxyAttributeToTargetType (mapping.Key, mapping.Value);
		}
		// Generate TypeMapAssociation<InvokerUniverse> for interface-to-invoker mappings
		foreach (var mapping in invokerMappings) {
			var attr = GenerateInvokerTypeMapAssociationAttribute (mapping.Key, mapping.Value);
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting [{attr.AttributeType.FullName}({string.Join (", ", attr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {AssemblyToInjectTypeMap.Name}"));
			AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
		}

		AssemblyToInjectTypeMap.Write (Context.GetAssemblyLocation (AssemblyToInjectTypeMap) + Random.Shared.GetHexString (4) + ".injected.dll");

		// JNIEnvInit sets Mono.Android as the entrypoint assembly. Forward the typemap logic to the user/custom assembly;
		CustomAttribute targetAssembly = new (TypeMapAssemblyTargetAttributeCtor);
		targetAssembly.ConstructorArguments.Add (new (SystemStringType, AssemblyToInjectTypeMap.Name.FullName));
		MonoAndroidAssembly.CustomAttributes.Add (targetAssembly);
		MonoAndroidAssembly.Write (Path.Combine (
			Path.GetDirectoryName (Context.GetAssemblyLocation (AssemblyToInjectTypeMap)),
			"Mono.Android." + Random.Shared.GetHexString (4) + ".injected.dll"));
	}

	/// <summary>
	/// Generates an alias type with [JavaInteropAliases(...)] attribute for Java names that map to multiple .NET types.
	/// <code>
	/// [JavaInteropAliases("javaName[0]", "javaName[1]", ...)]
	/// class javaName_Aliases { }
	/// </code>
	/// </summary>
	TypeDefinition GenerateAliasType (string javaName, string[] aliasKeys)
	{
		// Create a valid C# type name from the Java name
		var typeName = javaName.Replace ('/', '_').Replace ('$', '_') + "_Aliases";

		var aliasType = new TypeDefinition (
			"Java.Interop.TypeMap._",
			typeName,
			TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed,
			AssemblyToInjectTypeMap.MainModule.TypeSystem.Object);

		// Add [JavaInteropAliases("javaName[0]", "javaName[1]", ...)]
		var stringArrayType = new ArrayType (SystemStringType);
		var attr = new CustomAttribute (JavaInteropAliasesAttributeCtor);
		attr.ConstructorArguments.Add (new CustomAttributeArgument (stringArrayType,
			aliasKeys.Select (k => new CustomAttributeArgument (SystemStringType, k)).ToArray ()));
		aliasType.CustomAttributes.Add (attr);

		return aliasType;
	}

	/// <summary>
    /// Generates <code>[TypeMapAssociation(typeof(type), typeof(proxyType))]</code>
    /// </summary>
	CustomAttribute GenerateTypeMapAssociationAttribute (TypeDefinition type, TypeDefinition proxyType)
	{
		var ca = new CustomAttribute (TypeMapAssociationAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(proxyType)));
		return ca;
	}

	/// <summary>
	/// Generates <code>[TypeMapAssociation&lt;InvokerUniverse&gt;(typeof(interfaceType), typeof(invokerType))]</code>
	/// </summary>
	CustomAttribute GenerateInvokerTypeMapAssociationAttribute (TypeDefinition interfaceType, TypeDefinition invokerType)
	{
		var ca = new CustomAttribute (InvokerTypeMapAssociationAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference (interfaceType)));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference (invokerType)));
		return ca;
	}

	/// <summary>
	/// Generates <code>[TypeMap("javaName", typeof(type), typeof(type))]</code>
	/// </summary>
	CustomAttribute GenerateTypeMapAttribute (TypeDefinition type, string javaName)
	{
		CustomAttribute ca = new (TypeMapAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemStringType, javaName));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));
		return ca;
	}

	/// <summary>
	/// Applies the proxy attribute to the target type.
	/// This enables AOT-safe lookup via <c>type.GetCustomAttribute&lt;JavaPeerProxy&gt;()</c>
	/// instead of using <c>Activator.CreateInstance(proxyType)</c>.
	/// </summary>
	void ApplyProxyAttributeToTargetType (TypeDefinition targetType, TypeDefinition proxyType)
	{
		// Import the proxy type's constructor into the target type's module
		var proxyCtorDef = proxyType.Methods.Single (m => m.IsConstructor && !m.IsStatic && !m.HasParameters);
		var proxyCtorRef = targetType.Module.ImportReference (proxyCtorDef);

		// Create the custom attribute and apply it to the target type
		var attr = new CustomAttribute (proxyCtorRef);
		targetType.CustomAttributes.Add (attr);

		Context.LogMessage (MessageContainer.CreateInfoMessage ($"Applied [{proxyType.FullName}] attribute to {targetType.FullName}"));
	}

	/// <summary>
	/// Generates a proxy attribute type that extends JavaPeerProxy with an annotated TargetType property.
	/// The proxy is an attribute that will be applied to the target type.
	/// <code>
	/// [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
	/// [TypeMapProxy("javaClassName")]
	/// sealed class AssemblyName._.mappedTypeFullName_Proxy : JavaPeerProxy
	/// {
	///     public override Type TargetType => typeof(MappedType);
	/// }
	/// </code>
	/// </summary>
	TypeDefinition GenerateTypeMapProxyType (string javaClassName, TypeDefinition mappedType)
	{
		StringBuilder mappedName = new (mappedType.Name);
		TypeDefinition? declaringType = mappedType;
		while (declaringType is not null) {
			mappedName.Insert (0, "_");
			mappedName.Insert (0, declaringType.Name);
			if (declaringType.DeclaringType is null)
				break;
			declaringType = declaringType.DeclaringType;
		}

		// Create the proxy type extending JavaPeerProxy (which extends Attribute)
		// Note: JavaPeerProxy already has [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
		var proxyType = new TypeDefinition (
			mappedType.Module.Assembly.Name.Name + "._." + declaringType.Namespace,
			mappedName.ToString () + "_Proxy",
			TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed,
			JavaPeerProxyType);

		// Add [TypeMapProxy("javaClassName")] attribute
		var ca = new CustomAttribute (TypeMapProxyAttributeCtor);
		ca.ConstructorArguments.Add (new CustomAttributeArgument (SystemStringType, javaClassName));
		proxyType.CustomAttributes.Add (ca);

		// Add default constructor that calls base()
		var ctor = new MethodDefinition (
			".ctor",
			MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			AssemblyToInjectTypeMap.MainModule.TypeSystem.Void);

		var ctorIl = ctor.Body.GetILProcessor ();
		ctorIl.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_0);
		ctorIl.Emit (Mono.Cecil.Cil.OpCodes.Call, JavaPeerProxyDefaultCtor);
		ctorIl.Emit (Mono.Cecil.Cil.OpCodes.Ret);
		proxyType.Methods.Add (ctor);

		// Add the TargetType property with [return: DynamicallyAccessedMembers] annotation
		GenerateTargetTypeProperty (proxyType, mappedType);

		return proxyType;
	}

	/// <summary>
	/// Generates the TargetType property:
	/// <code>
	/// [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
	/// public override Type TargetType => typeof(MappedType);
	/// </code>
	/// </summary>
	void GenerateTargetTypeProperty (TypeDefinition proxyType, TypeDefinition mappedType)
	{
		// Create the getter method
		var getter = new MethodDefinition (
			"get_TargetType",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
			SystemTypeType);

		// Add [return: DynamicallyAccessedMembers(PublicConstructors | NonPublicConstructors)]
		var damAttrTypeDef = Context.GetType ("System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute");
		var damAttrCtor = damAttrTypeDef.Methods.Single (m => m.IsConstructor && !m.IsStatic && m.HasParameters);
		var damAttrCtorRef = AssemblyToInjectTypeMap.MainModule.ImportReference (damAttrCtor);

		var damtEnumTypeDef = Context.GetType ("System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes");
		var damtEnumTypeRef = AssemblyToInjectTypeMap.MainModule.ImportReference (damtEnumTypeDef);

		// PublicConstructors = 1, NonPublicConstructors = 4
		const int Constructors = 1 | 4;

		var returnAttr = new CustomAttribute (damAttrCtorRef);
		returnAttr.ConstructorArguments.Add (new CustomAttributeArgument (damtEnumTypeRef, Constructors));
		getter.MethodReturnType.CustomAttributes.Add (returnAttr);

		// Generate: return typeof(MappedType);
		var il = getter.Body.GetILProcessor ();
		var mappedTypeRef = AssemblyToInjectTypeMap.MainModule.ImportReference (mappedType);
		il.Emit (Mono.Cecil.Cil.OpCodes.Ldtoken, mappedTypeRef);

		var getTypeFromHandleMethod = Context.GetType ("System.Type").Methods
			.Single (m => m.Name == "GetTypeFromHandle" && m.IsStatic && m.Parameters.Count == 1);
		var getTypeFromHandleRef = AssemblyToInjectTypeMap.MainModule.ImportReference (getTypeFromHandleMethod);
		il.Emit (Mono.Cecil.Cil.OpCodes.Call, getTypeFromHandleRef);
		il.Emit (Mono.Cecil.Cil.OpCodes.Ret);

		proxyType.Methods.Add (getter);

		// Create the property
		var property = new PropertyDefinition ("TargetType", PropertyAttributes.None, SystemTypeType);
		property.GetMethod = getter;
		proxyType.Properties.Add (property);
	}
}
