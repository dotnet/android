using System;
using System.Collections.Generic;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class ManagedMarshalMethodsLookupGenerator
{
	readonly ManagedMarshalMethodsLookupInfo managedMarshalMethodsLookupInfo;
	readonly TaskLoggingHelper log;
	readonly AndroidTargetArch targetArch;
	readonly TypeDefinition functionPointerLookup;

	readonly Dictionary<ModuleReference, Dictionary<Type, TypeReference>> typeReferenceCache = new ();

	public ManagedMarshalMethodsLookupGenerator (
		TaskLoggingHelper log,
		AndroidTargetArch targetArch,
		ManagedMarshalMethodsLookupInfo managedMarshalMethodsLookupInfo,
		TypeDefinition functionPointerLookup)
	{
		this.log = log;
		this.targetArch = targetArch;
		this.managedMarshalMethodsLookupInfo = managedMarshalMethodsLookupInfo;
		this.functionPointerLookup = functionPointerLookup;
	}

	public void Generate (IEnumerable<IList<MarshalMethodEntry>> marshalMethods)
	{
		foreach (IList<MarshalMethodEntry> methodList in marshalMethods) {
			foreach (MarshalMethodEntry method in methodList) {
				managedMarshalMethodsLookupInfo.AddNativeCallbackWrapper (method.NativeCallbackWrapper);
			}
		}

		foreach (var assemblyInfo in managedMarshalMethodsLookupInfo.AssemblyLookup.Values) {
			foreach (var classInfo in assemblyInfo.ClassLookup.Values) {
				classInfo.GetFunctionPointerMethod = GenerateGetFunctionPointer (classInfo);
			}

			assemblyInfo.GetFunctionPointerMethod = GenerateGetFunctionPointerPerAssembly (assemblyInfo);
		}

		GenerateGlobalGetFunctionPointerMethod ();
	}

	MethodDefinition GenerateGetFunctionPointer (ManagedMarshalMethodsLookupInfo.ClassLookupInfo classLookup)
	{
		var declaringType = classLookup.DeclaringType;
		log.LogDebugMessage ($"Generating `<JI>GetFunctionPointer` for {declaringType.FullName} ({classLookup.MethodLookup.Count} UCO methods)");

		var intType = ImportReference (declaringType.Module, typeof (int));
		var intPtrType = ImportReference (declaringType.Module, typeof (System.IntPtr));

		// an "unspeakable" method name is used to avoid conflicts with user-defined methods
		var getFunctionPointerMethod = classLookup.GetFunctionPointerMethod = new MethodDefinition ("<JI>GetFunctionPointer", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, intPtrType);

		getFunctionPointerMethod.DeclaringType = declaringType;
		declaringType.Methods.Add (getFunctionPointerMethod);

		var methodIndexParameter = new ParameterDefinition ("methodIndex", ParameterAttributes.None, intType);
		getFunctionPointerMethod.Parameters.Add (methodIndexParameter);

		// we're setting the index here as we are creating the actual switch targets array to guarantee the indexing will be in sync
		var targets = new Instruction [classLookup.MethodLookup.Count];
		uint methodIndex = 0;
		foreach (var methodLookup in classLookup.MethodLookup.Values) {
			methodLookup.Index = methodIndex++;
			targets [methodLookup.Index] = Instruction.Create (OpCodes.Ldftn, methodLookup.NativeCallbackWrapper);
		}

		var invalidUcoTarget = Instruction.Create (OpCodes.Nop);

		var il = getFunctionPointerMethod.Body.GetILProcessor ();
		il.Emit (OpCodes.Ldarg, methodIndexParameter);
		il.Emit (OpCodes.Switch, targets);

		il.Emit (OpCodes.Br_S, invalidUcoTarget);

		for (var k = 0; k < targets.Length; k++) {
			il.Append (targets [k]);
			il.Emit (OpCodes.Ret);
		}

		// no hit? this shouldn't happen
		il.Append (invalidUcoTarget);
		il.Emit (OpCodes.Ldc_I4_M1);
		il.Emit (OpCodes.Conv_I);
		il.Emit (OpCodes.Ret);

		// in the case of private/private protected/protected nested types, we need to generate proxy method(s) in the parent type(s)
		// so that we can call the actual GetFunctionPointer method from our assembly-level GetFunctionPointer method
		while (declaringType.IsNested && (declaringType.IsNestedPrivate || declaringType.IsNestedFamily || declaringType.IsNestedFamilyAndAssembly)) {
			var parentType = declaringType.DeclaringType;

			// an "unspeakable" method name is used to avoid conflicts with user-defined methods
			var parentProxyMethod = new MethodDefinition ($"<JI>GetFunctionPointer_{declaringType.Name}", MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig, intPtrType);
			parentProxyMethod.DeclaringType = parentType;
			parentType.Methods.Add (parentProxyMethod);

			var parentMethodIndexParameter = new ParameterDefinition ("methodIndex", ParameterAttributes.None, intType);
			parentProxyMethod.Parameters.Add (parentMethodIndexParameter);

			var proxyIl = parentProxyMethod.Body.GetILProcessor ();
			proxyIl.Emit (OpCodes.Ldarg, parentMethodIndexParameter);
			proxyIl.Emit (OpCodes.Call, getFunctionPointerMethod);
			proxyIl.Emit (OpCodes.Ret);

			declaringType = parentType;
			getFunctionPointerMethod = parentProxyMethod;
		}

		return getFunctionPointerMethod;
	}

	MethodDefinition GenerateGetFunctionPointerPerAssembly (ManagedMarshalMethodsLookupInfo.AssemblyLookupInfo assemblyInfo)
	{
		var module = assemblyInfo.Assembly.MainModule;

		var intType = ImportReference (module, typeof (int));
		var intPtrType = ImportReference (module, typeof (System.IntPtr));
		var objectType = ImportReference (module, typeof (object));

		var lookupType = new TypeDefinition ("Java.Interop", "__ManagedMarshalMethodsLookupTable__", TypeAttributes.Public | TypeAttributes.Sealed, objectType);
		module.Types.Add (lookupType);

		var getFunctionPointerMethod = new MethodDefinition ($"GetFunctionPointer", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, intPtrType);
		getFunctionPointerMethod.DeclaringType = lookupType;
		lookupType.Methods.Add (getFunctionPointerMethod);

		var classIndexParameter = new ParameterDefinition ("classIndex", ParameterAttributes.None, intType);
		getFunctionPointerMethod.Parameters.Add (classIndexParameter);

		var methodIndexParameter = new ParameterDefinition ("methodIndex", ParameterAttributes.None, intType);
		getFunctionPointerMethod.Parameters.Add (methodIndexParameter);

		// we're setting the index here as we are creating the actual switch targets array to guarantee the indexing will be in sync
		var targets = new Instruction [assemblyInfo.ClassLookup.Count];
		uint classIndex = 0;
		foreach (var classInfo in assemblyInfo.ClassLookup.Values) {
			classInfo.Index = classIndex++;
			targets [classInfo.Index] = Instruction.Create (OpCodes.Call, module.Import (classInfo.GetFunctionPointerMethod));
		}

		var il = getFunctionPointerMethod.Body.GetILProcessor ();
		il.Emit (OpCodes.Ldarg, methodIndexParameter);

		il.Emit (OpCodes.Ldarg, classIndexParameter);
		il.Emit (OpCodes.Switch, targets);

		var defaultTarget = Instruction.Create (OpCodes.Nop);
		il.Emit (OpCodes.Br, defaultTarget);

		for (int i = 0; i < targets.Length; i++) {
			il.Append (targets [i]); // call
			il.Emit (OpCodes.Ret);
		}

		// no hit? this shouldn't happen
		il.Append (defaultTarget);
		il.Emit (OpCodes.Pop); // methodIndex
		il.Emit (OpCodes.Ldc_I4_M1);
		il.Emit (OpCodes.Conv_I);
		il.Emit (OpCodes.Ret);

		return getFunctionPointerMethod;
	}

	void GenerateGlobalGetFunctionPointerMethod ()
	{
		var module = functionPointerLookup.Module;

		var intType = ImportReference (module, typeof (int));
		var intPtrType = ImportReference (module, typeof (System.IntPtr));

		var getFunctionPointerMethod = FindMethod (functionPointerLookup, "GetFunctionPointer", parametersCount: 3, required: true);
		getFunctionPointerMethod.Body.Instructions.Clear ();

		// we're setting the index here as we are creating the actual switch targets array to guarantee the indexing will be in sync
		var targets = new Instruction [managedMarshalMethodsLookupInfo.AssemblyLookup.Count];
		uint assemblyIndex = 0;
		foreach (var assemblyInfo in managedMarshalMethodsLookupInfo.AssemblyLookup.Values) {
			assemblyInfo.Index = assemblyIndex++;
			targets [assemblyInfo.Index] = Instruction.Create (OpCodes.Call, module.Import (assemblyInfo.GetFunctionPointerMethod));
		}

		var il = getFunctionPointerMethod.Body.GetILProcessor ();
		il.Emit (OpCodes.Ldarg_1); // classIndex
		il.Emit (OpCodes.Ldarg_2); // methodIndex

		il.Emit (OpCodes.Ldarg_0); // assemblyIndex
		il.Emit (OpCodes.Switch, targets);

		var defaultTarget = Instruction.Create (OpCodes.Nop);
		il.Emit (OpCodes.Br_S, defaultTarget);

		for (int i = 0; i < targets.Length; i++) {
			il.Append (targets [i]); // call
			il.Emit (OpCodes.Ret);
		}

		// no hit? this shouldn't happen
		il.Append (defaultTarget);
		il.Emit (OpCodes.Pop); // methodIndex
		il.Emit (OpCodes.Pop); // classIndex
		il.Emit (OpCodes.Ldc_I4_M1);
		il.Emit (OpCodes.Conv_I);
		il.Emit (OpCodes.Ret);
	}

	TypeReference ImportReference (ModuleDefinition module, Type type)
	{
		if (!typeReferenceCache.TryGetValue (module, out var cache)) {
			typeReferenceCache [module] = cache = new ();
		}

		if (!cache.TryGetValue (type, out var typeReference)) {
			cache [type] = typeReference = module.ImportReference (type);
		}

		return typeReference;
	}

	MethodDefinition? FindMethod (TypeDefinition type, string methodName, int parametersCount, bool required)
	{
		log.LogDebugMessage ($"[{targetArch}] Looking for method '{methodName}' with {parametersCount} params in type {type}");
		foreach (MethodDefinition method in type.Methods) {
			log.LogDebugMessage ($"[{targetArch}]   method: {method.Name}");
			if (method.Parameters.Count == parametersCount && String.Compare (methodName, method.Name, StringComparison.Ordinal) == 0) {
				log.LogDebugMessage ($"[{targetArch}]     match!");
				return method;
			}
		}

		if (required) {
			throw new InvalidOperationException ($"[{targetArch}] Internal error: required method '{methodName}' in type {type} not found");
		}

		return null;
	}
}
