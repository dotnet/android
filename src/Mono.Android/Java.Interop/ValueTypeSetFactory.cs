#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

using Android.Runtime;

using Java.Interop;

// ICollection<T> intentionally remains in the JavaCollection universe. The target interface does not
// distinguish a Java Set from any other Collection, so selecting JavaSet from the runtime object would
// make interface conversion source-dependent instead of preserving the existing target-driven behavior.
#pragma warning disable IL2026 // TypeMapAttribute entries are interpreted conditionally by the trimmer.
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Boolean]", typeof (Java.Interop.BooleanJavaSetConverter), typeof (JavaSet<bool>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Byte]", typeof (Java.Interop.ByteJavaSetConverter), typeof (JavaSet<byte>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.SByte]", typeof (Java.Interop.SByteJavaSetConverter), typeof (JavaSet<sbyte>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Char]", typeof (Java.Interop.CharJavaSetConverter), typeof (JavaSet<char>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Int16]", typeof (Java.Interop.Int16JavaSetConverter), typeof (JavaSet<short>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Int32]", typeof (Java.Interop.Int32JavaSetConverter), typeof (JavaSet<int>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Int64]", typeof (Java.Interop.Int64JavaSetConverter), typeof (JavaSet<long>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Single]", typeof (Java.Interop.SingleJavaSetConverter), typeof (JavaSet<float>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Double]", typeof (Java.Interop.DoubleJavaSetConverter), typeof (JavaSet<double>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Nullable`1[System.Boolean]]", typeof (Java.Interop.NullableBooleanJavaSetConverter), typeof (JavaSet<bool?>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Nullable`1[System.Byte]]", typeof (Java.Interop.NullableByteJavaSetConverter), typeof (JavaSet<byte?>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Nullable`1[System.SByte]]", typeof (Java.Interop.NullableSByteJavaSetConverter), typeof (JavaSet<sbyte?>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Nullable`1[System.Char]]", typeof (Java.Interop.NullableCharJavaSetConverter), typeof (JavaSet<char?>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Nullable`1[System.Int16]]", typeof (Java.Interop.NullableInt16JavaSetConverter), typeof (JavaSet<short?>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Nullable`1[System.Int32]]", typeof (Java.Interop.NullableInt32JavaSetConverter), typeof (JavaSet<int?>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Nullable`1[System.Int64]]", typeof (Java.Interop.NullableInt64JavaSetConverter), typeof (JavaSet<long?>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Nullable`1[System.Single]]", typeof (Java.Interop.NullableSingleJavaSetConverter), typeof (JavaSet<float?>))]
[assembly: TypeMap<JavaSet> ("Android.Runtime.JavaSet`1[System.Nullable`1[System.Double]]", typeof (Java.Interop.NullableDoubleJavaSetConverter), typeof (JavaSet<double?>))]
#pragma warning restore IL2026

namespace Java.Interop;

static class ValueTypeSetFactory
{
	static readonly IReadOnlyDictionary<string, Type> TypeMap = CreateTypeMap ();

	internal static bool TryGetFromJniHandleConverter (
		Type targetType,
		[NotNullWhen (true)] out Func<IntPtr, JniHandleOwnership, object?>? converter)
	{
		if (TypeMap.TryGetValue (targetType.ToString (), out var converterType)) {
			var setConverter = converterType.GetCustomAttribute<JavaSetConverter> (inherit: false);
			if (setConverter == null) {
				throw new InvalidOperationException ($"Value-type set converter '{converterType}' is missing its self-applied converter attribute.");
			}
			converter = (handle, transfer) => handle == IntPtr.Zero ? null : setConverter.CreateInstance (handle, transfer);
			return true;
		}

		converter = null;
		return false;
	}

	[UnconditionalSuppressMessage ("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "The trimmable typemap build treats this fully-instantiated TypeMapping call as an intrinsic.")]
	static IReadOnlyDictionary<string, Type> CreateTypeMap ()
	{
		return TypeMapping.GetOrCreateExternalTypeMapping<JavaSet> ();
	}
}

[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
file abstract class JavaSetConverter : Attribute
{
	public abstract JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer);
}

file sealed class JavaSetConverter<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] T>
{
	public static JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer)
	{
		return new JavaSet<T> (handle, transfer);
	}
}

[BooleanJavaSetConverter] file sealed class BooleanJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<bool>.CreateInstance (handle, transfer); }
[ByteJavaSetConverter] file sealed class ByteJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<byte>.CreateInstance (handle, transfer); }
[SByteJavaSetConverter] file sealed class SByteJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<sbyte>.CreateInstance (handle, transfer); }
[CharJavaSetConverter] file sealed class CharJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<char>.CreateInstance (handle, transfer); }
[Int16JavaSetConverter] file sealed class Int16JavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<short>.CreateInstance (handle, transfer); }
[Int32JavaSetConverter] file sealed class Int32JavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<int>.CreateInstance (handle, transfer); }
[Int64JavaSetConverter] file sealed class Int64JavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<long>.CreateInstance (handle, transfer); }
[SingleJavaSetConverter] file sealed class SingleJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<float>.CreateInstance (handle, transfer); }
[DoubleJavaSetConverter] file sealed class DoubleJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<double>.CreateInstance (handle, transfer); }
[NullableBooleanJavaSetConverter] file sealed class NullableBooleanJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<bool?>.CreateInstance (handle, transfer); }
[NullableByteJavaSetConverter] file sealed class NullableByteJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<byte?>.CreateInstance (handle, transfer); }
[NullableSByteJavaSetConverter] file sealed class NullableSByteJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<sbyte?>.CreateInstance (handle, transfer); }
[NullableCharJavaSetConverter] file sealed class NullableCharJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<char?>.CreateInstance (handle, transfer); }
[NullableInt16JavaSetConverter] file sealed class NullableInt16JavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<short?>.CreateInstance (handle, transfer); }
[NullableInt32JavaSetConverter] file sealed class NullableInt32JavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<int?>.CreateInstance (handle, transfer); }
[NullableInt64JavaSetConverter] file sealed class NullableInt64JavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<long?>.CreateInstance (handle, transfer); }
[NullableSingleJavaSetConverter] file sealed class NullableSingleJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<float?>.CreateInstance (handle, transfer); }
[NullableDoubleJavaSetConverter] file sealed class NullableDoubleJavaSetConverter : JavaSetConverter { public override JavaSet CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaSetConverter<double?>.CreateInstance (handle, transfer); }
