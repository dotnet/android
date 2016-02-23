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
				Assert.IsNotNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (t.PeerReference));
			}
		}

		[Test]
		public void ActivationConstructor ()
		{
			var t = CallVirtualFromConstructorDerived.NewInstance (42);
			using (t) {
				Assert.IsTrue (t.InvokedActivationConstructor);
				Assert.IsTrue (t.InvokedConstructor);
			}
		}
	}
}

