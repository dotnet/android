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
	internal const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
	internal const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	static readonly Type[] EmptyTypeArray = [];
	static readonly Dictionary<Type, Type> ArrayTypes = [];
	static readonly Dictionary<Type, Type> JavaObjectArrayTypes = [];
	static readonly Dictionary<Type, Type[]> PrimitiveArrayTypes = [];
	readonly ConcurrentDictionary<Type, JniTypeSignature> _typeSignatureCache = new ();

	// This type manager has 2 core APIs: GetTypeSignatureCore for managed-to-Java lookups, and GetTypeForSimpleReference for Java-to-managed lookups.
	// The rest of the APIs are unsupported and will throw if called, as they are not needed internally anywhere.

	static TrimmableTypeMapTypeManager ()
	{
		AddKnownArrayTypes<string> ();
		AddKnownArrayTypes<JavaObject> ();

		AddKnownPrimitiveArrayTypes<bool, JavaBooleanArray> ();
		AddKnownPrimitiveArrayTypes<sbyte, JavaSByteArray> ();
		AddKnownPrimitiveArrayTypes<char, JavaCharArray> ();
		AddKnownPrimitiveArrayTypes<short, JavaInt16Array> ();
		AddKnownPrimitiveArrayTypes<int, JavaInt32Array> ();
		AddKnownPrimitiveArrayTypes<long, JavaInt64Array> ();
		AddKnownPrimitiveArrayTypes<float, JavaSingleArray> ();
		AddKnownPrimitiveArrayTypes<double, JavaDoubleArray> ();
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
				if (genericDefinition == typeof (JavaArray<>) || genericDefinition == typeof (JavaObjectArray<>)) {
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
				if (GetKeywordTypeName (type) is string keywordTypeName) {
					signature = new JniTypeSignature (keywordTypeName, 0, keyword: true);
					return true;
				}

				static string? GetKeywordTypeName (Type type)
					=> Type.GetTypeCode (type) switch {
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

				if (type == typeof (void)) {
					signature = new JniTypeSignature ("V", 0, keyword: true);
					return true;
				}

				if (type == typeof (string)) {
					signature = new JniTypeSignature ("java/lang/String");
					return true;
				}

				if (type == typeof (bool?)) {
					signature = new JniTypeSignature ("java/lang/Boolean");
					return true;
				}
				if (type == typeof (sbyte?)) {
					signature = new JniTypeSignature ("java/lang/Byte");
					return true;
				}
				if (type == typeof (char?)) {
					signature = new JniTypeSignature ("java/lang/Character");
					return true;
				}
				if (type == typeof (short?)) {
					signature = new JniTypeSignature ("java/lang/Short");
					return true;
				}
				if (type == typeof (int?)) {
					signature = new JniTypeSignature ("java/lang/Integer");
					return true;
				}
				if (type == typeof (long?)) {
					signature = new JniTypeSignature ("java/lang/Long");
					return true;
				}
				if (type == typeof (float?)) {
					signature = new JniTypeSignature ("java/lang/Float");
					return true;
				}
				if (type == typeof (double?)) {
					signature = new JniTypeSignature ("java/lang/Double");
					return true;
				}

				if (TryGetPrimitiveArrayTypeSignature (type, out signature))
					return true;

				signature = default;
				return false;
			}
		}
	}

	[return: DynamicallyAccessedMembers (Methods | Constructors | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
	protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
	{
		var builtInType = GetBuiltInTypeForSimpleReference (jniSimpleReference);
		if (builtInType is not null) {
			return builtInType;
		}

		return TrimmableTypeMap.Instance.TryGetTargetTypes (jniSimpleReference, out var types) && types.Length > 0
			? types [0].Type
			: null;
	}

	[return: DynamicallyAccessedMembers (Methods | Constructors | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
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

	// Remapping APIs for InTune support

	protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
		=> JniRemappingLookup.GetStaticMethodFallbackTypes (jniSimpleReference, useReplacementTypes: true);

	protected override string? GetReplacementTypeCore (string jniSimpleReference)
		=> JniRemappingLookup.GetReplacementType (jniSimpleReference);

	protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
		=> JniRemappingLookup.GetReplacementMethodInfo (jniSourceType, jniMethodName, jniMethodSignature);

	// The rest of the APIs are unsupported - they are not needed internally anywhere anyway

	[return: DynamicallyAccessedMembers (Constructors)]
	protected override Type? GetInvokerTypeCore ([DynamicallyAccessedMembers (Constructors)] Type type)
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

	public override IEnumerable<Type> GetTypes (JniTypeSignature typeSignature)
	{
		if (typeSignature.SimpleReference is null) {
			return EmptyTypeArray;
		}
		return CreateGetTypesEnumerator (typeSignature);
	}

	public override IEnumerable<ReflectionConstructibleType> GetReflectionConstructibleTypes (JniTypeSignature typeSignature)
		=> throw new UnreachableException (
			$"{nameof (GetReflectionConstructibleTypes)} should not be called in the trimmable typemap path. " +
			$"Managed peer construction should use generated {nameof (JavaPeerProxy)} instances.");

	protected override IEnumerable<JniTypeSignature> GetTypeSignaturesCore (Type type)
	{
		var signature = GetTypeSignatureCore (type);
		if (signature.IsValid) {
			yield return signature;
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
				yield return type.Type;
			}
		}
	}

	IEnumerable<Type> CreateGetTypesEnumerator (JniTypeSignature typeSignature)
	{
		if (!typeSignature.IsValid) {
			yield break;
		}

		foreach (var type in GetTypesForSimpleReference (typeSignature.SimpleReference ?? throw new InvalidOperationException ("Should not be reached"))) {
			if (typeSignature.ArrayRank == 0) {
				yield return type;
				continue;
			}

			if (IsKeywordSignature (typeSignature)) {
				foreach (var primitiveArrayType in GetPrimitiveArrayTypesForSimpleReference (typeSignature, type)) {
					yield return primitiveArrayType;
				}
				continue;
			}

			if (TryMakeJavaObjectArrayType (type, typeSignature.ArrayRank, out var javaObjectArrayType)) {
				yield return javaObjectArrayType;
			}

			if (TryMakeArrayType (type, typeSignature.ArrayRank, out var arrayType)) {
				yield return arrayType;
			}
		}
	}

	IEnumerable<Type> GetPrimitiveArrayTypesForSimpleReference (JniTypeSignature typeSignature, Type type)
	{
		foreach (var primitiveArrayType in GetPrimitiveArrayTypes (type)) {
			var rank = typeSignature.ArrayRank - 1;
			if (TryMakeJavaObjectArrayType (primitiveArrayType, rank, out var javaObjectArrayType)) {
				yield return javaObjectArrayType;
			}

			if (TryMakeArrayType (primitiveArrayType, rank, out var arrayType)) {
				yield return arrayType;
			}
		}
	}

	static bool IsKeywordSignature (JniTypeSignature typeSignature)
		=> typeSignature.SimpleReference is string simpleReference &&
			typeSignature.QualifiedReference == new string ('[', typeSignature.ArrayRank) + simpleReference;

	static bool TryMakeArrayType (Type elementType, int rank, [NotNullWhen (true)] out Type? arrayType)
	{
		arrayType = elementType;
		for (int i = 0; i < rank; i++) {
			if (!TryMakeArrayType (arrayType, out arrayType)) {
				return false;
			}
		}
		return true;
	}

	static bool TryMakeArrayType (Type elementType, [NotNullWhen (true)] out Type? arrayType)
	{
		if (ArrayTypes.TryGetValue (elementType, out arrayType)) {
			return true;
		}

		return TrimmableTypeMap.Instance.TryGetArrayType (elementType, out arrayType);
	}

	static bool TryMakeJavaObjectArrayType (Type elementType, int rank, [NotNullWhen (true)] out Type? arrayType)
	{
		arrayType = elementType;
		for (int i = 0; i < rank; i++) {
			if (!TryMakeJavaObjectArrayType (arrayType, out arrayType)) {
				return false;
			}
		}
		return true;
	}

	static bool TryMakeJavaObjectArrayType (Type elementType, [NotNullWhen (true)] out Type? arrayType)
	{
		return JavaObjectArrayTypes.TryGetValue (elementType, out arrayType);
	}

	static Type[] GetPrimitiveArrayTypes (Type primitiveType)
		=> PrimitiveArrayTypes.TryGetValue (primitiveType, out var types) ? types : EmptyTypeArray;

	static void AddKnownPrimitiveArrayTypes<
			[DynamicallyAccessedMembers (Constructors)]
			T,
			[DynamicallyAccessedMembers (Constructors)]
			TArray> ()
	{
		AddKnownArrayTypes<T> ();
		AddKnownArrayTypes<JavaArray<T>> ();
		AddKnownArrayTypes<JavaPrimitiveArray<T>> ();
		AddKnownArrayTypes<TArray> ();
		PrimitiveArrayTypes [typeof (T)] = [
			typeof (T[]),
			typeof (JavaArray<T>),
			typeof (JavaPrimitiveArray<T>),
			typeof (TArray),
		];
	}

	static void AddKnownArrayTypes<
			[DynamicallyAccessedMembers (Constructors)]
			T> ()
	{
		ArrayTypes [typeof (T)] = typeof (T[]);
		ArrayTypes [typeof (T[])] = typeof (T[][]);
		ArrayTypes [typeof (T[][])] = typeof (T[][][]);
		JavaObjectArrayTypes [typeof (T)] = typeof (JavaObjectArray<T>);
		JavaObjectArrayTypes [typeof (JavaObjectArray<T>)] = typeof (JavaObjectArray<JavaObjectArray<T>>);
	}

	public override void RegisterNativeMembers (JniType nativeClass, [DynamicallyAccessedMembers (Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)] Type type, ReadOnlySpan<char> methods)
		=> throw new UnreachableException (
			$"RegisterNativeMembers should not be called in the trimmable typemap path. " +
			$"Native methods for '{type.FullName}' should be registered by JCW static initializer blocks.");

	[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>)")]
	public override void RegisterNativeMembers (JniType nativeClass, [DynamicallyAccessedMembers (Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)] Type type, string? methods)
		=> throw new UnreachableException (
			$"RegisterNativeMembers should not be called in the trimmable typemap path. " +
			$"Native methods for '{type.FullName}' should be registered by JCW static initializer blocks.");
}
