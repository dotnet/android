using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using Java.Interop;
using Java.Interop.Dynamic;

using Mono.Linq.Expressions;

using NUnit.Framework;

namespace Java.Interop.DynamicTests {

	[TestFixture]
	class DynamicJavaInstanceTests : Java.InteropTests.JavaVMFixture
	{
		[Test]
		public void Constructor ()
		{
			Assert.Throws<ArgumentNullException> (() => new DynamicJavaInstance (null));
		}

		[Test]
		public void DisposeWithJavaObjectDisposesObject ([Values (true, false)] bool register)
		{
			var native      = new JavaObject ();
			if (!register) {
				native.UnregisterFromRuntime ();
			}

			var instance    = new DynamicJavaInstance (native);

			Assert.AreEqual (1,     JavaClassInfo.GetClassInfoCount ("java/lang/Object"));

			Assert.AreSame (native, instance.Value);
			instance.Dispose ();
			Assert.AreEqual (null,  instance.Value);
			Assert.AreEqual (-1,    JavaClassInfo.GetClassInfoCount ("java/lang/Object"));

			if (register) {
				Assert.IsTrue (native.PeerReference.IsValid);
			} else {
				Assert.IsFalse (native.PeerReference.IsValid);
			}
		}

		[Test]
		public void Demo ()
		{
			dynamic Integer = new DynamicJavaClass ("java/lang/Integer");
			dynamic value   = Integer (42);
			Integer.Dispose ();

			sbyte byteV     = value.byteValue ();
			Assert.AreEqual ((sbyte) 42,    byteV);
			dynamic str     = value.toString ();
			var s           = (string) str;
			Assert.AreEqual ("42",  s);
			str.Dispose ();
			value.Dispose ();
		}

		[Test]
		public void Boxing ()
		{
			dynamic Integer = new DynamicJavaClass ("java/lang/Integer");
			dynamic value   = Integer (42);
			int     c       = value.compareTo ((int?) 42);
			Assert.AreEqual (0, c);
		}
	}
}

