using System;
using System.Collections.Generic;

using ValueTypeContainerFixtures.External;

namespace ValueTypeContainerFixtures;

public readonly struct UserValue
{
	public UserValue (int value)
	{
		Value = value;
	}

	public int Value { get; }
}

public enum UserState : short
{
	None,
	Ready,
}

public readonly struct GenericBodyValue
{
}

public readonly struct ChainedMethodValue
{
}

public readonly struct ExternalBodyValue
{
}

public readonly struct GenericArrayValue
{
}

public readonly struct GenericMethodValue
{
}

public readonly struct GenericTypeValue
{
}

public readonly struct GenericTypeBodyValue
{
}

public readonly struct LocalOnlyValue
{
}

public readonly struct StaticBodyValue
{
}

public sealed class GenericBodyHolder<T>
{
	public object GetValue (object value)
	{
		IList<T> result = (IList<T>) value;
		return result;
	}
}

public sealed class ContainerUsage
{
	public IList<UserValue> GetStructList () => throw new NotSupportedException ();

	public ICollection<UserState> GetEnumCollection () => throw new NotSupportedException ();

	public IList<UserState?> GetNullableEnumList () => throw new NotSupportedException ();

	public IList<int?> GetNullablePrimitiveList () => throw new NotSupportedException ();

	public IDictionary<UserValue, string> GetStructKeyDictionary () => throw new NotSupportedException ();

	public IDictionary<string, UserState> GetEnumValueDictionary () => throw new NotSupportedException ();

	public IDictionary<UserValue, UserState> GetValueTypeDictionary () => throw new NotSupportedException ();

	public Type GetTokenOnlyDictionary () => typeof (IDictionary<UserState, UserValue>);

	public Type GetTokenOnlyUIntPtrList () => typeof (IList<UIntPtr>);

	public IList<UserValue>[] GetArrayOfStructLists () => throw new NotSupportedException ();

	public IList<UserValue[,]> GetRectangularStructArrayList () => throw new NotSupportedException ();

	public IDictionary<UserValue[,], int> GetRectangularStructArrayDictionary () => throw new NotSupportedException ();

	public object GetClosedGenericBodyOnly (object value) => GetGenericBodyOnly<GenericBodyValue> (value);

	public object GetChainedGenericBodyOnly (object value) => ForwardGenericBody<ChainedMethodValue> (value);

	public object GetExternalGenericBodyOnly (object value) => new GenericBodySource<ExternalBodyValue> ().GetValue (value);

	public object GetExternalGenericStaticBodyOnly () => GenericStaticBodySource<StaticBodyValue>.GetValue ();

	public object GetGenericArrayBodyOnly (object value) => GetGenericArrayBody<GenericArrayValue> (value);

	public object GetGenericMethodCallOnly () => GenericContainerSource.CreateList<GenericMethodValue> ();

	public object GetGenericTypeCallOnly () => new GenericContainerSource<GenericTypeValue> ().GetList ();

	public object GetGenericTypeBodyOnly (object value) => new GenericBodyHolder<GenericTypeBodyValue> ().GetValue (value);

	public object GetLocalOnly (object value)
	{
		IList<LocalOnlyValue> result = (IList<LocalOnlyValue>) value;
		return result;
	}

	public IList<string> GetReferenceList () => throw new NotSupportedException ();

	public IList<T> GetOpenList<T> () => throw new NotSupportedException ();

	static object ForwardGenericBody<T> (object value) => GetGenericBodyOnly<T> (value);

	static object GetGenericBodyOnly<T> (object value)
	{
		IList<T> result = (IList<T>) value;
		return result;
	}

	static object GetGenericArrayBody<T> (object value)
	{
		IDictionary<T[], int> result = (IDictionary<T[], int>) value;
		return result;
	}

	static void ExpandingRecursion<T> () => ExpandingRecursion<List<T>> ();
}
