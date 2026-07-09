#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Android.Runtime;

namespace Java.Interop {
	static class SafeArrayFactory
	{
		// NativeAOT can dynamically build SZArray EETypes from canonical templates when the element type
		// is a reference type. The path is RuntimeTypeInfo.MakeArrayType() ->
		// ExecutionEnvironment.TryGetArrayTypeForElementType() -> TypeLoaderEnvironment/TypeBuilder,
		// and StandardCanonicalizationAlgorithm canonicalizes reference DefTypes and arrays to __Canon.
		//
		// Value-type element arrays are different: value types do not collapse to __Canon, so an exact
		// vector EEType/template must be available. ValueTypeFactory roots typeof(T[]), new T[length],
		// and the matching Java collection wrapper instantiations in one shared primitive map.

		internal static Type GetArrayType (Type elementType, int rank)
		{
			if (TryGetArrayType (elementType, rank, out var arrayType)) {
				return arrayType;
			}

			throw CreateUnsupportedArrayException (elementType, rank);
		}

		internal static bool TryGetArrayType (Type elementType, int rank, [NotNullWhen (true)] out Type? arrayType)
		{
			ValidateRank (rank);

			if (elementType == null)
				throw new ArgumentNullException (nameof (elementType));

			if (!TryGetVectorType (elementType, out arrayType)) {
				arrayType = null;
				return false;
			}

			// Additional ranks here are jagged SZArrays, not multidimensional arrays. Once the first
			// vector is available, every extra wrapper is an array whose element is itself an array
			// type, i.e. a reference type, and follows NativeAOT's canonical reference-array path.
			for (int i = 1; i < rank; i++) {
				arrayType = MakeReferenceArrayType (arrayType);
			}

			return true;
		}

		internal static Array CreateInstance (Type elementType, int rank, int length)
		{
			if (TryCreateInstance (elementType, rank, length, out var array)) {
				return array;
			}

			throw CreateUnsupportedArrayException (elementType, rank);
		}

		internal static bool TryCreateInstance (Type elementType, int rank, int length, [NotNullWhen (true)] out Array? array)
		{
			ValidateRank (rank);
			ArgumentOutOfRangeException.ThrowIfNegative (length);

			if (elementType == null)
				throw new ArgumentNullException (nameof (elementType));

			if (rank == 1 && ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (elementType, out var factory)) {
				array = factory.CreateArray (length);
				return true;
			}

			if (TryGetArrayType (elementType, rank, out var arrayType)) {
				array = CreateInstanceFromArrayType (arrayType, length);
				return true;
			}

			if (RuntimeFeature.TrimmableTypeMap && RuntimeFeature.IsNativeAotRuntime &&
					TrimmableTypeMap.Instance.TryGetArrayProxy (elementType, rank, out var arrayProxy)) {
				array = arrayProxy.CreateManagedArray (length);
				return true;
			}

			array = null;
			return false;
		}

		static void ValidateRank (int rank)
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero (rank);
		}

		static bool TryGetVectorType (Type elementType, [NotNullWhen (true)] out Type? vectorType)
		{
			if (elementType.IsValueType) {
				if (ValueTypeFactory.PrimitiveTypeFactories.TryGetValue (elementType, out var factory)) {
					vectorType = factory.ArrayType;
					return true;
				}

				vectorType = null;
				return false;
			}

			vectorType = MakeReferenceArrayType (elementType);
			return true;
		}

		[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
			Justification = "NativeAOT's Type.MakeArrayType() is annotated because arbitrary element types can lack an array EEType/template. " +
				"This helper is only used for reference element types, or for already-created array types when adding jagged rank. " +
				"Those shapes canonicalize to __Canon/reference-array templates in NativeAOT's TypeBuilder. " +
				"First-rank value-type vectors are returned from explicit typeof(T[])/new T[length] maps before this helper is used.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2055:MakeGenericType",
			Justification = "Creating an SZArray type does not perform member discovery. " +
				"Value-type vectors are not passed here unless they are already array reference types for an outer jagged rank.")]
		static Type MakeReferenceArrayType (Type elementType)
		{
			return elementType.MakeArrayType ();
		}

		[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
			Justification = "Array.CreateInstanceFromArrayType() immediately asks for the array TypeHandle and allocates via RuntimeAugments.NewArray(). " +
				"SafeArrayFactory only passes NativeAOT-buildable reference-array canonical shapes here: either arrays whose original element type is a reference type, " +
				"or outer jagged array wrappers whose element is already an array reference type. " +
				"First-rank primitive/nullable value vectors are allocated directly by ValueTypeFactory<T>.CreateArray() before reaching this helper.")]
		static Array CreateInstanceFromArrayType (Type arrayType, int length)
		{
			return Array.CreateInstanceFromArrayType (arrayType, length);
		}

		static NotSupportedException CreateUnsupportedArrayException (Type elementType, int rank)
		{
			return new NotSupportedException (
				$"The array type for element type '{elementType}' and rank '{rank}' is not available for AOT-safe creation.");
		}
	}
}
