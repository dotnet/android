using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaExceptionTests
	{
		[Test]
		public void StackTrace ()
		{
			try {
				new JniType ("this/type/had/better/not/exist");
			} catch (JavaException e) {
				Assert.IsTrue (
						string.Equals ("this/type/had/better/not/exist", e.Message, StringComparison.OrdinalIgnoreCase) ||
						e.Message.StartsWith ("Didn't find class \"this.type.had.better.not.exist\" on path: DexPathList"));
				Assert.IsTrue (
						// ART
						e.JavaStackTrace.StartsWith ("java.lang.ClassNotFoundException: ", StringComparison.Ordinal) ||
						// Dalvik, JVM
						e.JavaStackTrace.StartsWith ("java.lang.NoClassDefFoundError: this/type/had/better/not/exist", StringComparison.Ordinal));
				e.Dispose ();
			}
		}

		[Test]
		public void InnerException ()
		{
			using (var t = new JniType ("java/lang/Throwable")) {
				var outer = CreateThrowable (t, "Outer Exception");
				SetThrowableCause (t, outer, "Inner Exception");
				using (var e = new JavaException (ref outer, JniHandleOwnership.Transfer)) {
					Assert.IsNotNull (e.InnerException);
					Assert.AreEqual ("Inner Exception", e.InnerException.Message);
					Assert.AreEqual ("Outer Exception", e.Message);
				}
			}
		}

		static unsafe JniObjectReference CreateThrowable (JniType type, string message)
		{
			var c = type.GetConstructor ("(Ljava/lang/String;)V");
			var s = JniEnvironment.Strings.NewString (message);
			try {
				var args = stackalloc JValue [1];
				args [0] = new JValue (s);
				return type.NewObject (c, args);
			} finally {
				JniEnvironment.Handles.Dispose (ref s);
			}
		}

		static void SetThrowableCause (JniType type, JniObjectReference outer, string message)
		{
			var cause = CreateThrowable (type, message);
			SetThrowableCause (type, outer, cause);
			JniEnvironment.Handles.Dispose (ref cause);
		}

		static unsafe void SetThrowableCause (JniType type, JniObjectReference outer, JniObjectReference inner)
		{
			var a = stackalloc JValue [1];
			a [0] = new JValue (inner);

			var i = type.GetInstanceMethod ("initCause", "(Ljava/lang/Throwable;)Ljava/lang/Throwable;");
			var l = i.CallVirtualObjectMethod (outer, a);
			JniEnvironment.Handles.Dispose (ref l);
		}

		[Test]
		public void InnerExceptionIsNotAProxy ()
		{
			using (var t = new JniType ("java/lang/Throwable")) {
				var outer = CreateThrowable (t, "Outer Exception");
				var ex    = new InvalidOperationException ("Managed Exception!");
				var exp   = CreateJavaProxyThrowable (ex);
				SetThrowableCause (t, outer, exp.PeerReference);
				using (var e = new JavaException (ref outer, JniHandleOwnership.Transfer)) {
					Assert.IsNotNull (e.InnerException);
					Assert.AreSame (ex, e.InnerException);
				}
				exp.Dispose ();
			}
		}

		static JavaException CreateJavaProxyThrowable (Exception value)
		{
			var JavaProxyThrowable_type = typeof(JavaObject)
				.Assembly
				.GetType ("Java.Interop.JavaProxyThrowable", throwOnError :true);
			var proxy   = (JavaException) Activator.CreateInstance (JavaProxyThrowable_type, value);
			proxy.RegisterWithVM ();
			return proxy;
		}
	}
}

