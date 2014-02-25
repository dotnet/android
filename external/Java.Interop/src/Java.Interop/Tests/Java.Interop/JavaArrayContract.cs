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

		[TestFixtureSetUp]
		public void StartArrayTests ()
		{
			lrefStartCount  = JniEnvironment.Current.LocalReferenceCount;
		}

		[TestFixtureTearDown]
		public void EndArrayTests ()
		{
			int lref    = JniEnvironment.Current.LocalReferenceCount;
			Assert.AreEqual (lrefStartCount, lref, "JNI local references");
		}

		[Test]
		public void ToArray ()
		{
			var expected = new[] {
				CreateValueA (),
				CreateValueB (),
				CreateValueC (),
			};
			var ja  = (JavaArray<T>) CreateCollection (expected);
			var a   = ja.ToArray ();
			Assert.IsTrue (expected.SequenceEqual (a));
			ja.Dispose ();
			DisposeCollection (a);
			DisposeCollection (expected);
		}
	}
}

