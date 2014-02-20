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
				Assert.AreEqual ("this/type/had/better/not/exist", e.Message);
				Assert.IsTrue (e.JavaStackTrace.StartsWith ("java.lang.NoClassDefFoundError: this/type/had/better/not/exist", StringComparison.Ordinal));
				e.Dispose ();
			}
		}

		[Test]
		public void InnerException ()
		{
			using (var t = new JniType ("java/lang/Throwable")) {
				var outer = CreateThrowable (t, "Outer Exception");
				SetThrowableCause (t, outer, "Inner Exception");
				using (var e = new JavaException (outer, JniHandleOwnership.Transfer)) {
					Assert.IsNotNull (e.InnerException);
					Assert.AreEqual ("Inner Exception", e.InnerException.Message);
					Assert.AreEqual ("Outer Exception", e.Message);
				}
			}
		}

		static JniLocalReference CreateThrowable (JniType type, string message)
		{
			var c = type.GetConstructor ("(Ljava/lang/String;)V");
			using (var s = JniEnvironment.Strings.NewString (message)) {
				return type.NewObject (c, new JValue (s));
			}
		}

		static void SetThrowableCause (JniType type, JniLocalReference outer, string message)
		{
			var i = type.GetInstanceMethod ("initCause", "(Ljava/lang/Throwable;)Ljava/lang/Throwable;");
			using (var cause = CreateThrowable (type, message)) {
				i.CallVirtualObjectMethod (outer, new JValue (cause))
					.Dispose ();
			}
		}
	}
}

