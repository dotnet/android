using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JniReferenceSafeHandleTest {

		[Test]
		public void RefType ()
		{
			JniReferenceSafeHandle h;
			using (var t = new JniType ("java/lang/Object")) {
				h = t.SafeHandle;
				Assert.AreEqual (JObjectRefType.Global, h.RefType);
			}
		}

		[Test, ExpectedException (typeof (ObjectDisposedException))]
		public void RefType_ThrowsObjectDisposedException ()
		{
			JniReferenceSafeHandle h;
			using (var t = new JniType ("java/lang/Object")) {
				h = t.SafeHandle;
			}
			Assert.AreEqual (JObjectRefType.Invalid, h.RefType);
			Assert.Fail ("Should not be reached; h.RefType should have thrown ObjectDisposedException.");
		}
	}
}

