#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Android.Runtime;

namespace Java.Interop;

abstract class ValueTypeFactory
{
	// NativeAOT's MakeGenericType() and MakeArrayType() paths use canonical templates.
	// Reference types collapse to __Canon, but value types stay value-specific. This map
	// intentionally roots each primitive/nullable value shape through direct typeof(T),
	// typeof(T[]), new T[length], and Java collection wrapper constructor references.
	// `byte` is included alongside `sbyte` (both marshal to java.lang.Byte bitwise) so that
	// byte-element collections keep working on the trimmable path, matching the reflection paths.
	internal static readonly Dictionary<Type, ValueTypeFactory> PrimitiveTypeFactories = new () {
		{ typeof (bool),    new ValueTypeFactory<bool> () },
		{ typeof (byte),    new ValueTypeFactory<byte> () },
		{ typeof (sbyte),   new ValueTypeFactory<sbyte> () },
		{ typeof (char),    new ValueTypeFactory<char> () },
		{ typeof (short),   new ValueTypeFactory<short> () },
		{ typeof (int),     new ValueTypeFactory<int> () },
		{ typeof (long),    new ValueTypeFactory<long> () },
		{ typeof (float),   new ValueTypeFactory<float> () },
		{ typeof (double),  new ValueTypeFactory<double> () },
		{ typeof (bool?),   new ValueTypeFactory<bool?> () },
		{ typeof (byte?),   new ValueTypeFactory<byte?> () },
		{ typeof (sbyte?),  new ValueTypeFactory<sbyte?> () },
		{ typeof (char?),   new ValueTypeFactory<char?> () },
		{ typeof (short?),  new ValueTypeFactory<short?> () },
		{ typeof (int?),    new ValueTypeFactory<int?> () },
		{ typeof (long?),   new ValueTypeFactory<long?> () },
		{ typeof (float?),  new ValueTypeFactory<float?> () },
		{ typeof (double?), new ValueTypeFactory<double?> () },
	};

	public abstract Type ValueType { get; }

	public abstract Type ArrayType { get; }

	public abstract Array CreateArray (int length);

	internal abstract IList CreateList (IntPtr handle, JniHandleOwnership transfer);

	internal abstract ICollection CreateCollection (IntPtr handle, JniHandleOwnership transfer);

	internal abstract IDictionary CreateDictionary (ValueTypeFactory valueFactory, IntPtr handle, JniHandleOwnership transfer);

	internal abstract IDictionary CreateDictionaryWithReferenceKey (Type keyType, IntPtr handle, JniHandleOwnership transfer);

	internal abstract IDictionary CreateDictionaryWithReferenceValue (Type valueType, IntPtr handle, JniHandleOwnership transfer);

	internal abstract IDictionary CreateDictionaryWithKey<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] TKey> (
		ValueTypeFactory<TKey> keyFactory,
		IntPtr handle,
		JniHandleOwnership transfer);
}

sealed class ValueTypeFactory<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] T> : ValueTypeFactory
{
	// These tokens root the mixed reference/value dictionary canonical shapes. For example,
	// JavaDictionary<string,int> canonicalizes like JavaDictionary<__Canon,int>, so
	// JavaDictionary<object,T> supplies the NativeAOT template when T is a mapped value type.
	static readonly Type ReferenceKeyDictionaryType = typeof (JavaDictionary<object, T>);
	static readonly Type ReferenceValueDictionaryType = typeof (JavaDictionary<T, object>);

	internal ValueTypeFactory ()
	{
	}

	public override Type ValueType { get; } = typeof (T);

	public override Type ArrayType { get; } = typeof (T[]);

	public override Array CreateArray (int length)
	{
		return new T [length];
	}

	internal override IList CreateList (IntPtr handle, JniHandleOwnership transfer)
	{
		return new JavaList<T> (handle, transfer);
	}

	internal override ICollection CreateCollection (IntPtr handle, JniHandleOwnership transfer)
	{
		return new JavaCollection<T> (handle, transfer);
	}

	internal override IDictionary CreateDictionary (ValueTypeFactory valueFactory, IntPtr handle, JniHandleOwnership transfer)
	{
		return valueFactory.CreateDictionaryWithKey (this, handle, transfer);
	}

	internal override IDictionary CreateDictionaryWithReferenceKey (Type keyType, IntPtr handle, JniHandleOwnership transfer)
	{
		_ = ReferenceKeyDictionaryType;
		var dictionaryType = SafeJavaCollectionFactory.MakeGenericType (typeof (JavaDictionary<,>), [keyType, typeof (T)]);
		return (IDictionary) SafeJavaCollectionFactory.CreateInstance (dictionaryType, handle, transfer);
	}

	internal override IDictionary CreateDictionaryWithReferenceValue (Type valueType, IntPtr handle, JniHandleOwnership transfer)
	{
		_ = ReferenceValueDictionaryType;
		var dictionaryType = SafeJavaCollectionFactory.MakeGenericType (typeof (JavaDictionary<,>), [typeof (T), valueType]);
		return (IDictionary) SafeJavaCollectionFactory.CreateInstance (dictionaryType, handle, transfer);
	}

	internal override IDictionary CreateDictionaryWithKey<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] TKey> (
		ValueTypeFactory<TKey> keyFactory,
		IntPtr handle,
		JniHandleOwnership transfer)
	{
		// Value/value dictionaries need no dedicated rooting token (unlike the mixed reference/value
		// cases): the full JavaDictionary<TKey, T> cross-product is statically rooted by NativeAOT.
		// Every ValueTypeFactory<X> is constructed in PrimitiveTypeFactories, CreateDictionary is a
		// virtual call reachable for all of them (so CreateDictionaryWithKey<X> is instantiated for
		// every key type X), and this override is a generic virtual method — so NativeAOT's GVM
		// dependency analysis emits ValueTypeFactory<Y>.CreateDictionaryWithKey<X> (hence
		// new JavaDictionary<X, Y>()) for every (X, Y) pair in the fixed primitive/nullable set.
		return new JavaDictionary<TKey, T> (handle, transfer);
	}
}
