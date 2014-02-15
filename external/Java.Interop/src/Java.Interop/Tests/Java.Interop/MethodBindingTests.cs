using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class MethodBindingTests
	{
		//
		//  https://bugzilla.xamarin.com/show_bug.cgi?id=17630
		//  https://bugzilla.xamarin.com/show_bug.cgi?id=17750#c6
		//  https://code.google.com/p/android/issues/detail?id=65710
		//
		//  The scenario: Assume a class hiearchy three-deep:
		//      CallNonvirtualBase > CallNonvirtualDerived > CallNonvirtualDerived2
		//
		//  Historically, "normal" binding rules/convention within Xamarin.Android
		//  is that if a Java class' Derived method overrides a base method
		//  without changing anything (visibility, return type), then the Derived
		//  method is not emitted.
		//
		//  Thus consider the java CallNonvirtualDerived.method() method, which
		//  overrides but doesn't otherwise change CallNonvirtualBase.method().
		//  Consequently, it doesn't need to be emitted, saving on IL size.
		//
		//  Which leads us to the problem: what should the body of the "common"
		//  CallNonvirtualBase.Method() binding do? It needs to do EITHER a
		//  virtual method dispatch (invocation on non-public types) OR a
		//  non-virtual method dispatch (e.g. `base.Method()` invocation).
		//
		//  THEN there are concerns about efficiency and not doing too much extra
		//  work within the binding methods.
		//
		//  Which brings us to THIS scenario: CallNonvirtualBase declares method().
		//  CallNonvirtualDerived overrides method(), but the override isn't
		//  declared within the CallNonvirtualDerived binding. Finally we have
		//  CallNonvirtualDerived2, which inherits CallNonvirtualDerived and
		//  doesn't override anything.
		//
		//  Logically then, if we have a CallNonvirtualDerived2 instance and we
		//  invoke method() upon it, then we SHOULD invoke
		//  CallNonvirtualDerived.method(). ASSERT that this is the case.
		//  Failure to do so possibly results in Bug #17630/#65710, or #17750.
		//
		//  Aside: just to get to this point we're throwing in
		//  JavaObject.JniThresholdType and JavaObject.JniThresholdClass.
		//  These likely won't survive long.
		//
		//  Aside: in Bug #17630 terms, CallNonvirtualBase is ContextWrapper,
		//  CallNonvirtualDerived is Activity, and CallNonvirtualDerived2 is
		//  an Android Callable Wrapper/user subclass
		[Test]
		public void VirtualMethodBinding ()
		{
			using (var d = new CallNonvirtualDerived ()) {
				d.Method ();
				Assert.IsTrue (d.MethodInvoked);
			}
			using (var b = new CallNonvirtualBase ()) {
				b.Method ();
				Assert.IsTrue (b.MethodInvoked);
			}
			using (CallNonvirtualBase b = new CallNonvirtualDerived ()) {
				b.Method ();
				Assert.IsFalse (b.MethodInvoked);
				Assert.IsTrue (((CallNonvirtualDerived) b).MethodInvoked);
			}
			using (var d2 = new CallNonvirtualDerived2 ()) {
				d2.Method ();
				Assert.IsTrue (((CallNonvirtualDerived) d2).MethodInvoked);
				Assert.IsFalse (((CallNonvirtualBase) d2).MethodInvoked);
			}
		}
	}
}

