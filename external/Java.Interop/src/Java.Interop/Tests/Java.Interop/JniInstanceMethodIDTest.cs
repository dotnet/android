using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniInstanceMethodIDTest
	{
		// https://code.google.com/p/android/issues/detail?id=65710
		[Test]
		public void CallNonvirtualVoidMethod_WithBaseMethodIDAndDerivedType ()
		{
			using (var b = new JniType ("com/xamarin/interop/CallNonvirtualBase"))
			using (var d = new JniType ("com/xamarin/interop/CallNonvirtualDerived")) {
				var m = b.GetInstanceMethod ("method", "()V");
				var f = b.GetInstanceField ("methodInvoked", "Z");

				var c = d.GetConstructor ("()V");
				var g = d.GetInstanceField ("methodInvoked", "Z");
				using (var o = d.NewObject (c)) {
					m.CallNonvirtualVoidMethod (o, d.SafeHandle);
					bool value = f.GetBooleanValue (o);
					Assert.IsTrue (value);
					value = g.GetBooleanValue (o);
					Assert.IsFalse (value);
				}
			}
		}
	}
}

