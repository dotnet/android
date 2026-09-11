#nullable enable

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
			if (JavaProxyThrowable_type == null)
				throw new AssertionException ("Unable to find the Android.Runtime.JavaProxyThrowable type");
			MethodInfo? create = JavaProxyThrowable_type.GetMethod (
				"Create",
				BindingFlags.Static | BindingFlags.Public,
				new Type[] { typeof (Exception) }
			);

			if (create == null)
				throw new AssertionException ("Unable to find the Android.Runtime.JavaProxyThrowable.Create(Exception) method");
			var proxy = create.Invoke (null, new object [] { e }); // Don't append Java stack trace
			if (proxy is not Java.Lang.Throwable throwable)
				throw new AssertionException ("Android.Runtime.JavaProxyThrowable.Create(Exception) did not return a Java.Lang.Throwable");
			return throwable;
		}

		[Test]
		[Category ("NativeAOTIgnore")] // NativeAOT has very limited stack traces
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

			Console.Error.WriteLine ("# jonp: InnerExceptionIsSet: ex={0}", ex);
			Console.Error.WriteLine ("# jonp: InnerExceptionIsSet: proxy={0}", proxy);

			CompareStackTraces (ex, proxy);
			Assert.AreEqual ("detailMessage", alias.Message);
			Assert.AreSame (ex, alias.InnerException);
		}

		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		void CompareStackTraces (Exception ex, Java.Lang.Throwable throwable)
		{
			var managedTrace = new StackTrace (ex, fNeedFileInfo: true);
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

				Console.WriteLine ("# jonp: CompareStackTraces: managedFrame[{0}]: {1}", i, mf);
				Console.WriteLine ("# jonp: CompareStackTraces:    javaFrame[{0}]: {1}", i, jf);
				var managedMethod = mf.GetMethod ();
				var managedDeclaringType = managedMethod?.DeclaringType;
				if (managedMethod != null && managedDeclaringType == null)
					throw new AssertionException ($"Frame {i}: managed declaring type is missing");
				var javaMethodName = jf.MethodName;
				if (managedLine > 0) {
					Assert.AreEqual (managedMethod?.Name,                    javaMethodName, $"Frame {i}: method names differ");
				} else {
					string managedMethodName = managedMethod?.Name ?? "";
					if (javaMethodName == null)
						throw new AssertionException ($"Frame {i}: Java method name is missing");
					Assert.IsTrue (javaMethodName.StartsWith ($"{managedMethodName} + 0x"),
						$"Frame {i}: method name should start with: '{managedMethodName} + 0x'; was `{javaMethodName}`; ");
				}
				Assert.AreEqual (managedDeclaringType?.FullName,          jf.ClassName,
					$"Frame {i}: class names differ: `{managedDeclaringType?.FullName}` != `{jf.ClassName}`");
				Assert.AreEqual (mf.GetFileName (),                       jf.FileName,
					$"Frame {i}: file names differ: `{mf.GetFileName ()}` != `{jf.FileName}`");
				Assert.AreEqual (managedLine,                             jf.LineNumber,
					$"Frame {i}: line numbers differ: {managedLine} != {jf.LineNumber}");
			}
		}
	}
}
