using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.BaseTests {

	[TestFixture]
	public class JavaToManagedTests : JavaVMFixture {

		[Test]
		public void InterfaceMethod ()
		{
			var invoked = false;
			var r       = new MyRunnable (() => invoked = true);
			JavaInvoker.Run (r);
			Assert.IsTrue (invoked);
			r.Dispose ();
		}
	}

	class JavaInvoker : JavaObject {
		internal const string JniTypeName = "com/microsoft/java_base_tests/Invoker";

		static readonly JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (JavaInvoker));

		public static unsafe void Run (Java.Lang.IRunnable r)
		{
			JniArgumentValue* args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (r);
			_members.StaticMethods.InvokeVoidMethod ("run.(Ljava/lang/Runnable;)V", args);
		}
	}

	[JniTypeSignature ("example/MyRunnable")]
	class MyRunnable : Java.Lang.Object, Java.Lang.IRunnable {

		Action action;

		public MyRunnable (Action action)
		{
			this.action = action;
		}

		public void Run ()
		{
			action ();
		}
	}
}
