using System;
using System.Linq;

using Java.Interop;

using Cadenza.Collections.Tests;
using NUnit.Framework;

namespace Java.InteropTests
{
	public abstract class JavaArrayContract<T> : ListContract<T>
	{
		int lrefStartCount;

		[OneTimeSetUp]
		public void StartArrayTests ()
		{
			lrefStartCount  = JniEnvironment.LocalReferenceCount;
		}

		[OneTimeTearDown]
		public void EndArrayTests ()
		{
			int lref    = JniEnvironment.LocalReferenceCount;
			Assert.AreEqual (lrefStartCount, lref, "JNI local references");
		}

		[Test]
		public void ToArray ()
		{
			var expected = new[] {
				CreateValueA (),
				CreateValueB (),
			};
			var ja  = (JavaArray<T>) CreateCollection (expected);
			var a   = ja.ToArray ();
			Assert.IsTrue (SequenceEqual (expected, a));
			ja.Dispose ();
			DisposeCollection (a);
			DisposeCollection (expected);
		}
	}
}

