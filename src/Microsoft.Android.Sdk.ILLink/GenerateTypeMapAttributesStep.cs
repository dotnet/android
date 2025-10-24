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
/// Generate TypeMap attributes using the .NET 10 TypeMapAttribute and TypeMapAssociationAttribute
/// </summary>
public class GenerateTypeMapAttributesStep : BaseStep
{
	const string TypeMapAttributeTypeName = "System.Runtime.InteropServices.TypeMapAttribute`1";
	MethodReference TypeMapAttributeCtor;

	const string TypeMapAssociationAttributeTypeName = "System.Runtime.InteropServices.TypeMapAssociationAttribute`1";
	MethodReference TypeMapAssociationAttributeCtor;

	const string TypeMapProxyAttributeTypeName = "Java.Interop.TypeMapProxyAttribute";
	//TypeReference TypeMapProxyAttribute;
	MethodReference TypeMapProxyAttributeCtor;

	const string TypeMapAssemblyTargetAttributeTypeName = "System.Runtime.InteropServices.TypeMapAssemblyTargetAttribute`1";
	MethodReference TypeMapAssemblyTargetAttributeCtor;

	const string JavaTypeMapUniverseTypeName = "Java.Lang.Object";
	TypeReference JavaTypeMapUniverseType { get; set; }

	TypeReference SystemTypeType { get; set; }
	TypeReference SystemStringType { get; set; }

	AssemblyDefinition AssemblyToInjectTypeMap { get; set; }
	AssemblyDefinition MonoAndroidAssembly { get; set; }

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
		AssemblyToInjectTypeMap = Context.Annotations.GetType ().GetMethod ("GetEntryPointAssembly")?.Invoke (Context.Annotations, null) as AssemblyDefinition ?? throw new NotImplementedException ("asdfasdf NoEntryPoint");
		var javaTypeMapUniverseTypeDefinition = Context.GetType (JavaTypeMapUniverseTypeName);
		JavaTypeMapUniverseType = AssemblyToInjectTypeMap.MainModule.ImportReference (javaTypeMapUniverseTypeDefinition);

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

		MonoAndroidAssembly = javaTypeMapUniverseTypeDefinition.Module.Assembly;
		GetTypeMapAttributeReferences (TypeMapAssemblyTargetAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [{ ParameterType.FullName: "System.String" }],
			MonoAndroidAssembly,
			JavaTypeMapUniverseType,
			out TypeMapAssemblyTargetAttributeCtor);

		var typeMapProxyAttrTypeDef = Context.GetType (TypeMapProxyAttributeTypeName);
		var typeMapProxyAttribute = AssemblyToInjectTypeMap.MainModule.ImportReference (typeMapProxyAttrTypeDef);
		var typeMapProxyAttrCtor = typeMapProxyAttrTypeDef.Methods.Single (m => m.IsConstructor);
		TypeMapProxyAttributeCtor = AssemblyToInjectTypeMap.MainModule.ImportReference (typeMapProxyAttrCtor);

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
	Dictionary<string, TypeDefinition> externalMappings = new ();
	// Need to inject the Proxy type, but cannot modify the assembly while iterating through types
	Dictionary<TypeDefinition, TypeDefinition> proxyMappings = new ();
	// list of proxy types to inject into AssemblyToInjectInto in EndProcess
	List<TypeDefinition> typesToInject = new ();

	/// <summary>
	/// Selects the best type that would be mapped to in the AndroidValueManager/AndroidTypeManager
	/// </summary>
	private TypeDefinition PickBestTargetType (TypeDefinition existing, TypeDefinition type)
	{
		if (type == existing)
			return existing;
		// Types in Mono.Android assembly should be chosen first
		if (existing.Module.Assembly.Name.Name != "Mono.Android" &&
				type.Module.Assembly.Name.Name == "Mono.Android") {
			return type;
		}
		// We found the `Invoker` type *before* the declared type
		// Fix things up so the abstract type is first, and the `Invoker` is considered a duplicate.
		if ((type.IsAbstract || type.IsInterface) &&
				!existing.IsAbstract &&
				!existing.IsInterface &&
				type.IsAssignableFrom ((TypeReference) existing, Context)) {
			return type;
		}
		// we found a generic subclass of a non-generic type
		if (type.IsGenericInstance &&
				!existing.IsGenericInstance &&
				type.IsAssignableFrom ((TypeReference) existing, Context)) {
			return type;
		}
		return existing;
	}

	/// <summary>
	/// Iterates through all types to find types that map to/from java types, and stores
	/// them for modifying the assemblies during EndProcess
	/// </summary>
	private void ProcessType (AssemblyDefinition assembly, TypeDefinition type)
	{
		if (type.HasJavaPeer (Context)) {
			string javaName = JavaNativeTypeManager.ToJniName (type, Context);
			if (externalMappings.TryGetValue (javaName, out var existing)) {
				externalMappings [javaName] = PickBestTargetType (existing, type);
			} else {
				externalMappings.Add (javaName, type);
			}
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Type '{type.FullName}' has peer '{javaName}'"));
			var proxyType = GenerateTypeMapProxyType (javaName, type);
			typesToInject.Add (proxyType);
			proxyMappings.Add (type, proxyType);
		} else {
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Type '{type.FullName}' has no peer"));
		}

		if (!type.HasNestedTypes)
			return;

		foreach (TypeDefinition nested in type.NestedTypes)
			ProcessType (assembly, nested);
	}

	CustomAttribute GenerateTypeMapAssociationAttribute (TypeDefinition type, TypeDefinition proxyType)
	{
		var ca = new CustomAttribute (TypeMapAssociationAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(proxyType)));
		return ca;
	}

	CustomAttribute GenerateTypeMapAttribute (TypeDefinition type, string javaName)
	{
		CustomAttribute ca = new (TypeMapAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemStringType, javaName));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));
		return ca;
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
		foreach (var mapping in externalMappings) {
			var attr = GenerateTypeMapAttribute (mapping.Value, mapping.Key);
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting [{attr.AttributeType.FullName}({string.Join (", ", attr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {AssemblyToInjectTypeMap.Name}"));
			AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
		}
		foreach (var mapping in proxyMappings) {
			var attr = GenerateTypeMapAssociationAttribute (mapping.Key, mapping.Value);
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
		var proxyType = new TypeDefinition (
			mappedType.Module.Assembly.Name.Name + "._." + declaringType.Namespace,
			mappedName.ToString () + "_<androidproxy>",
			TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed,
			AssemblyToInjectTypeMap.MainModule.TypeSystem.Object);

		var ca = new CustomAttribute (TypeMapProxyAttributeCtor);
		ca.ConstructorArguments.Add (new CustomAttributeArgument (SystemStringType, javaClassName));
		proxyType.CustomAttributes.Add (ca);
		return proxyType;
	}
}
