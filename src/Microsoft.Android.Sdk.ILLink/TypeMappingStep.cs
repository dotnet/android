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
	const string AssemblyName = "Mono.Android";
	const string TypeName = "Microsoft.Android.Runtime.ManagedTypeMapping";
	const string SystemIOHashingAssemblyPathCustomData = "SystemIOHashingAssemblyPath";
	readonly IDictionary<string, List<TypeDefinition>> TypeMappings = new Dictionary<string, List<TypeDefinition>> (StringComparer.Ordinal);
	AssemblyDefinition? MonoAndroidAssembly;

	delegate ulong HashMethod (ReadOnlySpan<byte> data, long seed = 0);
	HashMethod? _hashMethod;

	protected override void Process ()
	{
		_hashMethod = LoadHashMethod ();
	}

	protected override void ProcessAssembly (AssemblyDefinition assembly)
	{
		if (assembly.Name.Name == AssemblyName) {
			MonoAndroidAssembly = assembly;
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

		if (MonoAndroidAssembly is null) {
			throw new InvalidOperationException ($"Unable to find {AssemblyName} assembly");
		}

		var module = MonoAndroidAssembly.MainModule;
		var type = module.GetType (TypeName);
		if (type is null) {
			throw new InvalidOperationException ($"Unable to find {TypeName} type");
		}

		var typeMappingRecords = TypeMappings
			.Select (kvp =>
				new TypeMapRecord {
					JniName = kvp.Key,
					Types = kvp.Value.ToArray (),
					Context = Context,
				})
			.ToArray ();

		// Java -> .NET mapping
		{
			var orderedJavaToDotnetMapping = typeMappingRecords.OrderBy (record => Hash (record.JniName))
				.ToArray ();
			var jniNames = orderedJavaToDotnetMapping.Select (record => record.JniName)
				.ToArray ();
			var jniNameHashes = jniNames.Select (Hash)
				.ToArray ();
			var types = orderedJavaToDotnetMapping.Select (record => record.SelectTypeDefinition ())
				.ToArray ();

			Context.LogMessage ("JNI -> .NET mappings");
			Context.LogMessage ($"Generated field {type.FullName}.s_get_JniNameHashes_data contains {jniNameHashes.Length} hashes:");
			for (int i = 0; i < jniNameHashes.Length; ++i ) {
				var java = jniNames [i];
				var hash = jniNameHashes [i];
				Context.LogMessage ($"\t0x{hash.ToString ("x", System.Globalization.CultureInfo.InvariantCulture), -16} // {i,4}: {java}");
			}
			Context.LogMessage ($"Generated method {type.FullName}.GetTypeByJniNameHashIndex contains {types.Length} mappings:");
			var maxAqtnLength = types.Max (t => TypeDefinitionRocks.GetAssemblyQualifiedName (t, Context).Length)+2;
			for (int i = 0; i < types.Length; ++i ) {
				var aqtn = TypeDefinitionRocks.GetAssemblyQualifiedName (types [i], Context);
				var java = jniNames [i];
				var hash = jniNameHashes [i];
				Context.LogMessage (
						string.Format (System.Globalization.CultureInfo.InvariantCulture,
							"\tindex {0,4} => Type.GetType({1,-" + maxAqtnLength + "}), // `{2}` hash=0x{3:x16}", i, $"\"{aqtn}\"", java, hash));
			}
			Context.LogMessage ($"Generated method {type.FullName}.GetJniNameByJniNameHashIndex contains {jniNames.Length} mappings:");
			var maxJavaLength = jniNames.Max (s => s.Length)+2;
			for (int i = 0; i < jniNames.Length; ++i ) {
				var java = jniNames [i];
				var aqtn = TypeDefinitionRocks.GetAssemblyQualifiedName (types [i], Context);
				var hash = jniNameHashes [i];
				Context.LogMessage (
						string.Format (System.Globalization.CultureInfo.InvariantCulture,
							"\tindex {0,4} => {1,-" + maxJavaLength + "}, // `{2}` hash=0x{3:x16}", i, $"\"{java}\"", aqtn, hash));
			}

			GenerateHashes (jniNameHashes, methodName: "get_JniNameHashes");
			GenerateGetTypeByJniNameHashIndex (types);
			GenerateStringSwitchMethod (type, "GetJniNameByJniNameHashIndex", jniNames);
		}

		// .NET -> Java mapping
		{
			var orderedManagedToJavaMapping = typeMappingRecords
				.SelectMany (record => record.Flatten ())
				.OrderBy (record => Hash (record.TypeName))
				.ToArray ();

			var typeNames = orderedManagedToJavaMapping
				.Select (record => record.TypeName)
				.ToArray ();
			var typeNameHashes = typeNames.Select (Hash)
				.ToArray ();
			var jniNames = orderedManagedToJavaMapping.Select (record => record.JniName)
				.ToArray ();

			Context.LogMessage (".NET -> JNI mappings");
			Context.LogMessage ($"Generated field {type.FullName}.s_get_TypeNameHashes_data contains {typeNameHashes.Length} hashes:");
			for (int i = 0; i < typeNameHashes.Length; ++i ) {
				var aqtn = typeNames [i];
				var hash = typeNameHashes [i];
				Context.LogMessage ($"\t0x{hash.ToString ("x", System.Globalization.CultureInfo.InvariantCulture), -16} // {i,4}: {aqtn}");
			}
			var maxJavaLength = jniNames.Max (s => s.Length) + 2;
			Context.LogMessage ($"Generated method {type.FullName}.GetJniNameByTypeNameHashIndex contains {jniNames.Length} mappings:");
			for (int i = 0; i < jniNames.Length; ++i ) {
				var java = jniNames [i];
				var aqtn = typeNames [i];
				var hash = typeNameHashes [i];
				Context.LogMessage (
						string.Format (System.Globalization.CultureInfo.InvariantCulture,
							"\tindex {0,4} => {1,-" + maxJavaLength + "}, // `{2}` hash=0x{3:x16}", i, $"\"{java}\"", aqtn, hash));
			}
			Context.LogMessage ($"Generated method {type.FullName}.GetTypeNameByTypeNameHashIndex contains {typeNames.Length} mappings:");
			var maxAqtnLength = typeNames.Max (s => s.Length) + 2;
			for (int i = 0; i < typeNames.Length; ++i ) {
				var java = jniNames [i];
				var aqtn = typeNames [i];
				var hash = typeNameHashes [i];
				Context.LogMessage (
						string.Format (System.Globalization.CultureInfo.InvariantCulture,
							"\tindex {0,4} => {1,-" + maxAqtnLength + "}, // `{2}` hash=0x{3:x16}", i, $"\"{aqtn}\"", java, hash));
			}

			GenerateHashes (typeNameHashes, methodName: "get_TypeNameHashes");
			GenerateStringSwitchMethod (type, "GetJniNameByTypeNameHashIndex", jniNames);
			GenerateStringSwitchMethod (type, "GetTypeNameByTypeNameHashIndex", typeNames);
		}

		void GenerateGetTypeByJniNameHashIndex (IEnumerable<TypeDefinition> types)
		{
			var method = type.Methods.FirstOrDefault (m => m.Name == "GetTypeByJniNameHashIndex");
			if (method is null) {
				throw new InvalidOperationException ($"Unable to find {TypeName}.GetTypeByJniNameHashIndex() method");
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

			GenerateReadOnlySpanGetter<ulong> (type, methodName, hashes, sizeof (ulong), BitConverter.GetBytes);
		}

		void GenerateReadOnlySpanGetter<T> (TypeDefinition type, string name, T[] data, int sizeOfT, Func<T, byte[]> getBytes)
			where T : struct
		{
			// Create static array struct for `byte[#data * sizeof(T)]`
			var arrayType = GetArrayType (type, data.Length * sizeOfT);

			// Create static field to store the raw bytes
			var bytesField = new FieldDefinition ($"s_{name}_data", FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly, arrayType);
			bytesField.InitialValue = data.SelectMany (getBytes).ToArray ();
			if (!bytesField.Attributes.HasFlag (FieldAttributes.HasFieldRVA)) {
				throw new InvalidOperationException ($"Field {bytesField.Name} does not have RVA");
			}

			type.Fields.Add (bytesField);

			// Generate the Hashes getter
			var getHashes = type.Methods.FirstOrDefault (f => f.Name == name) ?? throw new InvalidOperationException ($"Unable to find {TypeName}.{name} field");

			getHashes.Body.Instructions.Clear ();
			var il = getHashes.Body.GetILProcessor ();

			il.Emit (OpCodes.Ldsflda, bytesField);
			il.Emit (OpCodes.Ldc_I4, data.Length);
			il.Emit (OpCodes.Newobj, module.ImportReference (typeof (ReadOnlySpan<T>).GetConstructor (new[] { typeof(void*), typeof(int) })));

			il.Emit (OpCodes.Ret);
		}

		TypeDefinition GetArrayType (TypeDefinition type, int size)
		{
			var hashesArrayName = $"HashesArray_{size}";
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

	class TypeMapRecord
	{
		public required string JniName { get; init; }
		public required TypeDefinition[] Types { get; init; }
		public required LinkContext Context { get; init; }

		public string TypeName
		{
			get
			{
				// We need to drop the version, culture, and public key information from the AQN.
				var type = SelectTypeDefinition ();
				var assemblyQualifiedName = TypeDefinitionRocks.GetAssemblyQualifiedName (type, Context);
				var commaIndex = assemblyQualifiedName.IndexOf(',');
				var secondCommaIndex = assemblyQualifiedName.IndexOf(',', startIndex: commaIndex + 1);
				return  secondCommaIndex < 0
					? assemblyQualifiedName
					: assemblyQualifiedName.Substring (0, secondCommaIndex);
			}
		}

		public TypeDefinition SelectTypeDefinition ()
		{
			if (Types.Length == 1)
				return Types[0];

			var best = Types[0];
			foreach (var type in Types) {
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
			foreach (var type in Types) {
				if (type == best)
					continue;
				Context.LogMessage ($"Duplicate typemap entry for {JniName} => {type.FullName}");
			}
			return best;
		}

		public IEnumerable<TypeMapRecord> Flatten () =>
			Types.Select (type =>
				new TypeMapRecord {
					JniName = JniName,
					Types = new[] { type },
					Context = Context,
				});
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
