using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	[NonParallelizable]
	[Category ("JniRegistrationSafety")]
	[Category ("NativeAOTIgnore")]
	public class JniNativeRegistrationTests {

		const string ClassName = "net/dot/jni/test/RegisterNativesTestType";

		[Test]
		public void UnregisterNativeMethodsReleasesArrayAndCallback ()
		{
			using (var type = new JniType (ClassName)) {
				var registration = RegisterCapturedDelegate (type, 10);
				Collect ();
				Assert.IsTrue (registration.Methods.IsAlive);
				Assert.IsTrue (registration.Callback.IsAlive);

				type.UnregisterNativeMethods ();
				Collect ();
				Assert.IsFalse (registration.Methods.IsAlive, "Successful unregistration must release the array.");
				Assert.IsFalse (registration.Callback.IsAlive, "Successful unregistration must release the callback.");
				GC.KeepAlive (type);
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Delegate registration tests run only on runtimes supporting dynamic code.")]
		static (WeakReference Methods, WeakReference Callback) RegisterCapturedDelegate (JniType type, int offset)
		{
			var callback = new AddCallback ((env, self, a, b) => a + b + offset);
			var registration = new JniNativeMethodRegistration ("add", "(II)I", callback);
			var methods = new [] { registration };
			unsafe {
				// SAFETY: Callback matches JNI and the owner retains the unmodified array until unregistration.
				type.RegisterNativeMethods (methods);
			}
			return (new WeakReference (methods), new WeakReference (callback));
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate int AddCallback (IntPtr env, IntPtr self, int a, int b);

		static void Collect ()
		{
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			GC.Collect ();
		}
	}
}
