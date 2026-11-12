using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Android.Graphics;
using Android.Runtime;

using Com.Xamarin.Android;

namespace Xamarin.Android.JcwGenTests {

	[TestClass]
	public class ExceptionTests {

		[TestMethod]
		public void ManagedJavaManaged_FinallyExecuted ()
		{
			using (var t = new Bxc7634 ()) {
				using (var r = new MyGenericRunnable<int> ()) {
					Assert.IsFalse (t.FinallyBlockRun);
					bool ioeThrown = false;
					try {
						t.RunFinallyBlock (r);
					} catch (InvalidOperationException) {
						ioeThrown = true;
					}
					Assert.IsTrue (ioeThrown);
				}
				Assert.IsTrue (t.FinallyBlockRun);
			}
		}

		[TestMethod]
		public void ManagedJavaManaged_JavaCatches ()
		{
			using (var t = new Bxc7634 ()) {
				using (var r = new Java.Lang.Runnable (() => {throw new InvalidOperationException ();})) {
					t.RunCatchBlock (r);
				}
				Assert.IsNotNull (t.ThrowableCaught);
				Assert.AreEqual ("Android.Runtime.JavaProxyThrowable", t.ThrowableCaught.GetType ().FullName);
			}
		}

		class MyGenericRunnable<T> : Java.Lang.Object, Java.Lang.IRunnable {

			public void Run ()
			{
				throw new InvalidOperationException ();
			}
		}
	}
}

