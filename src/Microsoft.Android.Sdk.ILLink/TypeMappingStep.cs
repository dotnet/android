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

	delegate ulong HashMethod (ReadOnlySpan<byte> data, long seed = 0);
	HashMethod? _hashMethod;

	protected override void Process ()
	{
		_hashMethod = LoadHashMethod ();
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

		// Java -> .NET mapping
		var orderedJavaToDotnetMapping = TypeMappings.OrderBy (kvp => Hash (kvp.Key)).ToArray ();
		var javaClassNameHashes = orderedJavaToDotnetMapping.Select (kvp => Hash (kvp.Key)).ToArray ();
		var types = orderedJavaToDotnetMapping.Select (kvp => SelectTypeDefinition (kvp.Key, kvp.Value));

		GenerateHashes (javaClassNameHashes, methodName: "get_JavaClassNameHashes");
		GenerateGetTypeByIndex (types);
		GenerateStringSwitchMethod (type, "GetJavaClassNameByTypeIndex", orderedJavaToDotnetMapping.Select (kvp => kvp.Key).ToArray ());

		// .NET -> Java mapping
		var orderedManagedToJavaMapping = TypeMappings.SelectMany(kvp => kvp.Value.Select (type => new KeyValuePair<string, Guid>(kvp.Key, GetOrGenerateGuid (type)))).OrderBy (kvp => kvp.Value).ToArray ();
		var guids = orderedManagedToJavaMapping.Select (kvp => kvp.Value).ToArray ();
		var javaClassNames = orderedManagedToJavaMapping.Select (kvp => kvp.Key).ToArray ();

		byte[] guidBytes = guids.SelectMany (guid => guid.ToByteArray ()).ToArray ();
		GenerateReadOnlySpanGetter<Guid> (type, "get_TypeGuids", guidBytes, guids.Length, sizeOfT: 16);
		GenerateStringSwitchMethod (type, "GetJavaClassNameByIndex", javaClassNames);

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

		void GenerateStringSwitchMethod (TypeDefinition type, string methodName, string[] values)
		{
			var method = type.Methods.FirstOrDefault (m => m.Name == methodName);
			if (method is null) {
				throw new InvalidOperationException ($"Unable to find {type.FullName}.{methodName} method");
			}

			// Clear IL in method body
			method.Body.Instructions.Clear ();

			var il = method.Body.GetILProcessor ();

			var targets = new List<Instruction> ();
			foreach (var value in values) {
				targets.Add (il.Create (OpCodes.Ldstr, value));
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

		void GenerateHashes (ulong[] hashes, string methodName)
		{
			// Sanity check: hashes must be unique and sorted
			if (hashes.Length > 0) {
				ulong previous = hashes [0];
				for (int i = 1; i < hashes.Length; ++i) {
					if (hashes [i] == previous) {
						throw new InvalidOperationException ($"Duplicate hashes");
					} else if (hashes [i] < previous) {
						throw new InvalidOperationException ($"Hashes are not in ascending order");
					}

					previous = hashes [i];
				}
			}

			var data = hashes.SelectMany (BitConverter.GetBytes).ToArray ();
			GenerateReadOnlySpanGetter<ulong> (type, methodName, data, hashes.Length, sizeof (ulong));
		}

		void GenerateReadOnlySpanGetter<T> (TypeDefinition type, string name, byte[] data, int count, int sizeOfT)
			where T : struct
		{
			// Create static array struct for `byte[#data]`
			var arrayType = GetArrayType (type, data.Length);

			// Create static field to store the raw bytes
			var bytesField = new FieldDefinition ($"s_{name}_data", FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly, arrayType);
			bytesField.InitialValue = data;
			if (!bytesField.Attributes.HasFlag (FieldAttributes.HasFieldRVA)) {
				throw new InvalidOperationException ($"Field {bytesField.Name} does not have RVA");
			}

			type.Fields.Add (bytesField);

			// Generate the getter
			var getter = type.Methods.FirstOrDefault (f => f.Name == name) ?? throw new InvalidOperationException ($"Unable to find {type.FullName}.{name} method");

			getter.Body.Instructions.Clear ();
			var il = getter.Body.GetILProcessor ();

			il.Emit (OpCodes.Ldsflda, bytesField);
			il.Emit (OpCodes.Ldc_I4, count);
			il.Emit (OpCodes.Newobj, module.ImportReference (typeof (ReadOnlySpan<T>).GetConstructor (new[] { typeof(void*), typeof(int) })));

			il.Emit (OpCodes.Ret);
		}

		TypeDefinition GetArrayType (TypeDefinition type, int size)
		{
			var hashesArrayName = $"RawBytes_{size}";
			var arrayType = type.NestedTypes.FirstOrDefault (td => td.Name == hashesArrayName);
			if (arrayType is null) {
				arrayType = new TypeDefinition (
					"",
					hashesArrayName,
					TypeAttributes.NestedPrivate | TypeAttributes.ExplicitLayout,
					module.ImportReference (typeof (ValueType)))
				{
					PackingSize = 1,
					ClassSize = size,
				};

				type.NestedTypes.Add (arrayType);
			}

			return arrayType;
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

			// we found a generic subclass of a non-generic type
			if (type.IsGenericInstance &&
					!best.IsGenericInstance &&
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

	ulong Hash (string value)
	{
		ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(value.AsSpan ());
		ulong hash = _hashMethod!(bytes);

		if (!BitConverter.IsLittleEndian) {
			hash = BinaryPrimitives.ReverseEndianness (hash);
		}

		return hash;
	}

	Guid GetOrGenerateGuid (TypeDefinition type)
	{
		if (type.HasCustomAttributes) {
			foreach (var ca in type.CustomAttributes) {
				if (ca.AttributeType.FullName == "System.Runtime.InteropServices.GuidAttribute") {
					if (ca.ConstructorArguments.Count == 1 && ca.ConstructorArguments[0].Value is string guidString) {
						return new Guid (guidString);
					}
				}
			}
		}

		var guid = Guid.NewGuid ();
		var guidAttribute = new CustomAttribute (type.Module.ImportReference (typeof (System.Runtime.InteropServices.GuidAttribute).GetConstructor ([ typeof (string) ])));
		guidAttribute.ConstructorArguments.Add (new CustomAttributeArgument (type.Module.ImportReference (typeof (string)), guid.ToString ("D")));
		type.CustomAttributes.Add (guidAttribute);
		return guid;
	}

	HashMethod LoadHashMethod ()
	{
		if (!Context.TryGetCustomData (SystemIOHashingAssemblyPathCustomData, out var assemblyPath)) {
			throw new InvalidOperationException ($"The {nameof (TypeMappingStep)} step requires setting the '{SystemIOHashingAssemblyPathCustomData}' custom data");
		} else if (!System.IO.File.Exists (assemblyPath)) {
			throw new InvalidOperationException ($"The '{SystemIOHashingAssemblyPathCustomData}' custom data must point to a valid assembly path ('{assemblyPath}' does not exist)");
		}

		System.Reflection.MethodInfo? hashToUInt64MethodInfo;
		try {
			hashToUInt64MethodInfo = System.Reflection.Assembly.LoadFile (assemblyPath).GetType ("System.IO.Hashing.XxHash3")?.GetMethod ("HashToUInt64");
		} catch (Exception ex) {
			throw new InvalidOperationException ($"The '{SystemIOHashingAssemblyPathCustomData}' custom data must point to a valid assembly path ('{assemblyPath}' could not be loaded)", ex);
		}

		if (hashToUInt64MethodInfo is null) {
			throw new InvalidOperationException ($"Unable to find System.IO.Hashing.XxHash3.HashToUInt64 method, {nameof(TypeMappingStep)} cannot proceed");
		}

		return (HashMethod)Delegate.CreateDelegate (typeof (HashMethod), hashToUInt64MethodInfo, throwOnBindFailure: true);
	}
}
