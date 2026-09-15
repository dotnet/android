#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

using Android.Runtime;

#pragma warning disable IL2026 // TypeMapAttribute entries are interpreted conditionally by the trimmer.
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Boolean]", typeof (Java.Interop.BooleanJavaCollectionConverter), typeof (ICollection<bool>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Boolean]", typeof (Java.Interop.BooleanJavaCollectionConverter), typeof (JavaCollection<bool>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Byte]", typeof (Java.Interop.ByteJavaCollectionConverter), typeof (ICollection<byte>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Byte]", typeof (Java.Interop.ByteJavaCollectionConverter), typeof (JavaCollection<byte>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.SByte]", typeof (Java.Interop.SByteJavaCollectionConverter), typeof (ICollection<sbyte>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.SByte]", typeof (Java.Interop.SByteJavaCollectionConverter), typeof (JavaCollection<sbyte>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Char]", typeof (Java.Interop.CharJavaCollectionConverter), typeof (ICollection<char>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Char]", typeof (Java.Interop.CharJavaCollectionConverter), typeof (JavaCollection<char>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Int16]", typeof (Java.Interop.Int16JavaCollectionConverter), typeof (ICollection<short>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Int16]", typeof (Java.Interop.Int16JavaCollectionConverter), typeof (JavaCollection<short>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Int32]", typeof (Java.Interop.Int32JavaCollectionConverter), typeof (ICollection<int>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Int32]", typeof (Java.Interop.Int32JavaCollectionConverter), typeof (JavaCollection<int>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Int64]", typeof (Java.Interop.Int64JavaCollectionConverter), typeof (ICollection<long>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Int64]", typeof (Java.Interop.Int64JavaCollectionConverter), typeof (JavaCollection<long>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Single]", typeof (Java.Interop.SingleJavaCollectionConverter), typeof (ICollection<float>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Single]", typeof (Java.Interop.SingleJavaCollectionConverter), typeof (JavaCollection<float>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Double]", typeof (Java.Interop.DoubleJavaCollectionConverter), typeof (ICollection<double>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Double]", typeof (Java.Interop.DoubleJavaCollectionConverter), typeof (JavaCollection<double>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Nullable`1[System.Boolean]]", typeof (Java.Interop.NullableBooleanJavaCollectionConverter), typeof (ICollection<bool?>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Nullable`1[System.Boolean]]", typeof (Java.Interop.NullableBooleanJavaCollectionConverter), typeof (JavaCollection<bool?>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Nullable`1[System.Byte]]", typeof (Java.Interop.NullableByteJavaCollectionConverter), typeof (ICollection<byte?>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Nullable`1[System.Byte]]", typeof (Java.Interop.NullableByteJavaCollectionConverter), typeof (JavaCollection<byte?>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Nullable`1[System.SByte]]", typeof (Java.Interop.NullableSByteJavaCollectionConverter), typeof (ICollection<sbyte?>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Nullable`1[System.SByte]]", typeof (Java.Interop.NullableSByteJavaCollectionConverter), typeof (JavaCollection<sbyte?>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Nullable`1[System.Char]]", typeof (Java.Interop.NullableCharJavaCollectionConverter), typeof (ICollection<char?>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Nullable`1[System.Char]]", typeof (Java.Interop.NullableCharJavaCollectionConverter), typeof (JavaCollection<char?>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Nullable`1[System.Int16]]", typeof (Java.Interop.NullableInt16JavaCollectionConverter), typeof (ICollection<short?>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Nullable`1[System.Int16]]", typeof (Java.Interop.NullableInt16JavaCollectionConverter), typeof (JavaCollection<short?>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Nullable`1[System.Int32]]", typeof (Java.Interop.NullableInt32JavaCollectionConverter), typeof (ICollection<int?>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Nullable`1[System.Int32]]", typeof (Java.Interop.NullableInt32JavaCollectionConverter), typeof (JavaCollection<int?>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Nullable`1[System.Int64]]", typeof (Java.Interop.NullableInt64JavaCollectionConverter), typeof (ICollection<long?>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Nullable`1[System.Int64]]", typeof (Java.Interop.NullableInt64JavaCollectionConverter), typeof (JavaCollection<long?>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Nullable`1[System.Single]]", typeof (Java.Interop.NullableSingleJavaCollectionConverter), typeof (ICollection<float?>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Nullable`1[System.Single]]", typeof (Java.Interop.NullableSingleJavaCollectionConverter), typeof (JavaCollection<float?>))]
[assembly: TypeMap<JavaCollection> ("System.Collections.Generic.ICollection`1[System.Nullable`1[System.Double]]", typeof (Java.Interop.NullableDoubleJavaCollectionConverter), typeof (ICollection<double?>))]
[assembly: TypeMap<JavaCollection> ("Android.Runtime.JavaCollection`1[System.Nullable`1[System.Double]]", typeof (Java.Interop.NullableDoubleJavaCollectionConverter), typeof (JavaCollection<double?>))]
#pragma warning restore IL2026

namespace Java.Interop;

static class ValueTypeCollectionFactory
{
	static readonly IReadOnlyDictionary<string, Type> TypeMap = CreateTypeMap ();

	internal static bool TryGetFromJniHandleConverter (
		Type targetType,
		[NotNullWhen (true)] out Func<IntPtr, JniHandleOwnership, object?>? converter)
	{
		if (TypeMap.TryGetValue (targetType.ToString (), out var converterType)) {
			var collectionConverter = converterType.GetCustomAttribute<JavaCollectionConverter> (inherit: false);
			if (collectionConverter == null) {
				throw new InvalidOperationException ($"Value-type collection converter '{converterType}' is missing its self-applied converter attribute.");
			}
			converter = (handle, transfer) => handle == IntPtr.Zero ? null : collectionConverter.CreateInstance (handle, transfer);
			return true;
		}

		converter = null;
		return false;
	}

	[UnconditionalSuppressMessage ("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "The trimmable typemap build treats this fully-instantiated TypeMapping call as an intrinsic.")]
	static IReadOnlyDictionary<string, Type> CreateTypeMap ()
	{
		return TypeMapping.GetOrCreateExternalTypeMapping<JavaCollection> ();
	}
}

[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
file abstract class JavaCollectionConverter : Attribute
{
	public abstract ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer);
}

file sealed class JavaCollectionConverter<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] T>
{
	public static ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer)
	{
		return new JavaCollection<T> (handle, transfer);
	}
}

[BooleanJavaCollectionConverter] file sealed class BooleanJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<bool>.CreateInstance (handle, transfer); }
[ByteJavaCollectionConverter] file sealed class ByteJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<byte>.CreateInstance (handle, transfer); }
[SByteJavaCollectionConverter] file sealed class SByteJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<sbyte>.CreateInstance (handle, transfer); }
[CharJavaCollectionConverter] file sealed class CharJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<char>.CreateInstance (handle, transfer); }
[Int16JavaCollectionConverter] file sealed class Int16JavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<short>.CreateInstance (handle, transfer); }
[Int32JavaCollectionConverter] file sealed class Int32JavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<int>.CreateInstance (handle, transfer); }
[Int64JavaCollectionConverter] file sealed class Int64JavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<long>.CreateInstance (handle, transfer); }
[SingleJavaCollectionConverter] file sealed class SingleJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<float>.CreateInstance (handle, transfer); }
[DoubleJavaCollectionConverter] file sealed class DoubleJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<double>.CreateInstance (handle, transfer); }
[NullableBooleanJavaCollectionConverter] file sealed class NullableBooleanJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<bool?>.CreateInstance (handle, transfer); }
[NullableByteJavaCollectionConverter] file sealed class NullableByteJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<byte?>.CreateInstance (handle, transfer); }
[NullableSByteJavaCollectionConverter] file sealed class NullableSByteJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<sbyte?>.CreateInstance (handle, transfer); }
[NullableCharJavaCollectionConverter] file sealed class NullableCharJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<char?>.CreateInstance (handle, transfer); }
[NullableInt16JavaCollectionConverter] file sealed class NullableInt16JavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<short?>.CreateInstance (handle, transfer); }
[NullableInt32JavaCollectionConverter] file sealed class NullableInt32JavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<int?>.CreateInstance (handle, transfer); }
[NullableInt64JavaCollectionConverter] file sealed class NullableInt64JavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<long?>.CreateInstance (handle, transfer); }
[NullableSingleJavaCollectionConverter] file sealed class NullableSingleJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<float?>.CreateInstance (handle, transfer); }
[NullableDoubleJavaCollectionConverter] file sealed class NullableDoubleJavaCollectionConverter : JavaCollectionConverter { public override ICollection CreateInstance (IntPtr handle, JniHandleOwnership transfer) => JavaCollectionConverter<double?>.CreateInstance (handle, transfer); }
