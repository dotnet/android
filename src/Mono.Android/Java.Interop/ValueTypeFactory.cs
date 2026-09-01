#nullable enable
using System;
using System.Collections.Generic;

namespace Java.Interop;

abstract class ValueTypeFactory
{
	// NativeAOT's MakeGenericType() and MakeArrayType() paths use canonical templates.
	// Reference types collapse to __Canon, but value types stay value-specific. This map
	// intentionally roots each primitive/nullable array shape through direct typeof(T), typeof(T[]),
	// and new T[length]. Collection wrappers are rooted separately from generated app-specific usage.
	// `byte` is included alongside `sbyte` (both marshal to java.lang.Byte bitwise) so that
	// byte-element collections keep working on the trimmable path, matching the reflection paths.
	internal static readonly Dictionary<Type, ValueTypeFactory> PrimitiveArrayFactories = new () {
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
}

sealed class ValueTypeFactory<T> : ValueTypeFactory
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
}
