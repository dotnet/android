using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using Mono.Cecil.Cil;
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
	TypeReference TypeMapAttribute { get; set; }
	MethodReference TypeMapAttributeCtor { get; set; }

	const string JavaTypeMapUniverseTypeName = "Java.Lang.Object";
	TypeReference JavaTypeMapUniverseType { get; set; }

	TypeReference SystemTypeType { get; set; }
	TypeReference SystemStringType { get; set; }

	AssemblyDefinition EntryPointAssembly { get; set; }

	protected override void Process ()
	{
		EntryPointAssembly = Context.Annotations.GetType ().GetMethod ("GetEntryPointAssembly")?.Invoke (Context.Annotations, null) as AssemblyDefinition ?? throw new NotImplementedException ("asdfasdf NoEntryPoint");
		JavaTypeMapUniverseType = EntryPointAssembly.MainModule.ImportReference (Context.GetType (JavaTypeMapUniverseTypeName));
		var typeMapAttributeDefinition = Context.GetType (TypeMapAttributeTypeName);
		TypeMapAttribute = EntryPointAssembly.MainModule.ImportReference (typeMapAttributeDefinition.MakeGenericInstanceType (JavaTypeMapUniverseType));
		var typeMapAttributeCtorDefinition = typeMapAttributeDefinition.Methods
			.FirstOrDefault (m => m.IsConstructor
				&& m.Parameters is [
				{ ParameterType.FullName: "System.String" },
				{ ParameterType.FullName: "System.Type" },
				{ ParameterType.FullName: "System.Type" }]) ?? throw new InvalidOperationException ("Couldn't find TypeMapAttribute<T>..ctor(string, Type, Type)");
		var typeMapAttributeCtor = new MethodReference (
			typeMapAttributeCtorDefinition.Name,
			typeMapAttributeCtorDefinition.ReturnType,
			TypeMapAttribute) {
			HasThis = typeMapAttributeCtorDefinition.HasThis,
			ExplicitThis = typeMapAttributeCtorDefinition.ExplicitThis,
			CallingConvention = typeMapAttributeCtorDefinition.CallingConvention,
		};
		foreach (var param in typeMapAttributeCtorDefinition.Parameters) {
			typeMapAttributeCtor.Parameters.Add (new ParameterDefinition (
				param.Name,
				param.Attributes,
				EntryPointAssembly.MainModule.ImportReference (param.ParameterType)));
		}

		TypeMapAttributeCtor = EntryPointAssembly.MainModule.ImportReference (typeMapAttributeCtor);
		SystemTypeType = EntryPointAssembly.MainModule.ImportReference (Context.GetType ("System.Type"));
		SystemStringType = EntryPointAssembly.MainModule.ImportReference (Context.GetType ("System.String"));

		Context.LogMessage (MessageContainer.CreateInfoMessage ($"""
			EntryPointAssembly: {EntryPointAssembly.Name}
			TypeMapUnivers: {JavaTypeMapUniverseType.FullName}
			TypeMapType: {typeMapAttributeDefinition.FullName}
			TypeMapAttr: {TypeMapAttribute.FullName}
			TypeMapAtrtibuteCtorDefinition: {typeMapAttributeCtorDefinition.FullName}
			TypeMapAtrtibuteCtor: {typeMapAttributeCtor.FullName}
			System.Type: {SystemTypeType}
			System.String: {SystemStringType}
		"""));
	}


	protected override void ProcessAssembly (AssemblyDefinition assembly)
	{
		foreach (var type in assembly.MainModule.Types) {
			ProcessType (assembly, type);
		}
	}
	Dictionary<string, List<TypeDefinition>> javaNameToTypes = new ();
	Dictionary<TypeDefinition, string> typeToJavaName = new ();

	List<CustomAttribute> injectedAttributes = new ();

	private void ProcessType (AssemblyDefinition assembly, TypeDefinition type)
	{
		if (type.HasJavaPeer (Context)) {
			string javaName = JavaNativeTypeManager.ToJniName (type, Context);
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Type '{type.FullName}' has peer '{javaName}'"));
			injectedAttributes.Add (GenerateTypeMapAttribute (type, javaName));
		} else {
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Type '{type.FullName}' has no peer"));
		}

		if (!type.HasNestedTypes)
			return;

		foreach (TypeDefinition nested in type.NestedTypes)
			ProcessType (assembly, nested);
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
		foreach (var attr in injectedAttributes) {
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting [{attr.AttributeType.FullName}({string.Join (", ", attr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {EntryPointAssembly.Name}"));
			EntryPointAssembly.CustomAttributes.Add (attr);
		}
		EntryPointAssembly.Write (Context.GetAssemblyLocation (EntryPointAssembly) + ".injected");
	}
}
