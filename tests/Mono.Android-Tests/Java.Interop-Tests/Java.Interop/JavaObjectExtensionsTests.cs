using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JavaObjectExtensionsTests
	{
		[Test]
		public void GetJniTypeName ()
		{
			using (var o = new JavaObject ()) {
				Assert.AreEqual ("java/lang/Object",    o.GetJniTypeName ());
			}
			using (var o = new JavaInt32Array (0)) {
				Assert.AreEqual ("[I",  o.GetJniTypeName ());
			}
		}

		[Test]
		public void GetJniTypeName_Exceptions ()
		{
			IJavaPeerable o = null;
			Assert.Throws<ArgumentNullException> (() => o.GetJniTypeName ());
			o = new JavaObject ();
			o.Dispose ();
			Assert.Throws<ObjectDisposedException> (() => o.GetJniTypeName ());
		}
	}
}

