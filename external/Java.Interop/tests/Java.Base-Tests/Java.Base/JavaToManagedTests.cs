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

		[Test]
		public void InterfaceInvokerMethod ()
		{
			int value   = 0;
			using var c = new MyIntConsumer (v => value = v);
			using var r = JavaInvoker.CreateRunnable (c);
			r?.Run ();
			Assert.AreEqual (0, value);
			r?.Run ();
			Assert.AreEqual (1, value);
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

		public static unsafe Java.Lang.IRunnable? CreateRunnable (Java.Util.Function.IIntConsumer c)
		{
			JniArgumentValue* args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (c);
			var _rm  = _members.StaticMethods.InvokeObjectMethod ("createRunnable.(Ljava/util/function/IntConsumer;)Ljava/lang/Runnable;", args);
			return Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<Java.Lang.IRunnable> (ref _rm, JniObjectReferenceOptions.CopyAndDispose);
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

	[JniTypeSignature ("example/MyIntConsumer")]
	class MyIntConsumer : Java.Lang.Object, Java.Util.Function.IIntConsumer {

		Action<int> action;

		public MyIntConsumer (Action<int> action)
		{
			this.action = action;
		}

		public void Accept (int value)
		{
			action (value);
		}
	}
}
