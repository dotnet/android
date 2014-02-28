using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
			var array       = (JavaPrimitiveArray<TElement>) Activator.CreateInstance (typeof (TArray), values);
			return array;
		}

		protected TElement FromInt32 (int value)
		{
			return (TElement) Convert.ChangeType (value, typeof(TElement));
		}

		[Test]
		public void Constructor_Exceptions ()
		{
			var ctor = typeof (TArray).GetConstructor (new[]{ typeof (IList<TElement>) });
			var ex = Assert.Throws<TargetInvocationException> (() => ctor.Invoke (new object[]{ null }));
			Assert.IsInstanceOf<ArgumentNullException> (ex.InnerException);

			ctor = typeof (TArray).GetConstructor (new[]{ typeof (IEnumerable<TElement>) });
			ex = Assert.Throws<TargetInvocationException> (() => ctor.Invoke (new object[]{ null }));
			Assert.IsInstanceOf<ArgumentNullException> (ex.InnerException);

			ctor = typeof (TArray).GetConstructor (new[]{ typeof (int) });
			ex = Assert.Throws<TargetInvocationException> (() => ctor.Invoke (new object[]{ -1 }));
			Assert.IsInstanceOf<ArgumentException> (ex.InnerException);
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

