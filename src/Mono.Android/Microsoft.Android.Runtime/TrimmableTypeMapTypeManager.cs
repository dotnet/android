#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Type manager for the trimmable typemap path. Delegates type lookups
/// to <see cref="TrimmableTypeMap"/>.
/// </summary>
class TrimmableTypeMapTypeManager : JniRuntime.JniTypeManager
{
	readonly ConcurrentDictionary<Type, JniTypeSignature> _typeSignatureCache = new ();

	// This type manager has 2 core APIs: GetTypeSignatureCore for managed-to-Java lookups, and GetTypeForSimpleReference for Java-to-managed lookups.
	// The rest of the APIs are unsupported and will throw if called, as they are not needed internally anywhere.

	public override IEnumerable<Type> GetTypes (JniTypeSignature typeSignature)
	{
		if (typeSignature.SimpleReference is null) {
			return [];
		}

		var simpleReference = typeSignature.SimpleReference ?? throw new InvalidOperationException ("Should not be reached");
		var simpleTypes = GetTypesForSimpleReference (simpleReference);

		if (typeSignature.ArrayRank == 0) {
			return simpleTypes;
		}

		return GetFlattenedArrayTypes (typeSignature, simpleTypes);

		// Multiple managed types can map to a single JNI type and a single managed type can map to multiple array types.
		IEnumerable<Type> GetFlattenedArrayTypes (JniTypeSignature typeSignature, IEnumerable<Type> elementTypes)
		{
			Debug.Assert (typeSignature.ArrayRank > 0, "Should not be reached");

			foreach (var elementType in elementTypes) {
				foreach (var arrayType in GetArrayTypes (typeSignature, elementType)) {
					yield return arrayType;
				}
			}
		}

		// A single managed type can map to multiple array types, e.g., JavaArray<T>, JavaPrimitiveArray<T>, and T[].
		static IEnumerable<Type> GetArrayTypes (JniTypeSignature typeSignature, Type elementType)
		{
			Debug.Assert (elementType != typeof (void), "Cannot create an array of void");

			// We only pre-generate the array types proxy map for Native AOT because we can't manipulate types at runtime.
			// For CoreCLR, we take advantage of the dynamic runtime and we save app size by not pre-generating the array types proxy map.
			if (RuntimeFeature.IsNativeAotRuntime) {
				return TrimmableTypeMap.Instance.TryGetArrayProxy (elementType, typeSignature.ArrayRank, out var arrayProxy)
					? arrayProxy.GetArrayTypes ()
					: [];
			}
			
			if (RuntimeFeature.IsCoreClrRuntime) {
				return GetArrayTypesForCoreClr (typeSignature, elementType);
			}
			
			throw new NotSupportedException ("Unsupported runtime.");

			[UnconditionalSuppressMessage ("Trimming", "IL2026:RequiresUnreferencedCode",
				Justification = "This API is called as part of Java to .NET type marshalling when the target type is expected as the input " +
					"parameter of the target method, so it must be seen by the IL trimmer. This justification would not hold for Native AOT " +
					"but this codepath is only reachable on CoreCLR.")]
			static IEnumerable<Type> GetArrayTypesForCoreClr (JniTypeSignature typeSignature, Type elementType)
			{
				if (IsKeyword (typeSignature)) {
					return GetPrimitiveArrayTypes (elementType, typeSignature.ArrayRank);
				}

				return MakeArrayTypes (elementType, typeSignature.ArrayRank);
			}

			static bool IsKeyword (JniTypeSignature typeSignature)
			{
				// typeSignature.IsKeyword is not public so we're using this workaround
				var keywordTypeSignature = new JniTypeSignature (typeSignature.SimpleReference, typeSignature.ArrayRank, keyword: true);
				return typeSignature.Equals (keywordTypeSignature);
			}

			[RequiresDynamicCode ("This API uses reflection to create generic types at runtime, which is not supported in AOT scenarios.")]
			[RequiresUnreferencedCode ("This API uses reflection to create array types at runtime, which is not supported in trimming scenarios.")]
			static IEnumerable<Type> GetPrimitiveArrayTypes (Type elementType, int rank)
			{
				Debug.Assert (elementType != typeof (void), "Cannot create an array of void");
				Debug.Assert (rank > 0, "At least one array rank is expected");

				if (!PrimitiveArrayInfo.TryGetArrayTypes (elementType, out var types)) {
					throw new InvalidOperationException ($"Cannot create an array of type '{elementType.FullName}'");
				}

				foreach (var type in types) {
					if (rank == 1) {
						yield return type;
					} else {
						foreach (var arrayType in MakeArrayTypes (type, rank - 1)) {
							yield return arrayType;
						}
					}
				}
			}

			[RequiresDynamicCode ("This API uses reflection to create generic types at runtime, which is not supported in AOT scenarios.")]
			[RequiresUnreferencedCode ("This API uses reflection to create array types at runtime, which is not supported in trimming scenarios.")]
			static IEnumerable<Type> MakeArrayTypes (Type elementType, int rank)
			{
				Debug.Assert (rank > 0, "At least one array rank is expected");

				var javaObjectArrayType = elementType;
				for (int i = 0; i < rank; i++) {
					javaObjectArrayType = typeof (JavaObjectArray<>).MakeGenericType (javaObjectArrayType);
				}

				var arrayType = elementType;
				for (int i = 0; i < rank; i++) {
					arrayType = arrayType.MakeArrayType ();
				}

				return [javaObjectArrayType, arrayType];
			}
		}
	}

	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		var builtInType = GetBuiltInTypeForSimpleReference (jniSimpleReference);
		if (builtInType is not null) {
			yield return builtInType;
		}

		if (TrimmableTypeMap.Instance.TryGetTargetTypes (jniSimpleReference, out var types)) {
			foreach (var type in types) {
				yield return type;
			}
		}

		/// <summary>
		/// Lookup of the JNI type signature for a built-in reference type, e.g., string, bool?, int?, etc.
		/// </summary>
		/// <param name="jniSimpleReference"></param>
		/// <returns></returns>
		static Type? GetBuiltInTypeForSimpleReference (string jniSimpleReference)
		{
			return jniSimpleReference switch {
				"java/lang/String"     => typeof (string),
				"V"                    => typeof (void),
				"Z"                    => typeof (bool),
				"java/lang/Boolean"    => typeof (bool?),
				"B"                    => typeof (sbyte),
				"java/lang/Byte"       => typeof (sbyte?),
				"C"                    => typeof (char),
				"java/lang/Character"  => typeof (char?),
				"S"                    => typeof (short),
				"java/lang/Short"      => typeof (short?),
				"I"                    => typeof (int),
				"java/lang/Integer"    => typeof (int?),
				"J"                    => typeof (long),
				"java/lang/Long"       => typeof (long?),
				"F"                    => typeof (float),
				"java/lang/Float"      => typeof (float?),
				"D"                    => typeof (double),
				"java/lang/Double"     => typeof (double?),
				_                      => null,
			};
		}
	}

	protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
	{
		var types = GetTypesForSimpleReference (jniSimpleReference);
		using var enumerator = types.GetEnumerator ();
		return enumerator.MoveNext () ? enumerator.Current : null;
	}

	protected override JniTypeSignature GetTypeSignatureCore (Type type)
	{
		return _typeSignatureCache.GetOrAdd (type, GetTypeSignatureUncached);

		static JniTypeSignature GetTypeSignatureUncached (Type type)
		{
			type = GetUnderlyingType (type, out int rank);

			if (TryGetBuiltInTypeSignature (type, out var signature)) {
				return signature.AddArrayRank (rank);
			}

			if (type.IsGenericType) {
				var genericDefinition = type.GetGenericTypeDefinition ();
				if (genericDefinition == typeof (JavaArray<>)
					|| genericDefinition == typeof (JavaObjectArray<>)
					|| genericDefinition == typeof (JavaPrimitiveArray<>)) {
					var elementSignature = GetTypeSignatureUncached (type.GenericTypeArguments [0]);
					return elementSignature.AddArrayRank (rank + 1);
				}
			}

			// Walk the base type chain for managed-only subclasses (e.g., JavaProxyThrowable
			// extends Java.Lang.Error but has no [Register] attribute itself).
			Type? currentType = type;

			while (currentType is not null) {
				if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (currentType, out var jniName)) {
					return new (jniName, rank, keyword: false);
				}

				currentType = currentType.BaseType;
			}

			return default;

			static Type GetUnderlyingType (Type type, out int rank)
			{
				rank = 0;
				var originalType = type;
				while (type.IsArray) {
					if (type.GetArrayRank () > 1)
						throw new ArgumentException ($"Multidimensional array '{originalType.FullName}' is not supported.", nameof (type));
					rank++;
					type = type.GetElementType () ?? throw new InvalidOperationException ("Array type has no element type.");
				}

				if (type.IsEnum)
					type = Enum.GetUnderlyingType (type);

				return type;
			}

			static bool TryGetBuiltInTypeSignature (Type type, out JniTypeSignature signature)
			{
				// Keep the hybrid Type.GetTypeCode + explicit nullable checks. Nullable.GetUnderlyingType ()
				// allocates a Type[] via GetGenericArguments (), and this path is otherwise allocation-free.
				if (GetPrimitiveTypeJniName (type) is string primitiveJniTypeName) {
					signature = new JniTypeSignature (primitiveJniTypeName, keyword: true);
					return true;
				}

				if (type == typeof (void)) {
					signature = new JniTypeSignature ("V", keyword: true);
					return true;
				}

				if (TryGetBuiltInReferenceJniName (type, out var jniName)) {
					signature = new JniTypeSignature (jniName);
					return true;
				}

				if (PrimitiveArrayInfo.TryGetTypeSignature (type, out signature)) {
					return true;
				}

				signature = default;
				return false;

				static string? GetPrimitiveTypeJniName (Type type)
				{
					return Type.GetTypeCode (type) switch {
						TypeCode.Boolean => "Z",
						TypeCode.Byte => "B",
						TypeCode.SByte => "B",
						TypeCode.Char => "C",
						TypeCode.Int16 => "S",
						TypeCode.UInt16 => "S",
						TypeCode.Int32 => "I",
						TypeCode.UInt32 => "I",
						TypeCode.Int64 => "J",
						TypeCode.UInt64 => "J",
						TypeCode.Single => "F",
						TypeCode.Double => "D",
						_ => null,
					};
				}

				/// <summary>
				/// Lookup of the JNI type signature for a built-in reference type, e.g., string, bool?, int?, etc.
				/// </summary>
				static bool TryGetBuiltInReferenceJniName (Type type, [NotNullWhen (true)] out string? jni)
				{
					if (type == typeof (string))  { jni = "java/lang/String"; return true; }
					if (type == typeof (bool?))   { jni = "java/lang/Boolean"; return true; }
					if (type == typeof (sbyte?))  { jni = "java/lang/Byte"; return true; }
					if (type == typeof (char?))   { jni = "java/lang/Character"; return true; }
					if (type == typeof (short?))  { jni = "java/lang/Short"; return true; }
					if (type == typeof (int?))    { jni = "java/lang/Integer"; return true; }
					if (type == typeof (long?))   { jni = "java/lang/Long"; return true; }
					if (type == typeof (float?))  { jni = "java/lang/Float"; return true; }
					if (type == typeof (double?)) { jni = "java/lang/Double"; return true; }
					jni = null;
					return false;
				}

			}
		}
	}

	protected override IEnumerable<JniTypeSignature> GetTypeSignaturesCore (Type type)
	{
		var signature = GetTypeSignatureCore (type);
		return signature.IsValid ? [signature] : [];
	}

	// Remapping APIs for InTune support

	protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
		=> JniRemappingLookup.GetStaticMethodFallbackTypes (jniSimpleReference, useReplacementTypes: true);

	protected override string? GetReplacementTypeCore (string jniSimpleReference)
		=> JniRemappingLookup.GetReplacementType (jniSimpleReference);

	protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
		=> JniRemappingLookup.GetReplacementMethodInfo (jniSourceType, jniMethodName, jniMethodSignature);

	// The rest of the APIs are unsupported - they are not needed internally anywhere anyway

	protected override Type? GetInvokerTypeCore (Type type)
		=> throw new UnreachableException (
			$"{nameof (GetInvokerTypeCore)} should not be called in the trimmable typemap path. " +
			$"Invoker types should use generated {nameof (JavaPeerProxy)} instances.");

	protected override string? GetSimpleReference (Type type)
		=> throw new UnreachableException (
			$"{nameof (GetSimpleReference)} should not be called in the trimmable typemap path. " +
			$"Simple reference lookup should use {nameof (GetTypeSignatureCore)} to get the full type signature, including simple reference.");

	protected override IEnumerable<string> GetSimpleReferences (Type type)
		=> throw new UnreachableException (
			$"{nameof (GetSimpleReferences)} should not be called in the trimmable typemap path. " +
			$"Simple reference lookup should use {nameof (GetTypeSignatureCore)} to get the full type signature, including simple reference.");

	public override void RegisterNativeMembers (JniType nativeClass, Type type, ReadOnlySpan<char> methods)
		=> throw new UnreachableException (
			$"RegisterNativeMembers should not be called in the trimmable typemap path. " +
			$"Native methods for '{type.FullName}' should be registered by JCW static initializer blocks.");

	[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>)")]
	public override void RegisterNativeMembers (JniType nativeClass, Type type, string? methods)
		=> throw new UnreachableException (
			$"RegisterNativeMembers should not be called in the trimmable typemap path. " +
			$"Native methods for '{type.FullName}' should be registered by JCW static initializer blocks.");
}
