using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	public abstract class JavaPrimitiveArrayContract<TArray, TElement> : JavaArrayContract<TElement>
	{
		static JavaPrimitiveArrayContract ()
		{
			#pragma warning disable 0219
			var ignore = JVM.Current;
			#pragma warning restore 0219
		}

		protected override ICollection<TElement> CreateCollection (IEnumerable<TElement> values)
		{
			var elements    = values.ToArray ();
			var array       = (JavaPrimitiveArray<TElement>) Activator.CreateInstance (typeof (TArray), elements.Length);
			array.CopyFrom (elements, 0, 0, elements.Length);
			return array;
		}

		protected override TElement FromInt32 (int value)
		{
			return (TElement) Convert.ChangeType (value, typeof(TElement));
		}
	}
}

