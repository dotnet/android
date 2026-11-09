using System;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests {

	[TestClass]
	public class JavaObjectExtensionsTests
	{
		[TestMethod]
		public void GetJniTypeName ()
		{
			using (var o = new JavaObject ()) {
				Assert.AreEqual ("java/lang/Object",    o.GetJniTypeName ());
			}
			using (var o = new JavaInt32Array (0)) {
				Assert.AreEqual ("[I",  o.GetJniTypeName ());
			}
		}

		[TestMethod]
		public void GetJniTypeName_Exceptions ()
		{
			IJavaPeerable o = null;
			Assert.ThrowsExactly<ArgumentNullException> (() => o.GetJniTypeName ());
			o = new JavaObject ();
			o.Dispose ();
			Assert.ThrowsExactly<ObjectDisposedException> (() => o.GetJniTypeName ());
		}
	}
}

