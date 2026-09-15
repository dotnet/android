#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

using Android.Runtime;

#pragma warning disable IL2026 // TypeMapAttribute entries are interpreted conditionally by the trimmer.
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Boolean]", typeof (Java.Interop.BooleanJavaListConverter), typeof (IList<bool>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Boolean]", typeof (Java.Interop.BooleanJavaListConverter), typeof (JavaList<bool>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Byte]", typeof (Java.Interop.ByteJavaListConverter), typeof (IList<byte>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Byte]", typeof (Java.Interop.ByteJavaListConverter), typeof (JavaList<byte>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.SByte]", typeof (Java.Interop.SByteJavaListConverter), typeof (IList<sbyte>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.SByte]", typeof (Java.Interop.SByteJavaListConverter), typeof (JavaList<sbyte>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Char]", typeof (Java.Interop.CharJavaListConverter), typeof (IList<char>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Char]", typeof (Java.Interop.CharJavaListConverter), typeof (JavaList<char>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Int16]", typeof (Java.Interop.Int16JavaListConverter), typeof (IList<short>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Int16]", typeof (Java.Interop.Int16JavaListConverter), typeof (JavaList<short>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Int32]", typeof (Java.Interop.Int32JavaListConverter), typeof (IList<int>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Int32]", typeof (Java.Interop.Int32JavaListConverter), typeof (JavaList<int>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Int64]", typeof (Java.Interop.Int64JavaListConverter), typeof (IList<long>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Int64]", typeof (Java.Interop.Int64JavaListConverter), typeof (JavaList<long>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Single]", typeof (Java.Interop.SingleJavaListConverter), typeof (IList<float>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Single]", typeof (Java.Interop.SingleJavaListConverter), typeof (JavaList<float>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Double]", typeof (Java.Interop.DoubleJavaListConverter), typeof (IList<double>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Double]", typeof (Java.Interop.DoubleJavaListConverter), typeof (JavaList<double>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Nullable`1[System.Boolean]]", typeof (Java.Interop.NullableBooleanJavaListConverter), typeof (IList<bool?>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Nullable`1[System.Boolean]]", typeof (Java.Interop.NullableBooleanJavaListConverter), typeof (JavaList<bool?>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Nullable`1[System.Byte]]", typeof (Java.Interop.NullableByteJavaListConverter), typeof (IList<byte?>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Nullable`1[System.Byte]]", typeof (Java.Interop.NullableByteJavaListConverter), typeof (JavaList<byte?>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Nullable`1[System.SByte]]", typeof (Java.Interop.NullableSByteJavaListConverter), typeof (IList<sbyte?>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Nullable`1[System.SByte]]", typeof (Java.Interop.NullableSByteJavaListConverter), typeof (JavaList<sbyte?>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Nullable`1[System.Char]]", typeof (Java.Interop.NullableCharJavaListConverter), typeof (IList<char?>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Nullable`1[System.Char]]", typeof (Java.Interop.NullableCharJavaListConverter), typeof (JavaList<char?>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Nullable`1[System.Int16]]", typeof (Java.Interop.NullableInt16JavaListConverter), typeof (IList<short?>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Nullable`1[System.Int16]]", typeof (Java.Interop.NullableInt16JavaListConverter), typeof (JavaList<short?>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Nullable`1[System.Int32]]", typeof (Java.Interop.NullableInt32JavaListConverter), typeof (IList<int?>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Nullable`1[System.Int32]]", typeof (Java.Interop.NullableInt32JavaListConverter), typeof (JavaList<int?>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Nullable`1[System.Int64]]", typeof (Java.Interop.NullableInt64JavaListConverter), typeof (IList<long?>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Nullable`1[System.Int64]]", typeof (Java.Interop.NullableInt64JavaListConverter), typeof (JavaList<long?>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Nullable`1[System.Single]]", typeof (Java.Interop.NullableSingleJavaListConverter), typeof (IList<float?>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Nullable`1[System.Single]]", typeof (Java.Interop.NullableSingleJavaListConverter), typeof (JavaList<float?>))]
[assembly: TypeMap<JavaList> ("System.Collections.Generic.IList`1[System.Nullable`1[System.Double]]", typeof (Java.Interop.NullableDoubleJavaListConverter), typeof (IList<double?>))]
[assembly: TypeMap<JavaList> ("Android.Runtime.JavaList`1[System.Nullable`1[System.Double]]", typeof (Java.Interop.NullableDoubleJavaListConverter), typeof (JavaList<double?>))]
#pragma warning restore IL2026

namespace Java.Interop;

static class ValueTypeListFactory
{
	static readonly IReadOnlyDictionary<string, Type> TypeMap = CreateTypeMap ();

	internal static bool TryGetFromJniHandleConverter (
		Type targetType,
		[NotNullWhen (true)] out Func<IntPtr, JniHandleOwnership, object?>? converter)
	{
		if (TypeMap.TryGetValue (targetType.ToString (), out var converterType)) {
			var listConverter = converterType.GetCustomAttribute<JavaListConverter> (inherit: false);
			if (listConverter == null) {
				throw new InvalidOperationException ($"Value-type list converter '{converterType}' is missing its self-applied converter attribute.");
			}
			converter = (handle, transfer) => handle == IntPtr.Zero ? null : listConverter.CreateInstance (handle, transfer);
			return true;
		}

		converter = null;
		return false;
	}

	[UnconditionalSuppressMessage ("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "The trimmable typemap build treats this fully-instantiated TypeMapping call as an intrinsic.")]
	static IReadOnlyDictionary<string, Type> CreateTypeMap ()
	{
		return TypeMapping.GetOrCreateExternalTypeMapping<JavaList> ();
	}
}

[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
file abstract class JavaListConverter : Attribute
{
	public abstract IList CreateInstance (IntPtr handle, JniHandleOwnership transfer);
}

file sealed class JavaListConverter<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] T>
{
	public static IList CreateInstance (IntPtr handle, JniHandleOwnership transfer)
	{
		return new JavaList<T> (handle, transfer);
	}
}

[BooleanJavaListConverter] file sealed class BooleanJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<bool>.CreateInstance (handle, transfer); }
[ByteJavaListConverter] file sealed class ByteJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<byte>.CreateInstance (handle, transfer); }
[SByteJavaListConverter] file sealed class SByteJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<sbyte>.CreateInstance (handle, transfer); }
[CharJavaListConverter] file sealed class CharJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<char>.CreateInstance (handle, transfer); }
[Int16JavaListConverter] file sealed class Int16JavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<short>.CreateInstance (handle, transfer); }
[Int32JavaListConverter] file sealed class Int32JavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<int>.CreateInstance (handle, transfer); }
[Int64JavaListConverter] file sealed class Int64JavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<long>.CreateInstance (handle, transfer); }
[SingleJavaListConverter] file sealed class SingleJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<float>.CreateInstance (handle, transfer); }
[DoubleJavaListConverter] file sealed class DoubleJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<double>.CreateInstance (handle, transfer); }
[NullableBooleanJavaListConverter] file sealed class NullableBooleanJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<bool?>.CreateInstance (handle, transfer); }
[NullableByteJavaListConverter] file sealed class NullableByteJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<byte?>.CreateInstance (handle, transfer); }
[NullableSByteJavaListConverter] file sealed class NullableSByteJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<sbyte?>.CreateInstance (handle, transfer); }
[NullableCharJavaListConverter] file sealed class NullableCharJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<char?>.CreateInstance (handle, transfer); }
[NullableInt16JavaListConverter] file sealed class NullableInt16JavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<short?>.CreateInstance (handle, transfer); }
[NullableInt32JavaListConverter] file sealed class NullableInt32JavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<int?>.CreateInstance (handle, transfer); }
[NullableInt64JavaListConverter] file sealed class NullableInt64JavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<long?>.CreateInstance (handle, transfer); }
[NullableSingleJavaListConverter] file sealed class NullableSingleJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<float?>.CreateInstance (handle, transfer); }
[NullableDoubleJavaListConverter] file sealed class NullableDoubleJavaListConverter : JavaListConverter { public override IList CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaListConverter<double?>.CreateInstance (handle, transfer); }
