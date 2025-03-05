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
			void AssertMembers (string message, string stackTraceMember, string stackTrace) {
				var containsMessage = message.Replace ('/', '.');
				Assert.IsTrue (
						containsMessage.IndexOf ("this.type.had.better.not.exist", StringComparison.OrdinalIgnoreCase) >= 0,
						$"Message: {message}");
				Assert.IsTrue (
						// ART
						(stackTrace.IndexOf ("java.lang.ClassNotFoundException: ", StringComparison.Ordinal) >= 0) ||
						// Dalvik, JVM
						(stackTrace.IndexOf ("java.lang.NoClassDefFoundError: this/type/had/better/not/exist", StringComparison.Ordinal) >= 0),
						$"{stackTraceMember}: {stackTrace}");
			}

			try {
				new JniType ("this/type/had/better/not/exist");
			}
#if __ANDROID__
			catch (Java.Lang.Throwable e) {
				AssertMembers (e.Message, "Throwable.StackTrace", e.StackTrace);
				e.Dispose ();
			}
#endif  // __ANDROID__
			catch (JavaException e) {
				AssertMembers (e.Message, "JavaException.JavaStackTrace", e.JavaStackTrace);
				e.Dispose ();
			}
		}

		[Test]
		public void InnerException ()
		{
			using (var t = new JniType ("java/lang/Throwable")) {
				var outer = CreateThrowable (t, "Outer Exception");
				SetThrowableCause (t, outer, "Inner Exception");
				using (var e = new JavaException (ref outer, JniObjectReferenceOptions.CopyAndDispose)) {
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
				var args = stackalloc JniArgumentValue [1];
				args [0] = new JniArgumentValue (s);
				return type.NewObject (c, args);
			} finally {
				JniObjectReference.Dispose (ref s);
			}
		}

		static void SetThrowableCause (JniType type, JniObjectReference outer, string message)
		{
			var cause = CreateThrowable (type, message);
			SetThrowableCause (type, outer, cause);
			JniObjectReference.Dispose (ref cause);
		}

		static unsafe void SetThrowableCause (JniType type, JniObjectReference outer, JniObjectReference inner)
		{
			var a = stackalloc JniArgumentValue [1];
			a [0] = new JniArgumentValue (inner);

			var i = type.GetInstanceMethod ("initCause", "(Ljava/lang/Throwable;)Ljava/lang/Throwable;");
			var l = JniEnvironment.InstanceMethods.CallObjectMethod (outer, i, a);
			JniObjectReference.Dispose (ref l);
		}

		[Test]
		public void InnerExceptionIsNotAProxy ()
		{
			using (var t = new JniType ("java/lang/Throwable")) {
				var outer = CreateThrowable (t, "Outer Exception");
				var ex    = new InvalidOperationException ("Managed Exception!");
				var exp   = CreateJavaProxyThrowable (ex);
				SetThrowableCause (t, outer, exp.PeerReference);
				using (var e = new JavaException (ref outer, JniObjectReferenceOptions.CopyAndDispose)) {
					Assert.IsNotNull (e.InnerException);
					Assert.AreSame (ex, e.InnerException);
				}
				exp.Dispose ();
			}
		}

		static JavaException CreateJavaProxyThrowable (Exception value)
		{
			var JavaProxyThrowable_type = Type.GetType ("Java.Interop.JavaProxyThrowable, Java.Interop", throwOnError :true);
			var proxy   = (JavaException) Activator.CreateInstance (JavaProxyThrowable_type, value);
			return proxy;
		}
	}
}

