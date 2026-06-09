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
	const string NoSimpleReference = "\0";
	internal const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
	internal const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;
	readonly ConcurrentDictionary<Type, string> _simpleReferenceCache = new ();

	protected override JniTypeSignature GetTypeSignatureCore (Type type)
	{
		type = GetUnderlyingType (type, out int rank);

		if (TryGetBuiltInTypeSignature (type, out var signature)) {
			return signature.AddArrayRank (rank);
		}

		var simpleReference = GetSimpleReference (type);
		return simpleReference == null ? default : new JniTypeSignature (simpleReference, rank, false);
	}

	protected override IEnumerable<JniTypeSignature> GetTypeSignaturesCore (Type type)
	{
		type = GetUnderlyingType (type, out int rank);

		if (TryGetBuiltInTypeSignature (type, out var signature)) {
			yield return signature.AddArrayRank (rank);
		}

		foreach (var simpleReference in GetSimpleReferences (type)) {
			yield return new JniTypeSignature (simpleReference, rank, false);
		}
	}

	static Type GetUnderlyingType (Type type, out int rank)
	{
		rank = 0;
		var originalType = type;
		while (type.IsArray) {
			if (type.GetArrayRank () > 1)
				throw new ArgumentException ("Multidimensional array '" + originalType.FullName + "' is not supported.", nameof (type));
			rank++;
			type = type.GetElementType () ?? throw new InvalidOperationException ("Array type has no element type.");
		}

		if (type.IsEnum)
			type = Enum.GetUnderlyingType (type);

		return type;
	}

	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		if (TryGetBuiltInTypeForSimpleReference (jniSimpleReference, out var builtIn)) {
			yield return builtIn;
		}

		if (TrimmableTypeMap.Instance.TryGetTargetTypes (jniSimpleReference, out var types)) {
			foreach (var type in types) {
				yield return type;
			}
		}
	}

	protected override string? GetSimpleReference (Type type)
	{
		var simpleReference = _simpleReferenceCache.GetOrAdd (type, GetSimpleReferenceUncached);
		return simpleReference == NoSimpleReference ? null : simpleReference;
	}

	string GetSimpleReferenceUncached (Type type)
	{
		if (TryGetBuiltInTypeSignature (type, out var signature)) {
			return signature.SimpleReference ?? NoSimpleReference;
		}

		if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (type, out var jniName)) {
			return jniName;
		}

		// Walk the base type chain for managed-only subclasses (e.g., JavaProxyThrowable
		// extends Java.Lang.Error but has no [Register] attribute itself).
		for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType) {
			if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (baseType, out var baseJniName)) {
				return baseJniName;
			}
		}

		return NoSimpleReference;
	}

	protected override IEnumerable<string> GetSimpleReferences (Type type)
	{
		if (TryGetBuiltInTypeSignature (type, out var signature) && signature.SimpleReference is not null) {
			yield return signature.SimpleReference;
			yield break;
		}

		if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (type, out var jniName)) {
			yield return jniName;
			yield break;
		}

		// Walk the base type chain for managed-only subclasses (e.g., JavaProxyThrowable
		// extends Java.Lang.Error but has no [Register] attribute itself).
		for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType) {
			if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (baseType, out var baseJniName)) {
				yield return baseJniName;
				yield break;
			}
		}
	}

	[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes | DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
	[UnconditionalSuppressMessage ("Trimming", "IL2063", Justification = "Trimmable typemap target types are generated from preserved Java peer metadata.")]
	protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
	{
		if (TryGetBuiltInTypeForSimpleReference (jniSimpleReference, out var type)) {
			return type;
		}

		return TrimmableTypeMap.Instance.TryGetTargetTypes (jniSimpleReference, out var types) && types.Length > 0
			? types [0]
			: null;
	}

	public override IEnumerable<Type> GetTypes (JniTypeSignature typeSignature)
	{
		if (!typeSignature.IsValid || typeSignature.SimpleReference == null)
			return [];
		return CreateGetTypesEnumerator (typeSignature);
	}

	IEnumerable<Type> CreateGetTypesEnumerator (JniTypeSignature typeSignature)
	{
		if (!typeSignature.IsValid || typeSignature.SimpleReference == null)
			yield break;

		foreach (var type in GetTypesForSimpleReference (typeSignature.SimpleReference)) {
			if (typeSignature.ArrayRank == 0) {
				yield return type;
				continue;
			}

			Type arrayElementType = type;
			for (int i = 1; i < typeSignature.ArrayRank; i++) {
				arrayElementType = MakeArrayType (arrayElementType);
			}

			if (TrimmableTypeMap.Instance.TryGetArrayType (arrayElementType, out var arrayType)) {
				yield return arrayType;
				continue;
			}

			if (IsKeywordSimpleReference (typeSignature.SimpleReference) || type == typeof (string)) {
				yield return MakeArrayType (arrayElementType);
			}
		}
	}

	public override IEnumerable<ReflectionConstructibleType> GetReflectionConstructibleTypes (JniTypeSignature typeSignature)
	{
		yield break;
	}

	[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
	protected override Type? GetInvokerTypeCore (
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			Type type)
	{
		return TrimmableTypeMap.Instance.GetInvokerType (type);
	}

	protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
	{
		return JniRemappingLookup.GetStaticMethodFallbackTypes (jniSimpleReference, useReplacementTypes: true);
	}

	protected override string? GetReplacementTypeCore (string jniSimpleReference)
	{
		return JniRemappingLookup.GetReplacementType (jniSimpleReference);
	}

	protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
	{
		return JniRemappingLookup.GetReplacementMethodInfo (jniSourceType, jniMethodName, jniMethodSignature);
	}

	public override void RegisterNativeMembers (
			JniType nativeClass,
			[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
			Type type,
			ReadOnlySpan<char> methods)
	{
		throw new UnreachableException (
			$"RegisterNativeMembers should not be called in the trimmable typemap path. " +
			$"Native methods for '{type.FullName}' should be registered by JCW static initializer blocks.");
	}

	[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>)")]
	public override void RegisterNativeMembers (
			JniType nativeClass,
			[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
			Type type,
			string? methods)
	{
		RegisterNativeMembers (nativeClass, type, methods.AsSpan ());
	}

	static bool TryGetBuiltInTypeSignature (Type type, out JniTypeSignature signature)
	{
		switch (Type.GetTypeCode (type)) {
			case TypeCode.String:
				signature = new JniTypeSignature ("java/lang/String");
				return true;
			case TypeCode.Boolean:
				signature = new JniTypeSignature ("Z", 0, keyword: true);
				return true;
			case TypeCode.Byte:
			case TypeCode.SByte:
				signature = new JniTypeSignature ("B", 0, keyword: true);
				return true;
			case TypeCode.Char:
				signature = new JniTypeSignature ("C", 0, keyword: true);
				return true;
			case TypeCode.UInt16:
			case TypeCode.Int16:
				signature = new JniTypeSignature ("S", 0, keyword: true);
				return true;
			case TypeCode.UInt32:
			case TypeCode.Int32:
				signature = new JniTypeSignature ("I", 0, keyword: true);
				return true;
			case TypeCode.UInt64:
			case TypeCode.Int64:
				signature = new JniTypeSignature ("J", 0, keyword: true);
				return true;
			case TypeCode.Single:
				signature = new JniTypeSignature ("F", 0, keyword: true);
				return true;
			case TypeCode.Double:
				signature = new JniTypeSignature ("D", 0, keyword: true);
				return true;
		}

		if (type == typeof (void)) {
			signature = new JniTypeSignature ("V", 0, keyword: true);
			return true;
		}

		if (type == typeof (Boolean?)) {
			signature = new JniTypeSignature ("java/lang/Boolean");
			return true;
		}
		if (type == typeof (SByte?)) {
			signature = new JniTypeSignature ("java/lang/Byte");
			return true;
		}
		if (type == typeof (Char?)) {
			signature = new JniTypeSignature ("java/lang/Character");
			return true;
		}
		if (type == typeof (Int16?)) {
			signature = new JniTypeSignature ("java/lang/Short");
			return true;
		}
		if (type == typeof (Int32?)) {
			signature = new JniTypeSignature ("java/lang/Integer");
			return true;
		}
		if (type == typeof (Int64?)) {
			signature = new JniTypeSignature ("java/lang/Long");
			return true;
		}
		if (type == typeof (Single?)) {
			signature = new JniTypeSignature ("java/lang/Float");
			return true;
		}
		if (type == typeof (Double?)) {
			signature = new JniTypeSignature ("java/lang/Double");
			return true;
		}

		signature = default;
		return false;
	}

	static bool TryGetBuiltInTypeForSimpleReference (
			string jniSimpleReference,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes | DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			[NotNullWhen (true)] out Type? type)
	{
		type = jniSimpleReference switch {
			"java/lang/String"     => typeof (string),
			"V"                    => typeof (void),
			"Z"                    => typeof (Boolean),
			"java/lang/Boolean"    => typeof (Boolean?),
			"B"                    => typeof (SByte),
			"java/lang/Byte"       => typeof (SByte?),
			"C"                    => typeof (Char),
			"java/lang/Character"  => typeof (Char?),
			"S"                    => typeof (Int16),
			"java/lang/Short"      => typeof (Int16?),
			"I"                    => typeof (Int32),
			"java/lang/Integer"    => typeof (Int32?),
			"J"                    => typeof (Int64),
			"java/lang/Long"       => typeof (Int64?),
			"F"                    => typeof (Single),
			"java/lang/Float"      => typeof (Single?),
			"D"                    => typeof (Double),
			"java/lang/Double"     => typeof (Double?),
			_                      => null,
		};
		return type != null;
	}

	static Type MakeArrayType (Type elementType)
	{
#pragma warning disable IL3050 // Trimmable typemap emits concrete array types; fallback arrays are runtime intrinsic.
		return elementType.MakeArrayType ();
#pragma warning restore IL3050
	}

	static bool IsKeywordSimpleReference (string simpleReference)
	{
		return simpleReference is "V" or "Z" or "B" or "C" or "S" or "I" or "J" or "F" or "D";
	}
}
