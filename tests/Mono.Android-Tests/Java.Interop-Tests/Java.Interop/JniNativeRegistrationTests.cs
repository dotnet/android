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
		public void RetainsTransferredArrayAndRejectsRepeatedRegistration ()
		{
			using (var type = new JniType (ClassName)) {
				var first = RegisterCapturedDelegate (type, 10, fail: false);
				var rejected = RegisterCapturedDelegate (type, 20, fail: false, reject: true);

				Collect ();
				Assert.IsTrue (first.Methods.IsAlive, "The original transferred array must remain rooted.");
				Assert.IsTrue (first.Callback.IsAlive);
				Assert.IsFalse (rejected.Methods.IsAlive, "A rejected registration must not take ownership of its array.");
				Assert.IsFalse (rejected.Callback.IsAlive);
				Assert.AreEqual (17, InvokeAdd (type));

				type.UnregisterNativeMethods ();
				Collect ();
				Assert.IsFalse (first.Methods.IsAlive);
				Assert.IsFalse (first.Callback.IsAlive);

				var replacement = RegisterCapturedDelegate (type, 30, fail: false);
				Collect ();
				Assert.IsTrue (replacement.Methods.IsAlive);
				Assert.IsTrue (replacement.Callback.IsAlive);
				Assert.AreEqual (37, InvokeAdd (type));
				type.UnregisterNativeMethods ();
				Collect ();
				Assert.IsFalse (replacement.Methods.IsAlive);
				Assert.IsFalse (replacement.Callback.IsAlive);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RuntimeRetainsOwnerIncludingPartialFailure (bool fail)
		{
			var references = RegisterWithoutKeepingOwner (fail);
			Collect ();
			Assert.IsTrue (references.Owner.IsAlive, "The runtime must retain the registration owner.");
			Assert.IsTrue (references.Methods.IsAlive, "The owner must retain the transferred array.");
			Assert.IsTrue (references.Callback.IsAlive, "A failed attempt may have published a callback.");

			var type = references.Owner.Target as JniType;
			Assert.IsNotNull (type);
			if (type == null)
				throw new InvalidOperationException ("The runtime did not retain the registration owner.");
			using (type) {
				if (!fail)
					Assert.AreEqual (37, InvokeAdd (type));
				type.Dispose ();
				Collect ();
				Assert.IsFalse (references.Methods.IsAlive);
				Assert.IsFalse (references.Callback.IsAlive);
			}
		}

		[Test]
		public void FailedRegistrationRetainsTransferredArrayAndRejectsRetry ()
		{
			using (var type = new JniType (ClassName)) {
				var attempted = RegisterCapturedDelegate (type, 20, fail: true);
				var rejected = RegisterCapturedDelegate (type, 30, fail: false, reject: true);

				Collect ();
				Assert.IsTrue (attempted.Methods.IsAlive);
				Assert.IsTrue (attempted.Callback.IsAlive);
				Assert.IsFalse (rejected.Methods.IsAlive);
				Assert.IsFalse (rejected.Callback.IsAlive);

				type.UnregisterNativeMethods ();
				Collect ();
				Assert.IsFalse (attempted.Methods.IsAlive);
				Assert.IsFalse (attempted.Callback.IsAlive);
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static (WeakReference Owner, WeakReference Methods, WeakReference Callback) RegisterWithoutKeepingOwner (bool fail)
		{
			var type = new JniType (ClassName);
			var registration = RegisterCapturedDelegate (type, 30, fail);
			return (new WeakReference (type), registration.Methods, registration.Callback);
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Delegate registration tests run only on runtimes supporting dynamic code.")]
		static (WeakReference Methods, WeakReference Callback) RegisterCapturedDelegate (JniType type, int offset, bool fail, bool reject = false)
		{
			var callback = new AddCallback ((env, self, a, b) => a + b + offset);
			var registration = new JniNativeMethodRegistration ("add", "(II)I", callback);
			var methods = fail
				? new [] { registration, new JniNativeMethodRegistration ("__missing_native_method__", "(II)I", callback) }
				: new [] { registration };
			if (reject) {
				Assert.Throws<InvalidOperationException> (() => {
					unsafe {
						type.RegisterNativeMethods (methods);
					}
				});
			} else if (fail) {
				var error = Assert.Throws<Java.Lang.NoSuchMethodError> (() => {
					unsafe {
						type.RegisterNativeMethods (methods);
					}
				});
				Assert.IsNotNull (error);
				if (error == null)
					throw new InvalidOperationException ("JNI did not report the missing native method.");
				error.Dispose ();
			} else {
				unsafe {
					type.RegisterNativeMethods (methods);
				}
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

		static int InvokeAdd (JniType type)
		{
			var constructor = JniEnvironment.InstanceMethods.GetMethodID (type.PeerReference, "<init>", "()V");
			var instance = JniEnvironment.Object.NewObject (type.PeerReference, constructor);
			try {
				var method = JniEnvironment.InstanceMethods.GetMethodID (type.PeerReference, "add", "(II)I");
				unsafe {
					var args = stackalloc JniArgumentValue [2];
					args [0] = new JniArgumentValue (3);
					args [1] = new JniArgumentValue (4);
					return JniEnvironment.InstanceMethods.CallIntMethod (instance, method, args);
				}
			} finally {
				JniObjectReference.Dispose (ref instance);
			}
		}
	}
}
