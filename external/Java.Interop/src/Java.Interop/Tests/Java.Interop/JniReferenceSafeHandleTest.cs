using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JniReferenceSafeHandleTest : JavaVMFixture {

		[Test]
		public void RefType ()
		{
			JniObjectReference h;
			using (var t = new JniType ("java/lang/Object")) {
				h = t.PeerReference;
				Assert.AreEqual (JniObjectReferenceType.Local, h.Type);
			}
		}

		[Test]
		public void RefType_ThrowsObjectDisposedException ()
		{
			JniObjectReference h;
			using (var t = new JniType ("java/lang/Object")) {
				h = t.PeerReference;
			}
			Assert.Throws<ObjectDisposedException> (() => {
					var ignore  = h.Type;
					GC.KeepAlive (ignore);
			});
		}
	}
}

