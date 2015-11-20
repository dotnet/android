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
		protected override TElement CreateValueA ()
		{
			return FromInt32 ((int) 'A');
		}

		protected override TElement CreateValueB ()
		{
			return FromInt32 ((int) 'B');
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
			Assert.IsTrue (ex.InnerException is ArgumentNullException);

			ctor = typeof (TArray).GetConstructor (new[]{ typeof (IEnumerable<TElement>) });
			ex = Assert.Throws<TargetInvocationException> (() => ctor.Invoke (new object[]{ null }));
			Assert.IsTrue (ex.InnerException is ArgumentNullException);

			ctor = typeof (TArray).GetConstructor (new[]{ typeof (int) });
			ex = Assert.Throws<TargetInvocationException> (() => ctor.Invoke (new object[]{ -1 }));
			Assert.IsTrue (ex.InnerException is ArgumentException);
		}

		[Test]
		public void GetElements ()
		{
			var a = (TArray) CreateCollection (new[]{FromInt32 ('A')});
			JniArrayElements e;
			using (e = a.GetElements ()) {
				if (e == null) // OOM?
					return;
				Assert.IsTrue (e.Elements != IntPtr.Zero);
				// Multi-dispose is supported.
				e.Dispose ();
			}
			Assert.Throws<ObjectDisposedException> (() => e.Release (JniReleaseArrayElementsMode.Abort));
			Assert.Throws<ObjectDisposedException> (() => {
					#pragma warning disable 0219
					var _ = e.Elements;
					#pragma warning restore 0219
			});
			a.Dispose ();
		}

		// TODO: http://developer.android.com/training/articles/perf-jni.html#arrays
		//       "Also, if the Get call fails, you must ensure that your code doesn't
		//        try to Release a NULL pointer later."
		//  This implies that JNIEnv::Get<Type>ArrayElements() can return NULL; how/when?
		//  Theory: this happens if the array is empty (kinda like what C# `fixed` does?).
		//  Try to test this.
		//  (Alas, on OpenJDK JNIEnv::Get<Type>ArrayElements() returns a non-NULL pointer
		//   when the array is empty, so we'll need to run this on Android.)
		[Test]
		public void GetElements_EmptyArray ()
		{
			var a = (TArray) CreateCollection (new TElement[0]);
			JniArrayElements e;
			using (e = a.GetElements ()) {
				if (e == null)
					return;
				Assert.IsTrue (e.Elements != IntPtr.Zero);
				// Multi-dispose is supported.
				e.Dispose ();
			}
			Assert.Throws<ObjectDisposedException> (() => e.Release (JniReleaseArrayElementsMode.Abort));
			Assert.Throws<ObjectDisposedException> (() => {
				#pragma warning disable 0219
				var _ = e.Elements;
				#pragma warning restore 0219
			});
			a.Dispose ();
		}
	}
}

