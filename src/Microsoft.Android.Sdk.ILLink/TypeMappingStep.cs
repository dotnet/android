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
	const string TypeName = "Android.Runtime.NativeAOT.NativeAotTypeManager";
	readonly IDictionary<string, TypeDefinition> TypeMappings = new Dictionary<string, TypeDefinition> (StringComparer.Ordinal);
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
		foreach (var (javaKey, typeDefinition) in TypeMappings) {
			/*
			 * IL_0000: ldsfld class [System.Runtime]System.Collections.Generic.IDictionary`2<string, class [System.Runtime]System.Type> Android.Runtime.NativeAOT.NativeAotTypeManager::TypeMappings
			 * IL_0005: ldstr "android/app/Activity"
			 * IL_000a: ldtoken [Mono.Android]Android.App.Activity
			 * IL_000f: call class [System.Runtime]System.Type [System.Runtime]System.Type::GetTypeFromHandle(valuetype [System.Runtime]System.RuntimeTypeHandle)
			 * IL_0014: callvirt instance void class [System.Runtime]System.Collections.Generic.IDictionary`2<string, class [System.Runtime]System.Type>::Add(!0, !1)
			 */
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldsfld, field);
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldstr, javaKey);
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldtoken, module.ImportReference (typeDefinition));
			il.Emit (Mono.Cecil.Cil.OpCodes.Call, getTypeFromHandle);
			il.Emit (Mono.Cecil.Cil.OpCodes.Callvirt, addMethod);
		}

		il.Emit (Mono.Cecil.Cil.OpCodes.Ret);
	}

	void ProcessType (AssemblyDefinition assembly, TypeDefinition type)
	{
		if (type.HasJavaPeer (Context)) {
			var javaName = JavaNativeTypeManager.ToJniName (type, Context);
			if (!TypeMappings.TryAdd (javaName, type)) {
				Context.LogMessage ($"Duplicate typemap entry for {javaName}");
			}
		}

		if (!type.HasNestedTypes)
			return;

		foreach (TypeDefinition nested in type.NestedTypes)
			ProcessType (assembly, nested);
	}
}
