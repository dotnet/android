using System;
using System.Diagnostics;

using NUnit.Framework;

using Android.Runtime;

using Java.Interop;

namespace Android.InteropTests {

	[TestFixture]
	public class TestsSample {

		const int ToString_Iterations = 10000;

		[Test]
		public void XAMethodCallTimings ()
		{
			var k = JNIEnv.FindClass ("java/lang/Object");
			var c = JNIEnv.GetMethodID (k, "<init>", "()V");
			var o = JNIEnv.NewObject (k, c);
			var t = JNIEnv.GetMethodID (k, "toString", "()Ljava/lang/String;");

			var sw = Stopwatch.StartNew ();
			for (int i = 0; i < ToString_Iterations; ++i) {
				var r = JNIEnv.CallObjectMethod (o, t);
				JNIEnv.DeleteLocalRef (r);
			}
			sw.Stop ();
			Console.WriteLine ("Xamarin.Android Object.toString() Timing: {0}", sw.Elapsed);
		}

		[Test]
		public void JIMethodCallTimings ()
		{
			using (var k = new JniType ("java/lang/Object"))
			using (var c = k.GetConstructor ("()V"))
			using (var o = k.NewObject (c))
			using (var t = k.GetInstanceMethod ("toString", "()Ljava/lang/String;")) {

				var sw = Stopwatch.StartNew ();
				for (int i = 0; i < ToString_Iterations; ++i) {
					using (var r = t.CallVirtualObjectMethod (o)) {
					}
				}
				sw.Stop ();
				Console.WriteLine ("   Java.Interop Object.toString() Timing: {0}", sw.Elapsed);
			}
		}
	}
}

