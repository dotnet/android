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
	readonly ConcurrentDictionary<Type, JniTypeSignature> _typeSignatureCache = new ();

	protected override JniTypeSignature GetTypeSignatureCore (Type type)
	{
		return _typeSignatureCache.GetOrAdd (type, GetTypeSignatureUncached);

		static JniTypeSignature GetTypeSignatureUncached (Type type)
		{
			type = GetUnderlyingType (type, out int rank);

			if (TryGetBuiltInTypeSignature (type, out var signature)) {
				return signature.AddArrayRank (rank);
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

				if (GetPrimitiveArrayWrapperKeywordTypeName (type) is string primitiveArrayKeywordTypeName) {
					signature = new JniTypeSignature (primitiveArrayKeywordTypeName, 1, keyword: true);
					return true;
				}

				static string? GetPrimitiveArrayWrapperKeywordTypeName (Type type)
				{
					if (type == typeof (JavaBooleanArray) || type == typeof (JavaPrimitiveArray<bool>))
						return "Z";
					if (type == typeof (JavaSByteArray) || type == typeof (JavaPrimitiveArray<sbyte>))
						return "B";
					if (type == typeof (JavaCharArray) || type == typeof (JavaPrimitiveArray<char>))
						return "C";
					if (type == typeof (JavaInt16Array) || type == typeof (JavaPrimitiveArray<short>))
						return "S";
					if (type == typeof (JavaInt32Array) || type == typeof (JavaPrimitiveArray<int>))
						return "I";
					if (type == typeof (JavaInt64Array) || type == typeof (JavaPrimitiveArray<long>))
						return "J";
					if (type == typeof (JavaSingleArray) || type == typeof (JavaPrimitiveArray<float>))
						return "F";
					if (type == typeof (JavaDoubleArray) || type == typeof (JavaPrimitiveArray<double>))
						return "D";
					return null;
				}

				signature = default;
				return false;
			}
		}
	}

	[return: DynamicallyAccessedMembers (Methods | Constructors | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
	protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
	{
		var builtInType = jniSimpleReference switch {
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

		if (builtInType is not null) {
			return builtInType;
		}

		return TrimmableTypeMap.Instance.TryGetTargetTypes (jniSimpleReference, out var types) && types.Length > 0
			? types [0].Type
			: null;
	}

	[return: DynamicallyAccessedMembers (Constructors)]
	protected override Type? GetInvokerTypeCore ([DynamicallyAccessedMembers (Constructors)] Type type)
		// => TrimmableTypeMap.Instance.GetInvokerType (type);
		=> throw new UnreachableException (
			$"{nameof (GetInvokerTypeCore)} should not be called in the trimmable typemap path. " +
			$"Invoker types should use generated {nameof (JavaPeerProxy)} instances.");

	protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
		=> JniRemappingLookup.GetStaticMethodFallbackTypes (jniSimpleReference, useReplacementTypes: true);

	protected override string? GetReplacementTypeCore (string jniSimpleReference)
		=> JniRemappingLookup.GetReplacementType (jniSimpleReference);

	protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
		=> JniRemappingLookup.GetReplacementMethodInfo (jniSourceType, jniMethodName, jniMethodSignature);

	protected override string? GetSimpleReference (Type type)
	// {
	// 	var typeSignature = GetTypeSignature (type);
	// 	return typeSignature.IsValid ? typeSignature.SimpleReference : null;
	// }
		=> throw new UnreachableException (
			$"{nameof (GetSimpleReference)} should not be called in the trimmable typemap path. " +
			$"Simple reference lookup should use {nameof (GetTypeSignatureCore)} to get the full type signature, including simple reference.");

	protected override IEnumerable<string> GetSimpleReferences (Type type)
	// {
	// 	var simpleReference = GetSimpleReference (type);
	// 	return simpleReference is not null ? [simpleReference] : [];
	// }
		=> throw new UnreachableException (
			$"{nameof (GetSimpleReferences)} should not be called in the trimmable typemap path. " +
			$"Simple reference lookup should use {nameof (GetTypeSignatureCore)} to get the full type signature, including simple reference.");

	public override IEnumerable<Type> GetTypes (JniTypeSignature typeSignature)
		=> throw new UnreachableException (
			$"{nameof (GetTypes)} should not be called in the trimmable typemap path. " +
			$"Java-to-managed constructor activation should use generated {nameof (JavaPeerProxy)} instances.");

	public override IEnumerable<ReflectionConstructibleType> GetReflectionConstructibleTypes (JniTypeSignature typeSignature)
		=> throw new UnreachableException (
			$"{nameof (GetReflectionConstructibleTypes)} should not be called in the trimmable typemap path. " +
			$"Managed peer construction should use generated {nameof (JavaPeerProxy)} instances.");

	protected override IEnumerable<JniTypeSignature> GetTypeSignaturesCore (Type type)
		=> throw new UnreachableException (
			$"{nameof (GetTypeSignaturesCore)} should not be called in the trimmable typemap path. " +
			$"Runtime type signature lookup should use {nameof (GetTypeSignatureCore)}.");

	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
		=> throw new UnreachableException (
			$"{nameof (GetTypesForSimpleReference)} should not be called in the trimmable typemap path. " +
			$"Simple reference lookup should use {nameof (GetTypeForSimpleReference)}.");

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
