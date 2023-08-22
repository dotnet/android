using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

using Android.App;
using Android.Content;
using Android.Runtime;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class ExceptionTest {

		static Java.Lang.Throwable CreateJavaProxyThrowable (Exception e)
		{
			var JavaProxyThrowable_type = typeof (Java.Lang.Object)
				.Assembly
				.GetType ("Android.Runtime.JavaProxyThrowable");
			MethodInfo? create = JavaProxyThrowable_type.GetMethod (
				"Create",
				BindingFlags.Static | BindingFlags.Public,
				new Type[] { typeof (Exception) }
			);

			Assert.AreNotEqual (null, create, "Unable to find the Android.Runtime.JavaProxyThrowable.Create(Exception) method");
			return (Java.Lang.Throwable)create.Invoke (null, new object[] { e }); // Don't append Java stack trace
		}

		[Test]
		public void InnerExceptionIsSet ()
		{
			Exception ex;
			try {
				throw new InvalidOperationException ("boo!");
			} catch (Exception e) {
				ex = e;
			}

			using (Java.Lang.Throwable proxy = CreateJavaProxyThrowable (ex))
			using (var source = new Java.Lang.Throwable ("detailMessage", proxy))
			using (var alias  = new Java.Lang.Throwable (source.Handle, JniHandleOwnership.DoNotTransfer)) {
				CompareStackTraces (ex, proxy);
				Assert.AreEqual ("detailMessage", alias.Message);
				Assert.AreSame (ex, alias.InnerException);
			}
		}

		void CompareStackTraces (Exception ex, Java.Lang.Throwable throwable)
		{
			var managedTrace = new StackTrace (ex);
			StackFrame[] managedFrames = managedTrace.GetFrames ();
			Java.Lang.StackTraceElement[] javaFrames = throwable.GetStackTrace ();

			// Java
			Assert.IsTrue (javaFrames.Length >= managedFrames.Length,
					$"Java should have at least as many frames as .NET does; java({javaFrames.Length}) < managed({managedFrames.Length})");
			for (int i = 0; i < managedFrames.Length; i++) {
				var mf = managedFrames[i];
				var jf = javaFrames[i];

				Assert.AreEqual (mf.GetMethod ()?.Name,                   jf.MethodName, $"Frame {i}: method names differ");
				Assert.AreEqual (mf.GetMethod ()?.DeclaringType.FullName, jf.ClassName,  $"Frame {i}: class names differ");
				Assert.AreEqual (mf.GetFileName (),                       jf.FileName,   $"Frame {i}: file names differ");
				Assert.AreEqual (mf.GetFileLineNumber (),                 jf.LineNumber, $"Frame {i}: line numbers differ");
			}
		}
	}
}
