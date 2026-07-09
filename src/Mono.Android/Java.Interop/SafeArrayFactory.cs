#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Android.Runtime;

namespace Java.Interop {
	static class SafeArrayFactory
	{
		static readonly Dictionary<Type, ArrayFactoryInfo> ValueTypeArrayFactories = new Dictionary<Type, ArrayFactoryInfo> {
			{ typeof (bool),    new ArrayFactoryInfo (typeof (bool[]),    static length => new bool [length]) },
			{ typeof (sbyte),   new ArrayFactoryInfo (typeof (sbyte[]),   static length => new sbyte [length]) },
			{ typeof (char),    new ArrayFactoryInfo (typeof (char[]),    static length => new char [length]) },
			{ typeof (short),   new ArrayFactoryInfo (typeof (short[]),   static length => new short [length]) },
			{ typeof (int),     new ArrayFactoryInfo (typeof (int[]),     static length => new int [length]) },
			{ typeof (long),    new ArrayFactoryInfo (typeof (long[]),    static length => new long [length]) },
			{ typeof (float),   new ArrayFactoryInfo (typeof (float[]),   static length => new float [length]) },
			{ typeof (double),  new ArrayFactoryInfo (typeof (double[]),  static length => new double [length]) },
			{ typeof (bool?),   new ArrayFactoryInfo (typeof (bool?[]),   static length => new bool? [length]) },
			{ typeof (sbyte?),  new ArrayFactoryInfo (typeof (sbyte?[]),  static length => new sbyte? [length]) },
			{ typeof (char?),   new ArrayFactoryInfo (typeof (char?[]),   static length => new char? [length]) },
			{ typeof (short?),  new ArrayFactoryInfo (typeof (short?[]),  static length => new short? [length]) },
			{ typeof (int?),    new ArrayFactoryInfo (typeof (int?[]),    static length => new int? [length]) },
			{ typeof (long?),   new ArrayFactoryInfo (typeof (long?[]),   static length => new long? [length]) },
			{ typeof (float?),  new ArrayFactoryInfo (typeof (float?[]),  static length => new float? [length]) },
			{ typeof (double?), new ArrayFactoryInfo (typeof (double?[]), static length => new double? [length]) },
		};

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

			if (rank == 1 && ValueTypeArrayFactories.TryGetValue (elementType, out var factory)) {
				array = factory.CreateInstance (length);
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
				if (ValueTypeArrayFactories.TryGetValue (elementType, out var factory)) {
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
			Justification = "Reference-type arrays use NativeAOT canonical array support. Value-type vectors are returned from explicit rooted maps before this helper is used.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2055:MakeGenericType",
			Justification = "Creating an SZ array type does not require member discovery, and value-type vectors are returned from explicit rooted maps.")]
		static Type MakeReferenceArrayType (Type elementType)
		{
			return elementType.MakeArrayType ();
		}

		[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
			Justification = "The array type is produced by SafeArrayFactory: reference arrays use canonical support, and value-type vectors are explicitly rooted.")]
		static Array CreateInstanceFromArrayType (Type arrayType, int length)
		{
			return Array.CreateInstanceFromArrayType (arrayType, length);
		}

		static NotSupportedException CreateUnsupportedArrayException (Type elementType, int rank)
		{
			return new NotSupportedException (
				$"The array type for element type '{elementType}' and rank '{rank}' is not available for AOT-safe creation.");
		}

		sealed class ArrayFactoryInfo
		{
			public ArrayFactoryInfo (Type arrayType, Func<int, Array> createInstance)
			{
				ArrayType = arrayType;
				CreateInstance = createInstance;
			}

			public Type ArrayType { get; }

			public Func<int, Array> CreateInstance { get; }
		}
	}
}
