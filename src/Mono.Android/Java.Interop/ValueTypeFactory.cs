#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Android.Runtime;

namespace Java.Interop;

abstract class ValueTypeFactory
{
	// NativeAOT's MakeGenericType() and MakeArrayType() paths use canonical templates.
	// Reference types collapse to __Canon, but value types stay value-specific. This map
	// intentionally roots each primitive/nullable value shape through direct typeof(T), typeof(T[]),
	// new T[length], and mixed Java dictionary wrapper constructor references. Android.Runtime.JavaArray<T>
	// wrappers are rooted separately and conditionally by ValueTypeJavaArrayFactory.
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

	internal abstract IDictionary CreateDictionaryWithReferenceKey (Type keyType, IntPtr handle, JniHandleOwnership transfer);

	internal abstract IDictionary CreateDictionaryWithReferenceValue (Type valueType, IntPtr handle, JniHandleOwnership transfer);
}

sealed class ValueTypeFactory<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] T> : ValueTypeFactory
{
	internal ValueTypeFactory ()
	{
	}

	public override Type ValueType { get; } = typeof (T);

	public override Type ArrayType { get; } = typeof (T[]);

	public override Array CreateArray (int length)
	{
		return new T [length];
	}

	internal override IDictionary CreateDictionaryWithReferenceKey (Type keyType, IntPtr handle, JniHandleOwnership transfer)
	{
		// JavaDictionary<keyType, T> canonicalizes like JavaDictionary<__Canon, T>; the
		// JavaDictionary<IJavaPeerable, T> exemplar roots that template with its constructors.
		return CreateReferenceMixedDictionary<JavaDictionary<IJavaPeerable, T>> ([keyType, typeof (T)], handle, transfer);
	}

	internal override IDictionary CreateDictionaryWithReferenceValue (Type valueType, IntPtr handle, JniHandleOwnership transfer)
	{
		// JavaDictionary<T, valueType> canonicalizes like JavaDictionary<T, __Canon>; the
		// JavaDictionary<T, IJavaPeerable> exemplar roots that template with its constructors.
		return CreateReferenceMixedDictionary<JavaDictionary<T, IJavaPeerable>> ([typeof (T), valueType], handle, transfer);
	}

	/// <summary>
	/// Builds a mixed reference/value <see cref="JavaDictionary{K,V}"/> whose reference argument rides the
	/// <c>__Canon</c> template rooted by the <typeparamref name="TExemplar"/> instantiation.
	/// </summary>
	/// <typeparam name="TExemplar">
	/// A <c>JavaDictionary&lt;IJavaPeerable, T&gt;</c> or <c>JavaDictionary&lt;T, IJavaPeerable&gt;</c> exemplar.
	/// Its <see cref="DynamicallyAccessedMembersAttribute"/> roots the constructors of the canonical
	/// template that <paramref name="arguments"/> resolves to (the exact-value argument stays value-specific).
	/// </typeparam>
	/// <param name="arguments">The <c>[key, value]</c> generic arguments, exactly one of which is a reference type.</param>
	[UnconditionalSuppressMessage ("AOT", "IL3050:RequiresDynamicCode",
		Justification = "NativeAOT's Type.MakeGenericType() and Activator.CreateInstance() are annotated because arbitrary constructed generics can lack a runtime template. " +
			"This helper only closes JavaDictionary<,> over one reference argument and the fixed value-type argument from TExemplar. " +
			"The reference argument canonicalizes to __Canon and the value type stays exact, so the result shares the JavaDictionary<IJavaPeerable, T> / JavaDictionary<T, IJavaPeerable> " +
			"template that TExemplar already roots.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2055:MakeGenericType",
		Justification = "IL2055 is raised because the runtime key/value arguments cannot be statically proven to satisfy the DynamicallyAccessedMembers(Constructors) " +
			"requirement that JavaDictionary<[DAM(Constructors)] TKey, [DAM(Constructors)] TValue> places on its element type parameters. That requirement exists for the " +
			"dynamic-code path, where the wrapper reflectively activates key/value peers from their constructors. On the trimmable typemap path taken here the wrapper never " +
			"activates its elements: element peer creation goes through JavaConvert and the typemap's registered activation constructors, so the unmet element requirement is " +
			"never exercised. The wrapper's own constructor is separately rooted by the DynamicallyAccessedMembers(Constructors) annotation on TExemplar.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072:UnrecognizedReflectionPattern",
		Justification = "The constructed dictionary type rides the TExemplar canonical template whose constructors are rooted by DynamicallyAccessedMembers(Constructors). " +
			"Only the known JavaDictionary<TKey,TValue> constructor is invoked here.")]
	static IDictionary CreateReferenceMixedDictionary<[DynamicallyAccessedMembers (SafeJavaCollectionFactory.Constructors)] TExemplar> (
		Type[] arguments,
		IntPtr handle,
		JniHandleOwnership transfer)
	{
		var exemplarType = typeof (TExemplar);
		Debug.Assert (exemplarType.IsGenericType && !exemplarType.IsGenericTypeDefinition);

		var definition = exemplarType.GetGenericTypeDefinition ();
		var dictionaryType = definition.MakeGenericType (arguments);
		var instance = Activator.CreateInstance (
			dictionaryType,
			SafeJavaCollectionFactory.ActivationConstructorBinding,
			binder: null,
			args: [handle, transfer],
			culture: CultureInfo.InvariantCulture);
		if (instance == null) {
			throw new InvalidOperationException ($"Unable to create an instance of collection type '{dictionaryType}'.");
		}
		return (IDictionary) instance;
	}
}
