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
		public void RetainsSnapshotsAcrossRepeatedRegistrations ()
		{
			using (var type = new JniType (ClassName)) {
				var first = RegisterCapturedDelegate (type, 10, fail: false);
				var second = RegisterCapturedDelegate (type, 20, fail: false);

				Collect ();
				Assert.IsTrue (first.IsAlive, "The previous registration must remain rooted.");
				Assert.IsTrue (second.IsAlive, "Mutating the input array must not remove the delegate root.");
				Assert.AreEqual (27, InvokeAdd (type));

				type.UnregisterNativeMethods ();
				Collect ();
				Assert.IsFalse (first.IsAlive);
				Assert.IsFalse (second.IsAlive);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RuntimeRetainsOwnerIncludingPartialFailure (bool fail)
		{
			var references = RegisterWithoutKeepingOwner (fail);
			Collect ();
			Assert.IsTrue (references.Owner.IsAlive, "The runtime must retain the registration owner.");
			Assert.IsTrue (references.Callback.IsAlive, "A failed attempt may have published a callback.");

			var type = references.Owner.Target as JniType;
			Assert.IsNotNull (type);
			if (type == null)
				throw new InvalidOperationException ("The runtime did not retain the registration owner.");
			using (type) {
				if (!fail)
					Assert.AreEqual (37, InvokeAdd (type));
				type.UnregisterNativeMethods ();
				Collect ();
				Assert.IsFalse (references.Callback.IsAlive);
			}
		}

		[Test]
		public void FailedRegistrationPreservesPreviousAndAttemptedDelegates ()
		{
			using (var type = new JniType (ClassName)) {
				var previous = RegisterCapturedDelegate (type, 10, fail: false);
				var attempted = RegisterCapturedDelegate (type, 20, fail: true);

				Collect ();
				Assert.IsTrue (previous.IsAlive);
				Assert.IsTrue (attempted.IsAlive);
				Assert.That (InvokeAdd (type), Is.EqualTo (17).Or.EqualTo (27));

				type.UnregisterNativeMethods ();
				Collect ();
				Assert.IsFalse (previous.IsAlive);
				Assert.IsFalse (attempted.IsAlive);
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static (WeakReference Owner, WeakReference Callback) RegisterWithoutKeepingOwner (bool fail)
		{
			var type = new JniType (ClassName);
			var callback = RegisterCapturedDelegate (type, 30, fail);
			return (new WeakReference (type), callback);
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Delegate registration tests run only on runtimes supporting dynamic code.")]
		static WeakReference RegisterCapturedDelegate (JniType type, int offset, bool fail)
		{
			var callback = new AddCallback ((env, self, a, b) => a + b + offset);
			var registration = new JniNativeMethodRegistration ("add", "(II)I", callback);
			var methods = fail
				? new [] { registration, new JniNativeMethodRegistration ("__missing_native_method__", "(II)I", callback) }
				: new [] { registration };
			if (fail) {
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
			Array.Clear (methods, 0, methods.Length);
			return new WeakReference (callback);
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
