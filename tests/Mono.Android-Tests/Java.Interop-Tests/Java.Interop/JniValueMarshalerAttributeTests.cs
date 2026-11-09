using System;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests {

	[TestClass]
	public class JniValueMarshalerAttributeTests {

		[TestMethod]
		public void Constructor ()
		{
			Assert.ThrowsExactly<ArgumentNullException> (() => new JniValueMarshalerAttribute (null));
			Assert.ThrowsExactly<ArgumentException> (() => new JniValueMarshalerAttribute (typeof(int)));

			var a   = new JniValueMarshalerAttribute (typeof (JniInt32ValueMarshaler));
			Assert.AreEqual (a.MarshalerType, typeof (JniInt32ValueMarshaler));
		}
	}
}
