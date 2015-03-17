using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class InvokeVirtualFromConstructorTests : JavaVMFixture
	{
		[Test]
		public void InvokeVirtualFromConstructor ()
		{
			using (var t = new CallVirtualFromConstructorDerived (42)) {
				Assert.IsTrue (t.Called);
				Assert.IsNull (JavaVM.Current.PeekObject (t.SafeHandle));
			}
		}
	}
}

