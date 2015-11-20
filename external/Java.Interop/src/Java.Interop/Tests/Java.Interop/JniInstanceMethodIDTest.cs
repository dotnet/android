using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniInstanceMethodIDTest : JavaVMFixture
	{
		// https://code.google.com/p/android/issues/detail?id=65710
		[Test]
		public unsafe void CallNonvirtualVoidMethod_WithBaseMethodIDAndDerivedType ()
		{
			using (var b = new JniType ("com/xamarin/interop/CallNonvirtualBase"))
			using (var d = new JniType ("com/xamarin/interop/CallNonvirtualDerived")) {
				var m = b.GetInstanceMethod ("method", "()V");
				var f = b.GetInstanceField ("methodInvoked", "Z");

				var c = d.GetConstructor ("()V");
				var g = d.GetInstanceField ("methodInvoked", "Z");
				var o = d.NewObject (c, null);
				try {
					if (JavaVMFixture.CallNonvirtualVoidMethodSupportsDeclaringClassMismatch) {
						JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (o, d.PeerReference, m);
						Assert.IsFalse (JniEnvironment.InstanceFields.GetBooleanField (o, f));
						Assert.IsTrue (JniEnvironment.InstanceFields.GetBooleanField (o, g));
					} else {
						JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (o, d.PeerReference, m);
						Assert.IsTrue (JniEnvironment.InstanceFields.GetBooleanField (o, f));
						Assert.IsFalse (JniEnvironment.InstanceFields.GetBooleanField (o, g));
					}
				} finally {
					JniObjectReference.Dispose (ref o);
				}
			}
		}
	}
}

