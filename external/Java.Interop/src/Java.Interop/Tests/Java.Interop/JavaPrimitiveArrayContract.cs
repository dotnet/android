using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	public abstract class JavaPrimitiveArrayContract<TArray, TElement> : JavaArrayContract<TElement>
		where TArray : JavaPrimitiveArray<TElement>
	{
		static JavaPrimitiveArrayContract ()
		{
			#pragma warning disable 0219
			var ignore = JVM.Current;
			#pragma warning restore 0219
		}

		protected override sealed TElement CreateValueA ()
		{
			return FromInt32 ((int) 'A');
		}

		protected override sealed TElement CreateValueB ()
		{
			return FromInt32 ((int) 'B');
		}

		protected override sealed TElement CreateValueC ()
		{
			return FromInt32 ((int) 'C');
		}

		protected override ICollection<TElement> CreateCollection (IEnumerable<TElement> values)
		{
			var elements    = values.ToArray ();
			var array       = (JavaPrimitiveArray<TElement>) Activator.CreateInstance (typeof (TArray), elements.Length);
			array.CopyFrom (elements, 0, 0, elements.Length);
			return array;
		}

		protected TElement FromInt32 (int value)
		{
			return (TElement) Convert.ChangeType (value, typeof(TElement));
		}

		[Test]
		public void GetElements ()
		{
			var a = (TArray) CreateCollection (new[]{FromInt32 ('A')});
			var e = a.GetElements ();
			Assert.IsTrue (e.Elements != IntPtr.Zero);
			e.Dispose ();
			// Multi-dispose is supported.
			e.Dispose ();
			Assert.Throws<ObjectDisposedException> (() => e.Release (JniArrayElementsReleaseMode.DoNotCopyBack));
			Assert.Throws<ObjectDisposedException> (() => {
					#pragma warning disable 0219
					var _ = e.Elements;
					#pragma warning restore 0219
			});
			a.Dispose ();
		}
	}
}

