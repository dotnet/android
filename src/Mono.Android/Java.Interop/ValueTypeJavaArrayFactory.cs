#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

using Android.Runtime;

// IList<T> intentionally remains in the JavaList universe. A Java array and a java.util.List both
// implement the target interface, so selecting the wrapper from the runtime source would make
// interface conversion source-dependent instead of preserving the existing target-driven behavior.
#pragma warning disable IL2026 // TypeMapAttribute entries are interpreted conditionally by the trimmer.
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Boolean]", typeof (Java.Interop.BooleanJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<bool>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Byte]", typeof (Java.Interop.ByteJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<byte>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.SByte]", typeof (Java.Interop.SByteJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<sbyte>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Char]", typeof (Java.Interop.CharJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<char>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Int16]", typeof (Java.Interop.Int16JavaArrayConverter), typeof (global::Android.Runtime.JavaArray<short>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Int32]", typeof (Java.Interop.Int32JavaArrayConverter), typeof (global::Android.Runtime.JavaArray<int>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Int64]", typeof (Java.Interop.Int64JavaArrayConverter), typeof (global::Android.Runtime.JavaArray<long>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Single]", typeof (Java.Interop.SingleJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<float>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Double]", typeof (Java.Interop.DoubleJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<double>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Nullable`1[System.Boolean]]", typeof (Java.Interop.NullableBooleanJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<bool?>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Nullable`1[System.Byte]]", typeof (Java.Interop.NullableByteJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<byte?>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Nullable`1[System.SByte]]", typeof (Java.Interop.NullableSByteJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<sbyte?>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Nullable`1[System.Char]]", typeof (Java.Interop.NullableCharJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<char?>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Nullable`1[System.Int16]]", typeof (Java.Interop.NullableInt16JavaArrayConverter), typeof (global::Android.Runtime.JavaArray<short?>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Nullable`1[System.Int32]]", typeof (Java.Interop.NullableInt32JavaArrayConverter), typeof (global::Android.Runtime.JavaArray<int?>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Nullable`1[System.Int64]]", typeof (Java.Interop.NullableInt64JavaArrayConverter), typeof (global::Android.Runtime.JavaArray<long?>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Nullable`1[System.Single]]", typeof (Java.Interop.NullableSingleJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<float?>))]
[assembly: TypeMap<global::Android.Runtime.JavaArray> ("Android.Runtime.JavaArray`1[System.Nullable`1[System.Double]]", typeof (Java.Interop.NullableDoubleJavaArrayConverter), typeof (global::Android.Runtime.JavaArray<double?>))]
#pragma warning restore IL2026

namespace Java.Interop;

static class ValueTypeJavaArrayFactory
{
	static readonly IReadOnlyDictionary<string, Type> TypeMap = CreateTypeMap ();

	internal static bool TryGetFromJniHandleConverter (
		Type targetType,
		[NotNullWhen (true)] out Func<IntPtr, JniHandleOwnership, IJavaPeerable?>? converter)
	{
		if (TypeMap.TryGetValue (targetType.ToString (), out var converterType)) {
			var arrayConverter = converterType.GetCustomAttribute<JavaArrayConverter> (inherit: false);
			if (arrayConverter == null) {
				throw new InvalidOperationException ($"Value-type JavaArray converter '{converterType}' is missing its self-applied converter attribute.");
			}
			converter = (handle, transfer) => handle == IntPtr.Zero ? null : arrayConverter.CreateInstance (handle, transfer);
			return true;
		}

		converter = null;
		return false;
	}

	[UnconditionalSuppressMessage ("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "The trimmable typemap build treats this fully-instantiated TypeMapping call as an intrinsic.")]
	static IReadOnlyDictionary<string, Type> CreateTypeMap ()
	{
		return TypeMapping.GetOrCreateExternalTypeMapping<global::Android.Runtime.JavaArray> ();
	}
}

[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
file abstract class JavaArrayConverter : Attribute
{
	public abstract IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer);
}

file sealed class JavaArrayConverter<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] T>
{
	public static IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer)
	{
		return new global::Android.Runtime.JavaArray<T> (handle, transfer);
	}
}

[BooleanJavaArrayConverter] file sealed class BooleanJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<bool>.CreateInstance (handle, transfer); }
[ByteJavaArrayConverter] file sealed class ByteJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<byte>.CreateInstance (handle, transfer); }
[SByteJavaArrayConverter] file sealed class SByteJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<sbyte>.CreateInstance (handle, transfer); }
[CharJavaArrayConverter] file sealed class CharJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<char>.CreateInstance (handle, transfer); }
[Int16JavaArrayConverter] file sealed class Int16JavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<short>.CreateInstance (handle, transfer); }
[Int32JavaArrayConverter] file sealed class Int32JavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<int>.CreateInstance (handle, transfer); }
[Int64JavaArrayConverter] file sealed class Int64JavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<long>.CreateInstance (handle, transfer); }
[SingleJavaArrayConverter] file sealed class SingleJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<float>.CreateInstance (handle, transfer); }
[DoubleJavaArrayConverter] file sealed class DoubleJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<double>.CreateInstance (handle, transfer); }
[NullableBooleanJavaArrayConverter] file sealed class NullableBooleanJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<bool?>.CreateInstance (handle, transfer); }
[NullableByteJavaArrayConverter] file sealed class NullableByteJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<byte?>.CreateInstance (handle, transfer); }
[NullableSByteJavaArrayConverter] file sealed class NullableSByteJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<sbyte?>.CreateInstance (handle, transfer); }
[NullableCharJavaArrayConverter] file sealed class NullableCharJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<char?>.CreateInstance (handle, transfer); }
[NullableInt16JavaArrayConverter] file sealed class NullableInt16JavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<short?>.CreateInstance (handle, transfer); }
[NullableInt32JavaArrayConverter] file sealed class NullableInt32JavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<int?>.CreateInstance (handle, transfer); }
[NullableInt64JavaArrayConverter] file sealed class NullableInt64JavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<long?>.CreateInstance (handle, transfer); }
[NullableSingleJavaArrayConverter] file sealed class NullableSingleJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<float?>.CreateInstance (handle, transfer); }
[NullableDoubleJavaArrayConverter] file sealed class NullableDoubleJavaArrayConverter : JavaArrayConverter { public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaArrayConverter<double?>.CreateInstance (handle, transfer); }
