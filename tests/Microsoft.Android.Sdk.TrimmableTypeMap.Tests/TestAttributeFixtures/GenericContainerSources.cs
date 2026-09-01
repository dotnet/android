using System;
using System.Collections.Generic;

namespace ValueTypeContainerFixtures.External;

public static class GenericContainerSource
{
	public static IList<T> CreateList<T> () => throw new NotSupportedException ();
}

public sealed class GenericContainerSource<T>
{
	public IList<T> GetList () => throw new NotSupportedException ();
}

public sealed class GenericBodySource<T>
{
	public object GetValue (object value)
	{
		IList<T> result = (IList<T>) value;
		return result;
	}
}

public static class GenericStaticBodySource<T>
{
	static readonly Type ContainerType = typeof (IList<T>);

	public static object GetValue () => ContainerType;
}
