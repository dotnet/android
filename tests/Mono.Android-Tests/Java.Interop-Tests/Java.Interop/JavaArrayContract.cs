using System;
using System.Linq;

using Java.Interop;

using Cadenza.Collections.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	public abstract class JavaArrayContract<T> : ListContract<T>
	{
		static int lrefStartCount;

		[ClassInitialize (InheritanceBehavior.BeforeEachDerivedClass)]
		public static void StartArrayTests (TestContext context)
		{
			lrefStartCount  = JniEnvironment.LocalReferenceCount;
		}

		[ClassCleanup (InheritanceBehavior.BeforeEachDerivedClass)]
		public static void EndArrayTests ()
		{
			int lref    = JniEnvironment.LocalReferenceCount;
			Assert.AreEqual (lrefStartCount, lref, "JNI local references");
		}

		[TestMethod]
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

