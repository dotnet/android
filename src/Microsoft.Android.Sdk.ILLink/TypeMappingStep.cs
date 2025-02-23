using System;
using System.Collections.Generic;
using System.Linq;
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

		var method = type.Methods.FirstOrDefault (m => m.Name == "GetTypeByIndex");
		if (method is null) {
			Context.LogMessage ($"Unable to find {TypeName}.GetTypeByIndex() method");
			return;
		}

		// Clear IL in method body
		method.Body.Instructions.Clear ();

		Context.LogMessage ($"Writing {TypeMappings.Count} typemap entries");
		var il = method.Body.GetILProcessor ();

		var getTypeFromHandle = module.ImportReference (typeof (Type).GetMethod ("GetTypeFromHandle"));
		var orderedMapping = TypeMappings.Select(kvp => (Hash (kvp.Key), SelectTypeDefinition (kvp.Key, kvp.Value))).OrderBy (x => x.Item1);

		var hashes = new List<ulong> ();
		var targets = new List<Instruction> ();
		foreach (var (hash, target) in orderedMapping) {
			hashes.Add (hash);
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

		AddHashesRVAField (module, type, hashes.ToArray ());
	}

	void AddHashesRVAField (ModuleDefinition module, TypeDefinition type, ulong[] hashes)
	{
		var privateImplementationDetails = GetPrivateImplementationType ();
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

		var cctor = new MethodDefinition (
			".cctor",
			MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			module.TypeSystem.Void);

		var il = cctor.Body.GetILProcessor ();
		il.Emit (OpCodes.Ldc_I4, hashes.Length);
		il.Emit (OpCodes.Newarr, module.ImportReference (typeof (ulong)));
		il.Emit (OpCodes.Dup);
		il.Emit (OpCodes.Ldtoken, bytesField);
		il.Emit (OpCodes.Call, module.ImportReference (typeof (System.Runtime.CompilerServices.RuntimeHelpers).GetMethod("InitializeArray")));
		il.Emit (OpCodes.Stsfld, field);
		il.Emit (OpCodes.Ret);

		type.Methods.Add (cctor);

		TypeDefinition? GetPrivateImplementationType ()
		{
			foreach (var type in module.Types)
				if (type.FullName.Contains ("<PrivateImplementationDetails>"))
					return type;

			return null;
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
		var bytes = System.Text.Encoding.UTF8.GetBytes (javaName);
		// TODO the custom linker step cannot have a dependency??
		// or we would need to copy the System.IO.Hashing.dll manually to make it work?
		// return System.IO.Hashing.XxHash3.HashToUInt64 (bytes);
		return (ulong)javaName.GetHashCode ();
	}
}
