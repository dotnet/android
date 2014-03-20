using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class InvokeVirtualFromConstructorTests
	{
		[Test]
		public void InvokeVirtualFromConstructor ()
		{
			using (var t = new CallVirtualFromConstructorDerived (42)) {
				Assert.IsTrue (t.Called);
				Assert.IsNull (JVM.Current.PeekObject (t.SafeHandle));
			}
		}
	}
}

