using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Linker;
using Mono.Linker.Steps;

namespace Microsoft.Android.Sdk.ILLink;

/// <summary>
/// MVP "typemap" implementation for NativeAOT
/// </summary>
public class TypeMappingStep : BaseStep
{
	const string AssemblyName = "Microsoft.Android.Runtime.NativeAOT";
	const string TypeName = "Microsoft.Android.Runtime.TypeMapping";
	const string SystemIOHashingAssemblyPathCustomData = "SystemIOHashingAssemblyPath";
	readonly IDictionary<string, List<TypeDefinition>> TypeMappings = new Dictionary<string, List<TypeDefinition>> (StringComparer.Ordinal);
	AssemblyDefinition? MicrosoftAndroidRuntimeNativeAot;

	AssemblyLoadContext? _hashingAssemblyLoadContext;
	delegate ulong HashMethod (ReadOnlySpan<byte> data, long seed = 0);
	HashMethod? _hashMethod;

	protected override void Process ()
	{
		if (!Context.TryGetCustomData (SystemIOHashingAssemblyPathCustomData, out var assemblyPath)) {
			throw new InvalidOperationException ($"The {nameof (TypeMappingStep)} step requires setting the '{SystemIOHashingAssemblyPathCustomData}' custom data");
		} else if (!System.IO.File.Exists (assemblyPath)) {
			throw new InvalidOperationException ($"The '{SystemIOHashingAssemblyPathCustomData}' custom data must point to a valid assembly path ('{assemblyPath}' does not exist)");
		}

		_hashingAssemblyLoadContext = new AssemblyLoadContext (name: null, isCollectible: true);
		_hashMethod = GetHashMethod (_hashingAssemblyLoadContext, assemblyPath);
	}

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
		Context.LogMessage ($"Writing {TypeMappings.Count} typemap entries");

		if (MicrosoftAndroidRuntimeNativeAot is null) {
			throw new InvalidOperationException ($"Unable to find {AssemblyName} assembly");
		}

		var module = MicrosoftAndroidRuntimeNativeAot.MainModule;
		var type = module.GetType (TypeName);
		if (type is null) {
			throw new InvalidOperationException ($"Unable to find {TypeName} type");
		}

		KeyValuePair<string, List<TypeDefinition>>[] orderedMapping = TypeMappings.OrderBy (kvp => Hash (kvp.Key)).ToArray ();

		var hashes = orderedMapping.Select (kvp => Hash (kvp.Key)).ToArray ();
		GenerateHashes (hashes);

		var types = orderedMapping.Select (kvp => SelectTypeDefinition (kvp.Key, kvp.Value));
		GenerateGetTypeByIndex (types);

		var javaClassNames = orderedMapping.Select(kvp => kvp.Key);
		GenerateGetJavaClassNameByIndex (javaClassNames);

		_hashingAssemblyLoadContext?.Unload ();
		_hashingAssemblyLoadContext = null;
		_hashMethod = null;

		void GenerateGetTypeByIndex (IEnumerable<TypeDefinition> types)
		{
			var method = type.Methods.FirstOrDefault (m => m.Name == "GetTypeByIndex");
			if (method is null) {
				throw new InvalidOperationException ($"Unable to find {TypeName}.GetTypeByIndex() method");
			}

			var getTypeFromHandle = module.ImportReference (typeof (Type).GetMethod ("GetTypeFromHandle"));
			if (getTypeFromHandle is null) {
				throw new InvalidOperationException ($"Unable to find Type.GetTypeFromHandle() method");
			}

			// Clear IL in method body
			method.Body.Instructions.Clear ();

			var il = method.Body.GetILProcessor ();

			var targets = new List<Instruction> ();
			foreach (var target in types) {
				targets.Add (il.Create (OpCodes.Ldtoken, module.ImportReference (target)));
			}

			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Switch, targets.ToArray ());

			var defaultTarget = il.Create (OpCodes.Ldnull);
			il.Emit (OpCodes.Br, defaultTarget);

			foreach (var target in targets) {
				il.Append (target);
				il.Emit (OpCodes.Call, getTypeFromHandle);
				il.Emit (OpCodes.Ret);
			}

			il.Append (defaultTarget);
			il.Emit (OpCodes.Ret);
		}

		void GenerateGetJavaClassNameByIndex (IEnumerable<string> names)
		{
			var method = type.Methods.FirstOrDefault (m => m.Name == "GetJavaClassNameByIndex");
			if (method is null) {
				throw new InvalidOperationException ($"Unable to find {TypeName}.GetJavaClassNameByIndex() method");
			}

			// Clear IL in method body
			method.Body.Instructions.Clear ();

			var il = method.Body.GetILProcessor ();

			var orderedMapping = TypeMappings.Select(kvp => (Hash (kvp.Key), SelectTypeDefinition (kvp.Key, kvp.Value))).OrderBy (x => x.Item1);

			var targets = new List<Instruction> ();
			foreach (var name in names) {
				targets.Add (il.Create (OpCodes.Ldstr, name));
			}

			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Switch, targets.ToArray ());

			var defaultTarget = il.Create (OpCodes.Ldnull);
			il.Emit (OpCodes.Br, defaultTarget);

			foreach (var target in targets) {
				il.Append (target);
				il.Emit (OpCodes.Ret);
			}

			il.Append (defaultTarget);
			il.Emit (OpCodes.Ret);
		}

		void GenerateHashes (ulong[] hashes)
		{
			// Sanity check: hashes must be unique and sorted
			if (hashes.Length > 0) {
				ulong previous = hashes[0];
				for (int i = 1; i < hashes.Length; ++i) {
					if (hashes[i] == previous) {
						throw new InvalidOperationException ($"Duplicate hashes");
					} else if (hashes[i] < previous) {
						throw new InvalidOperationException ($"Hashes are not in ascending order");
					}

					previous = hashes[i];
				}
			}

			// Create static array struct for `byte[#number-of-hashes]`
			var arrayType = new TypeDefinition (
				"",
				"HashesArray",
				TypeAttributes.NestedPrivate | TypeAttributes.ExplicitLayout,
				module.ImportReference (typeof (ValueType)))
			{
				PackingSize = 1,
				ClassSize = hashes.Length * sizeof (ulong),
			};

			type.NestedTypes.Add (arrayType);

			// Create static field to store the raw bytes
			var bytesField = new FieldDefinition ("s_hashes", FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly, arrayType);
			bytesField.InitialValue = hashes.Select(h => BitConverter.GetBytes (h)).SelectMany (x => x).ToArray ();
			if (!bytesField.Attributes.HasFlag (FieldAttributes.HasFieldRVA)) {
				throw new InvalidOperationException ($"Field {bytesField.Name} does not have RVA");
			}

			type.Fields.Add (bytesField);

			// Generate the Hashes getter
			var getHashes = type.Methods.FirstOrDefault (f => f.Name == "get_Hashes") ?? throw new InvalidOperationException ($"Unable to find {TypeName}.get_Hashes field");

			getHashes.Body.Instructions.Clear ();
			var il = getHashes.Body.GetILProcessor ();

			il.Emit (OpCodes.Ldsflda, bytesField);
			il.Emit (OpCodes.Call, module.ImportReference (typeof (System.Runtime.CompilerServices.Unsafe).GetMethod("AsPointer")));
			il.Emit (OpCodes.Ldc_I4, hashes.Length);
			il.Emit (OpCodes.Newobj, module.ImportReference (typeof (ReadOnlySpan<ulong>).GetConstructor (new[] { typeof(void*), typeof(int) })));

			il.Emit (OpCodes.Ret);
		}
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

	ulong Hash (string javaName)
	{
		ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(javaName.AsSpan ());
		return _hashMethod!(bytes);
	}

	static HashMethod GetHashMethod (AssemblyLoadContext alc, string assemblyPath)
	{
		System.Reflection.Assembly? hashingAssembly;
		try {
			hashingAssembly = alc.LoadFromAssemblyPath (assemblyPath);
		} catch (Exception ex) {
			throw new InvalidOperationException ($"Unable to load {assemblyPath}, {nameof(TypeMappingStep)} cannot proceed", ex);
		}

		var hashToUInt64 = hashingAssembly?.GetType ("System.IO.Hashing.XxHash3")?.GetMethod ("HashToUInt64");
		if (hashToUInt64 is null) {
			throw new InvalidOperationException ($"Unable to find System.IO.Hashing.XxHash3.HashToUInt64 method, {nameof(TypeMappingStep)} cannot proceed");
		}

		return (HashMethod)Delegate.CreateDelegate (typeof (HashMethod), hashToUInt64, throwOnBindFailure: true);
	}
}
