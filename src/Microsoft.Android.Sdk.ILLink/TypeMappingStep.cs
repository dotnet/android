using System;
using System.Collections.Generic;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;

namespace Microsoft.Android.Sdk.ILLink;

/// <summary>
/// MVP "typemap" implementation for NativeAOT
/// </summary>
public class TypeMappingStep : BaseStep
{
	const string AssemblyName = "Microsoft.Android.Runtime.NativeAOT";
	const string TypeName = "Microsoft.Android.Runtime.NativeAotTypeManager";
	readonly IDictionary<string, List<TypeDefinition>> TypeMappings = new Dictionary<string, List<TypeDefinition>> (StringComparer.Ordinal);
	AssemblyDefinition? MicrosoftAndroidRuntimeNativeAot;

	protected override void ProcessAssembly (AssemblyDefinition assembly)
	{
		if (assembly.Name.Name == AssemblyName) {
			MicrosoftAndroidRuntimeNativeAot = assembly;
			return;
		}
		if (Annotations?.GetAction (assembly) == AssemblyAction.Delete)
			return;

		foreach (var type in assembly.MainModule.Types) {
			ProcessType (assembly, type);
		}
	}

	protected override void EndProcess ()
	{
		if (MicrosoftAndroidRuntimeNativeAot is null) {
			Context.LogMessage ($"Unable to find {AssemblyName} assembly");
			return;
		}

		var module = MicrosoftAndroidRuntimeNativeAot.MainModule;
		var type = module.GetType (TypeName);
		if (type is null) {
			Context.LogMessage ($"Unable to find {TypeName} type");
			return;
		}

		var method = type.Methods.FirstOrDefault (m => m.Name == "InitializeTypeMappings");
		if (method is null) {
			Context.LogMessage ($"Unable to find {TypeName}.InitializeTypeMappings() method");
			return;
		}

		var field = type.Fields.FirstOrDefault (f => f.Name == "TypeMappings");
		if (field is null) {
			Context.LogMessage ($"Unable to find {TypeName}.TypeMappings field");
			return;
		}

		// Clear IL in method body
		method.Body.Instructions.Clear ();

		Context.LogMessage ($"Writing {TypeMappings.Count} typemap entries");
		var il = method.Body.GetILProcessor ();
		var addMethod = module.ImportReference (typeof (IDictionary<string, Type>).GetMethod ("Add"));
		var getTypeFromHandle = module.ImportReference (typeof (Type).GetMethod ("GetTypeFromHandle"));
		foreach (var (javaName, list) in TypeMappings) {
			/*
			 * IL_0000: ldarg.0
			 * IL_0001: ldfld class [System.Runtime]System.Collections.Generic.IDictionary`2<string, class [System.Runtime]System.Type> Microsoft.Android.Runtime.NativeAotTypeManager::TypeMappings
			 * IL_0006: ldstr "java/lang/Object"
			 * IL_000b: ldtoken [Mono.Android]Java.Lang.Object
			 * IL_0010: call class [System.Runtime]System.Type [System.Runtime]System.Type::GetTypeFromHandle(valuetype [System.Runtime]System.RuntimeTypeHandle)
			 * IL_0015: callvirt instance void class [System.Runtime]System.Collections.Generic.IDictionary`2<string, class [System.Runtime]System.Type>::Add(!0, !1)
			 */
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_0);
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldfld, field);
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldstr, javaName);
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldtoken, module.ImportReference (SelectTypeDefinition (javaName, list)));
			il.Emit (Mono.Cecil.Cil.OpCodes.Call, getTypeFromHandle);
			il.Emit (Mono.Cecil.Cil.OpCodes.Callvirt, addMethod);
		}

		il.Emit (Mono.Cecil.Cil.OpCodes.Ret);
	}

	TypeDefinition SelectTypeDefinition (string javaName, List<TypeDefinition> list)
	{
		if (list.Count == 1)
			return list[0];

		var best = list[0];
		foreach (var type in list) {
			if (type == best)
				continue;
			// Types in Mono.Android assembly should be first in the list
			if (best.Module.Assembly.Name.Name != "Mono.Android" &&
					type.Module.Assembly.Name.Name == "Mono.Android") {
				best = type;
				continue;
			}
			// We found the `Invoker` type *before* the declared type 
 			// Fix things up so the abstract type is first, and the `Invoker` is considered a duplicate. 
			if ((type.IsAbstract || type.IsInterface) &&
					!best.IsAbstract &&
					!best.IsInterface &&
					type.IsAssignableFrom (best, Context)) {
				best = type;
				continue;
			}
		}
		foreach (var type in list) {
			if (type == best)
				continue;
			Context.LogMessage ($"Duplicate typemap entry for {javaName} => {type.FullName}");
		}
		return best;
	}

	void ProcessType (AssemblyDefinition assembly, TypeDefinition type)
	{
		if (type.HasJavaPeer (Context)) {
			var javaName = JavaNativeTypeManager.ToJniName (type, Context);
			if (!TypeMappings.TryGetValue (javaName, out var list)) {
				TypeMappings.Add (javaName, list = new List<TypeDefinition> ());
			}
			list.Add (type);
		}

		if (!type.HasNestedTypes)
			return;

		foreach (TypeDefinition nested in type.NestedTypes)
			ProcessType (assembly, nested);
	}
}
