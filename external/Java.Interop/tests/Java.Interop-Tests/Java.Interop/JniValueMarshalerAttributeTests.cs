using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JniValueMarshalerAttributeTests {

		[Test]
		public void Constructor ()
		{
			Assert.Throws<ArgumentNullException> (() => new JniValueMarshalerAttribute (null));
			Assert.Throws<ArgumentException> (() => new JniValueMarshalerAttribute (typeof(int)));

			var a   = new JniValueMarshalerAttribute (typeof (DemoValueTypeValueMarshaler));
			Assert.AreEqual (a.MarshalerType, typeof (DemoValueTypeValueMarshaler));
		}
	}
}

