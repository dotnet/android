using System;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	public class JavaObjectArrayContractTest<T> : JavaArrayContract<T>
		where T : class, IJavaObject, new()
	{
		protected override System.Collections.Generic.ICollection<T> CreateCollection (System.Collections.Generic.IEnumerable<T> values)
		{
			var items = values.ToList ();
			var array = new JavaObjectArray<T> (items.Count);
			for (int i = 0; i < items.Count; ++i)
				array [i] = items [i];
			return array;
		}

		protected override T CreateValueA ()
		{
			return new T ();
		}

		protected override T CreateValueB ()
		{
			return new T ();
		}

		protected override T CreateValueC ()
		{
			return new T ();
		}
	}

	[TestFixture]
	public class JavaObjectArrayContractTest : JavaObjectArrayContractTest<JavaObject> {
	}
}

