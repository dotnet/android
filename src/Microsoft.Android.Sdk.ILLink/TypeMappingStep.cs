using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Linq;
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
	const string TypeName = "Microsoft.Android.Runtime.TypeMap";
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
		Context.LogMessage ($"Writing {TypeMappings.Count} typemap entries");

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

		KeyValuePair<string, List<TypeDefinition>>[] orderedMapping = TypeMappings.OrderBy (kvp => Hash (kvp.Key)).ToArray ();

		var hashes = orderedMapping.Select (kvp => Hash (kvp.Key)).ToArray ();
		GenerateHashesFieldInitialization (hashes);

		var types = orderedMapping.Select (kvp => SelectTypeDefinition (kvp.Key, kvp.Value));
		GenerateGetTypeByIndex (types);

		var javaClassNames = orderedMapping.Select(kvp => kvp.Key);
		GenerateGetJavaClassNameByIndex (javaClassNames);

		void GenerateGetTypeByIndex (IEnumerable<TypeDefinition> types)
		{
			var method = type.Methods.FirstOrDefault (m => m.Name == "GetTypeByIndex");
			if (method is null) {
				Context.LogMessage ($"Unable to find {TypeName}.GetTypeByIndex() method");
				return;
			}

			var getTypeFromHandle = module.ImportReference (typeof (Type).GetMethod ("GetTypeFromHandle"));
			if (getTypeFromHandle is null) {
				Context.LogMessage ($"Unable to find Type.GetTypeFromHandle() method");
				return;
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
				Context.LogMessage ($"Unable to find {TypeName}.GetJavaClassNameByIndex() method");
				return;
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

		void GenerateHashesFieldInitialization (ulong[] hashes)
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

			var privateImplementationDetails = module.Types.FirstOrDefault (t => t.Name.Contains ("<PrivateImplementationDetails>"));
			if (privateImplementationDetails is null) {
				Context.LogMessage ($"Unable to find <PrivateImplementationDetails> class");
				return;
			}

			// Create static array struct for `byte[#number-of-hashes]`
			var arraySize = hashes.Length * sizeof (ulong);
			var arrayTypeName = $"__StaticArrayInitTypeSize={arraySize}";
			var arrayType = privateImplementationDetails.NestedTypes.FirstOrDefault (t => t.Name == arrayTypeName);
			if (arrayType is null) {
				arrayType = new TypeDefinition (
					"",
					arrayTypeName,
					TypeAttributes.NestedAssembly | TypeAttributes.ExplicitLayout,
					module.ImportReference (typeof (ValueType)))
				{
					PackingSize = 1,
					ClassSize = arraySize,
				};

				privateImplementationDetails.NestedTypes.Add (arrayType);
			}

			// Create field in `<PrivateImplementationDetails>...`
			var bytesField = new FieldDefinition (
				"<Microsoft.Android.Runtime.TypeMap>s_hashes",
				FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly,
				arrayType)
			{
				InitialValue = hashes.Select(h => BitConverter.GetBytes (h)).SelectMany (x => x).ToArray (),
			};

			if (!bytesField.Attributes.HasFlag (FieldAttributes.HasFieldRVA)) {
				throw new InvalidOperationException ($"Field {bytesField.Name} does not have RVA");
			}

			privateImplementationDetails.Fields.Add (bytesField);

			// Initialize s_hashes in .cctor from the RVA
			var field = type.Fields.FirstOrDefault (f => f.Name == "s_hashes");
			if (field is null) {
				Context.LogMessage ($"Unable to find {TypeName}.s_hashes field");
				return;
			}

			var initialize = type.Methods.FirstOrDefault (m => m.Name == "Initialize");
			if (initialize is null) {
				Context.LogMessage ($"Unable to find TypeMap.Initialize method");
				return;
			}

			initialize.Body.Instructions.Clear ();
			var il = initialize.Body.GetILProcessor ();

			il.Emit (OpCodes.Ldc_I4, hashes.Length);
			il.Emit (OpCodes.Newarr, module.ImportReference (typeof (ulong)));
			il.Emit (OpCodes.Dup);
			il.Emit (OpCodes.Ldtoken, bytesField);
			il.Emit (OpCodes.Call, module.ImportReference (typeof (System.Runtime.CompilerServices.RuntimeHelpers).GetMethod("InitializeArray")));
			il.Emit (OpCodes.Stsfld, field);

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

	static ulong Hash (string javaName)
	{
		var bytes = Encoding.UTF8.GetBytes (javaName);
		return XxHash3.HashToUInt64 (bytes);
	}
}
