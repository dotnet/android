using System;

using NUnit.Framework;

using Android.Graphics;
using Android.Runtime;

using Com.Xamarin.Android;

namespace Xamarin.Android.JcwGenTests {

	[TestFixture]
	public class BindingTests {

		[Test]
		public void TestResourceId ()
		{
			Assert.AreEqual (Resource.Id.action_settings, Com.Example.Javalib.MainActivity.ActionSettings);
		}

		[Test]
		public void TestNativeLibDllImport ()
		{
			Assert.AreEqual (TestNativeLib.Binding.SampleFunction (), 0xf00);
		}

		[Test]
		public void TestNativeLibDllImportInEmbeddedArchive ()
		{
			Assert.AreEqual (TestNativeLib.Binding.SampleFunction2 (), 0xf200);
		}

		[Test]
		public void TestBxc4288 ()
		{
			var t = new Com.Xamarin.Android.Bxc4288 ();
			var c = t.UseColors (Color.Gray);
			Assert.AreEqual (Color.Gray, c);
		}

		[Test]
		public void Hello ()
		{
			using (var v = new HélloÊncodingIssues ()) {
			}
		}

		[Test]
		public void EnsureTypesAreBound ()
		{
#pragma warning disable 0219
			Com.Xamarin.Android.Bxc9446 ignore  = null;
#pragma warning restore 0219
		}

		[Test]
		public void Arrays ()
		{
			using (var dh = new Com.Xamarin.Android.DataHandler ()) {
				EventHandler<Com.Xamarin.Android.DataEventArgs> h = (o, e) => {
					Assert.AreEqual ("fromNode", e.P0);
					Assert.AreEqual ("fromChannel", e.P1);
					Assert.AreEqual ("payloadType", e.P2);
					for (int i = 0; i < e.P3.Length; ++i) {
						for (int j = 0; j < e.P3 [i].Length; ++j) {
							byte expected = (byte) (((i+1)*10) + (j+1));
							Assert.AreEqual ((byte)(expected + 'J'), e.P3 [i][j]);
							e.P3 [i][j] = expected;
						}
					}
				};
				dh.Data += h;
				dh.Send ();
				dh.Data -= h;
			}
		}

		[Test]
		public void JavaSideActivation ()
		{
			using (var i = new ConstructorTest ()) {
				// To ensure that CallMethodFromCtor.class_ref is initialized
			}
			using (var c = Java.Lang.Class.FromType (typeof (ConstructorTest))) {
				int initGref = Java.Interop.Runtime.GlobalReferenceCount;
				using (var j = Com.Xamarin.Android.CallMethodFromCtor.NewInstance (c)) {
					var instance = j.JavaCast<ConstructorTest>();
					Assert.AreSame (j, instance);
					Assert.IsTrue (instance.DefaultConstructorInvoked);
					Assert.IsTrue (instance.ActivationConstructorInvoked);
				}
				int finiGref = Java.Interop.Runtime.GlobalReferenceCount;
				Assert.AreEqual (initGref, finiGref,
						string.Format ("Initial grefc={0}; final gref={1}; No GREFs should be lost!", initGref, finiGref));
			}
		}

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

		[Test]
		public void JavaAbstractMethodTest ()
		{
			// Library is referencing APIv1, ICursor is from APIv2
			// if the Library assembly isn't fixed, the MyClrCursor instance is not even created
			Test.Bindings.ICursor ic = new Library.MyClrCursor ();

			// we should be able to call Method without issue
			ic.Method ();
			try {
				// when calling Newmethod, we should get Java.Lang.AbstractMethodError. we catch
				// general Exception on purpose, so that AbstractmethodError is not marked by MarkStep
				// but by FixAbstractMethodStep
				ic.NewMethod ();
			} catch (Exception e) {
				if (e.GetType ().ToString () != "Java.Lang.AbstractMethodError")
					throw e;
			}
		}

		// Context https://bugzilla.xamarin.com/show_bug.cgi?id=36036
		[Test]
		public void NestedClassTest ()
		{
			using (var v = new Default.A.B ()) {
			}
		}

		[Test]
		public void BindingInterractionTest ()
		{
			var e = new Com.Xamarin.Android.Bxc37706Throwable ();
			var m = e.Message;
			Assert.IsTrue (e.GetMessageInvoked);
		}
	}

	class ConstructorTest : Com.Xamarin.Android.CallMethodFromCtor {

		public ConstructorTest (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
			ActivationConstructorInvoked = true;
		}

		public ConstructorTest ()
		{
			DefaultConstructorInvoked = true;

			// Ensure that CallMethodFromCtor.class_ref is initialized
			var ignore  = ThresholdClass;
			ignore      = ignore;
		}

		public bool DefaultConstructorInvoked;
		public bool ActivationConstructorInvoked;

		public override int CalledFromCtor ()
		{
			return 42;
		}
	}

	public class CallNonvirtualDerived2 : CallNonvirtualDerived {
	}

	public class Default : Java.Lang.Object {
		public class A {
			public class B : Java.Lang.Object {
			}
		}
	}
}

