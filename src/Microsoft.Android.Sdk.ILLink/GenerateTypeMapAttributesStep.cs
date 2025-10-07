using System;
using System.Collections.Generic;
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
	TypeReference TypeMapAttribute;
	MethodReference TypeMapAttributeCtor;

	const string TypeMapAssociationAttributeTypeName = "System.Runtime.InteropServices.TypeMapAssociationAttribute`1";
	TypeReference TypeMapAssociationAttribute;
	MethodReference TypeMapAssociationAttributeCtor;

	const string TypeMapProxyAttributeTypeName = "Java.Interop.TypeMapProxyAttribute";
	TypeReference TypeMapProxyAttribute;
	MethodReference TypeMapProxyAttributeCtor;

	const string TypeMapAssemblyTargetAttributeTypeName = "System.Runtime.InteropServices.TypeMapAssemblyTargetAttribute`1";
	TypeReference TypeMapAssemblyTargetAttribute;
	MethodReference TypeMapAssemblyTargetAttributeCtor;

	const string JavaTypeMapUniverseTypeName = "Java.Lang.Object";
	TypeReference JavaTypeMapUniverseType { get; set; }

	TypeReference SystemTypeType { get; set; }
	TypeReference SystemStringType { get; set; }

	AssemblyDefinition EntryPointAssembly { get; set; }
	AssemblyDefinition MonoAndroidAssembly { get; set; }

	void GetTypeMapAttributeReferences (
		string attributeTypeName,
		Func<MethodDefinition, bool> ctorSelector,
		AssemblyDefinition addReferencesTo,
		TypeReference typeMapUniverse,
		out TypeReference attributeType,
		out MethodReference ctor)
	{
		var typeMapAttributeDefinition = Context.GetType (attributeTypeName);
		attributeType = addReferencesTo.MainModule.ImportReference (typeMapAttributeDefinition.MakeGenericInstanceType (typeMapUniverse));

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
		EntryPointAssembly = Context.Annotations.GetType ().GetMethod ("GetEntryPointAssembly")?.Invoke (Context.Annotations, null) as AssemblyDefinition ?? throw new NotImplementedException ("asdfasdf NoEntryPoint");
		var javaTypeMapUniverseTypeDefinition = Context.GetType (JavaTypeMapUniverseTypeName);
		JavaTypeMapUniverseType = EntryPointAssembly.MainModule.ImportReference (javaTypeMapUniverseTypeDefinition);

		GetTypeMapAttributeReferences (TypeMapAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [
				{ ParameterType.FullName: "System.String" },
				{ ParameterType.FullName: "System.Type" },
				{ ParameterType.FullName: "System.Type" }],
			EntryPointAssembly,
			JavaTypeMapUniverseType,
			out TypeMapAttribute,
			out TypeMapAttributeCtor);

		GetTypeMapAttributeReferences (TypeMapAssociationAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [
				{ ParameterType.FullName: "System.Type" },
				{ ParameterType.FullName: "System.Type" }],
			EntryPointAssembly,
			JavaTypeMapUniverseType,
			out TypeMapAssociationAttribute,
			out TypeMapAssociationAttributeCtor);

		MonoAndroidAssembly = javaTypeMapUniverseTypeDefinition.Module.Assembly;
		GetTypeMapAttributeReferences (TypeMapAssemblyTargetAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [{ ParameterType.FullName: "System.String" }],
			MonoAndroidAssembly,
			JavaTypeMapUniverseType,
			out TypeMapAssemblyTargetAttribute,
			out TypeMapAssemblyTargetAttributeCtor);

		var typeMapProxyAttrTypeDef = Context.GetType ("Java.Interop.TypeMapProxyAttribute");
		var typeMapProxyAttrCtor = typeMapProxyAttrTypeDef.Methods.Single (m => m.IsConstructor);
		TypeMapProxyAttribute = EntryPointAssembly.MainModule.ImportReference (typeMapProxyAttrTypeDef);
		TypeMapProxyAttributeCtor = EntryPointAssembly.MainModule.ImportReference (typeMapProxyAttrCtor);

		SystemTypeType = EntryPointAssembly.MainModule.ImportReference (Context.GetType ("System.Type"));
		SystemStringType = EntryPointAssembly.MainModule.ImportReference (Context.GetType ("System.String"));
	}


	protected override void ProcessAssembly (AssemblyDefinition assembly)
	{
		foreach (var type in assembly.MainModule.Types) {
			ProcessType (assembly, type);
		}
	}

	List<CustomAttribute> injectedAttributes = new ();
	List<TypeDefinition> injectedTypes = new ();

	private void ProcessType (AssemblyDefinition assembly, TypeDefinition type)
	{
		if (type.HasJavaPeer (Context)) {
			string javaName = JavaNativeTypeManager.ToJniName (type, Context);
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Type '{type.FullName}' has peer '{javaName}'"));
			injectedAttributes.Add (GenerateTypeMapAttribute (type, javaName));

			var proxyType = GenerateTypeMapProxyType (javaName, type);
			injectedTypes.Add (proxyType);
			injectedAttributes.Add (GenerateTypeMapAssociationAttribute (type, proxyType));
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
		ca.ConstructorArguments.Add (new (SystemTypeType, type));
		ca.ConstructorArguments.Add (new (SystemTypeType, proxyType));
		return ca;
	}

	CustomAttribute GenerateTypeMapAttribute (TypeDefinition type, string javaName)
	{
		CustomAttribute ca = new (TypeMapAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemStringType, javaName));
		ca.ConstructorArguments.Add (new (SystemTypeType, type));
		ca.ConstructorArguments.Add (new (SystemTypeType, type));
		return ca;
	}

	protected override void EndProcess ()
	{
		Context.Annotations.GetType ().GetField ("entry_assembly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue (Context.Annotations, MonoAndroidAssembly);
		foreach(var type in injectedTypes) {
			EntryPointAssembly.MainModule.Types.Add (type);
		}
		foreach (var attr in injectedAttributes) {
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting [{attr.AttributeType.FullName}({string.Join (", ", attr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {EntryPointAssembly.Name}"));
			EntryPointAssembly.CustomAttributes.Add (attr);
		}
		EntryPointAssembly.Write (Context.GetAssemblyLocation (EntryPointAssembly) + Random.Shared.GetHexString(4) + ".injected.dll");

		// JNIEnvInit sets Mono.Android as the entrypoint assembly. Forward the typemap logic to the user/custom assembly;
		CustomAttribute targetAssembly = new (TypeMapAssemblyTargetAttributeCtor);
		targetAssembly.ConstructorArguments.Add (new (SystemStringType, EntryPointAssembly.Name.FullName));
		MonoAndroidAssembly.CustomAttributes.Add (targetAssembly);
		MonoAndroidAssembly.Write (Path.Combine(
			Path.GetDirectoryName(Context.GetAssemblyLocation (EntryPointAssembly)),
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
			mappedName.ToString() +  "_<androidproxy>",
			TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed,
			EntryPointAssembly.MainModule.TypeSystem.Object);

		var ca = new CustomAttribute (TypeMapProxyAttributeCtor);
		ca.ConstructorArguments.Add (new CustomAttributeArgument (SystemStringType, javaClassName));
		proxyType.CustomAttributes.Add (ca);

		return proxyType;
	}
}
