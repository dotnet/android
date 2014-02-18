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
				Assert.AreEqual (JniReferenceType.Local, h.ReferenceType);
			}
		}

		[Test]
		public void RefType_ThrowsObjectDisposedException ()
		{
			JniReferenceSafeHandle h;
			using (var t = new JniType ("java/lang/Object")) {
				h = t.SafeHandle;
			}
			Assert.Throws<ObjectDisposedException> (() => {
					var ignore  = h.ReferenceType;
					GC.KeepAlive (ignore);
			});
		}
	}
}

