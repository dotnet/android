using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
			var JavaProxyThrowable_type = Type.GetType ("Android.Runtime.JavaProxyThrowable, Mono.Android");
			MethodInfo? create = JavaProxyThrowable_type.GetMethod (
				"Create",
				BindingFlags.Static | BindingFlags.Public,
				new Type[] { typeof (Exception) }
			);

			Assert.AreNotEqual (null, create, "Unable to find the Android.Runtime.JavaProxyThrowable.Create(Exception) method");
			return (Java.Lang.Throwable)create.Invoke (null, new object[] { e }); // Don't append Java stack trace
		}

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void InnerExceptionIsSet ()
		{
			Exception ex;
			try {
				throw new InvalidOperationException ("boo!");
			} catch (Exception e) {
				ex = e;
			}

			using Java.Lang.Throwable proxy = CreateJavaProxyThrowable (ex);
			using var source = new Java.Lang.Throwable ("detailMessage", proxy);
			using var alias  = new Java.Lang.Throwable (source.Handle, JniHandleOwnership.DoNotTransfer);

			CompareStackTraces (ex, proxy);
			Assert.AreEqual ("detailMessage", alias.Message);
			Assert.AreSame (ex, alias.InnerException);
		}

		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
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

				// Unknown line locations are -1 on the Java side if they're managed, -2 if they're native
				int managedLine = mf.GetFileLineNumber ();
				if (managedLine == 0) {
					managedLine = mf.HasNativeImage () ? -2 :  -1;
				}

				if (managedLine > 0) {
					Assert.AreEqual (mf.GetMethod ()?.Name,                   jf.MethodName, $"Frame {i}: method names differ");
				} else {
					string managedMethodName = mf.GetMethod ()?.Name ?? String.Empty;
					Assert.IsTrue (jf.MethodName.StartsWith ($"{managedMethodName} + 0x"), $"Frame {i}: method name should start with: '{managedMethodName} + 0x'");
				}
				Assert.AreEqual (mf.GetMethod ()?.DeclaringType.FullName, jf.ClassName,  $"Frame {i}: class names differ");
				Assert.AreEqual (mf.GetFileName (),                       jf.FileName,   $"Frame {i}: file names differ");
				Assert.AreEqual (managedLine,                             jf.LineNumber, $"Frame {i}: line numbers differ");
			}
		}
	}
}
