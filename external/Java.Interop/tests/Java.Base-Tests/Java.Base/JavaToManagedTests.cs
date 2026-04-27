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

		[JniMethodSignature ("run", "()V")]
		public void Run ()
		{
			action ();
		}

		static Delegate GetRunHandler ()
		{
			return new _JniMarshal_PPV_V (n_Run);
		}

		delegate void _JniMarshal_PPV_V (IntPtr jnienv, IntPtr n_self);

		static void n_Run (IntPtr jnienv, IntPtr n_self)
		{
			var r_self = new JniObjectReference (n_self);
			var self = JniEnvironment.Runtime.ValueManager.GetValue<MyRunnable> (ref r_self, JniObjectReferenceOptions.CopyAndDoNotRegister);
			try {
				self!.Run ();
			} finally {
				self?.DisposeUnlessReferenced ();
			}
		}

		[JniAddNativeMethodRegistration]
		static void RegisterNativeMembers (JniNativeMethodRegistrationArguments args)
		{
			args.AddRegistrations (new [] { new JniNativeMethodRegistration ("n_run", "()V", new _JniMarshal_PPV_V (n_Run)) });
		}
	}

	[JniTypeSignature ("example/MyIntConsumer")]
	class MyIntConsumer : Java.Lang.Object, Java.Util.Function.IIntConsumer {

		Action<int> action;

		public MyIntConsumer (Action<int> action)
		{
			this.action = action;
		}

		[JniMethodSignature ("accept", "(I)V")]
		public void Accept (int value)
		{
			action (value);
		}

		static Delegate GetAcceptHandler ()
		{
			return new _JniMarshal_PPIV_V (n_Accept);
		}

		delegate void _JniMarshal_PPIV_V (IntPtr jnienv, IntPtr n_self, int value);

		static void n_Accept (IntPtr jnienv, IntPtr n_self, int value)
		{
			var r_self = new JniObjectReference (n_self);
			var self = JniEnvironment.Runtime.ValueManager.GetValue<MyIntConsumer> (ref r_self, JniObjectReferenceOptions.CopyAndDoNotRegister);
			try {
				self!.Accept (value);
			} finally {
				self?.DisposeUnlessReferenced ();
			}
		}

		[JniAddNativeMethodRegistration]
		static void RegisterNativeMembers (JniNativeMethodRegistrationArguments args)
		{
			args.AddRegistrations (new [] { new JniNativeMethodRegistration ("n_accept", "(I)V", new _JniMarshal_PPIV_V (n_Accept)) });
		}
	}
}
