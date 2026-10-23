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
		public void RetainsTransferredArrayUntilUnregistration ()
		{
			using (var type = new JniType (ClassName)) {
				var registration = RegisterCapturedDelegate (type, 10);
				Collect ();
				Assert.IsTrue (registration.Methods.IsAlive, "The original transferred array must remain rooted.");
				Assert.IsTrue (registration.Callback.IsAlive);
				Assert.AreEqual (17, InvokeAdd (type));

				type.UnregisterNativeMethods ();
				Collect ();
				Assert.IsFalse (registration.Methods.IsAlive);
				Assert.IsFalse (registration.Callback.IsAlive);

				var replacement = RegisterCapturedDelegate (type, 30);
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

		[Test]
		public void RuntimeRetainsOwnerAfterSuccessfulRegistration ()
		{
			var references = RegisterWithoutKeepingOwner ();
			Collect ();
			Assert.IsTrue (references.Owner.IsAlive, "The runtime must retain the registration owner.");
			Assert.IsTrue (references.Methods.IsAlive, "The owner must retain the transferred array.");
			Assert.IsTrue (references.Callback.IsAlive, "The owner must retain the callback.");

			var type = references.Owner.Target as JniType;
			if (type == null)
				throw new InvalidOperationException ("The runtime did not retain the registration owner.");
			using (type) {
				Assert.AreEqual (37, InvokeAdd (type));
				type.Dispose ();
				Collect ();
				Assert.IsFalse (references.Methods.IsAlive);
				Assert.IsFalse (references.Callback.IsAlive);
			}
		}

		[Test]
		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Delegate registration tests run only on runtimes supporting dynamic code.")]
		public void RegistrationFailurePropagatesJavaException ()
		{
			using (var type = new JniType (ClassName)) {
				int offset = 20;
				var callback = new AddCallback ((env, self, a, b) => a + b + offset);
				var methods = new [] {
					new JniNativeMethodRegistration ("__missing_native_method__", "(II)I", callback),
				};
				try {
					var error = Assert.Throws<Java.Lang.NoSuchMethodError> (() => {
						unsafe {
							// SAFETY: Callback matches JNI; the caller keeps methods rooted through fixture cleanup.
							type.RegisterNativeMethods (methods);
						}
					});
					if (error == null)
						throw new InvalidOperationException ("JNI did not report the missing native method.");
					error.Dispose ();
				} finally {
					try {
						type.UnregisterNativeMethods ();
					} finally {
						GC.KeepAlive (methods);
						GC.KeepAlive (callback);
					}
				}
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void PreparationFailureAllowsValidRetry (bool alreadyTracked)
		{
			using (var type = new JniType (ClassName)) {
				if (alreadyTracked)
					type.RegisterWithRuntime ();
				var failed = RegisterInvalidMethods (type);
				Collect ();
				Assert.IsFalse (failed.Methods.IsAlive, "Preparation failure must not transfer ownership.");
				Assert.IsFalse (failed.Callback.IsAlive);

				var registration = RegisterCapturedDelegate (type, 50);
				Collect ();
				Assert.IsTrue (registration.Methods.IsAlive);
				Assert.IsTrue (registration.Callback.IsAlive);
				Assert.AreEqual (57, InvokeAdd (type));
			}
		}

		[Test]
		public void PreparationFailurePreservesPreviousRegistration ()
		{
			using (var type = new JniType (ClassName)) {
				var previous = RegisterCapturedDelegate (type, 60);
				var failed = RegisterInvalidMethods (type);
				Collect ();
				Assert.IsTrue (previous.Methods.IsAlive);
				Assert.IsTrue (previous.Callback.IsAlive);
				Assert.IsFalse (failed.Methods.IsAlive);
				Assert.IsFalse (failed.Callback.IsAlive);
				Assert.AreEqual (67, InvokeAdd (type));
			}
		}

		[Test]
		public void DisposingAfterPreparationFailurePreservesOtherOwner ()
		{
			using (var owner = new JniType (ClassName)) {
				var registration = RegisterCapturedDelegate (owner, 60);
				using (var failed = new JniType (ClassName)) {
					RegisterInvalidMethods (failed);
				}
				Collect ();
				Assert.IsTrue (registration.Methods.IsAlive);
				Assert.IsTrue (registration.Callback.IsAlive);
				Assert.AreEqual (67, InvokeAdd (owner));
			}
		}

		[Test]
		public void DisposeUnregistersMethodsAndReleasesOwner ()
		{
			var references = RegisterAndDispose ();
			Collect ();
			Assert.IsFalse (references.Owner.IsAlive, "Disposal must remove the runtime's owner root.");
			Assert.IsFalse (references.Methods.IsAlive);
			Assert.IsFalse (references.Callback.IsAlive);
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static (WeakReference Owner, WeakReference Methods, WeakReference Callback) RegisterAndDispose ()
		{
			using (var owner = new JniType (ClassName))
			using (var independent = new JniType (ClassName)) {
				var registration = RegisterCapturedDelegate (owner, 40);
				try {
					Assert.AreEqual (47, InvokeAdd (independent));
					owner.Dispose ();
					Assert.IsFalse (owner.PeerReference.IsValid);
					Assert.IsTrue (independent.PeerReference.IsValid);

					var error = Assert.Throws<Java.Lang.UnsatisfiedLinkError> (() => InvokeAdd (independent));
					if (error == null)
						throw new InvalidOperationException ("The disposed owner's native method is still registered.");
					error.Dispose ();
					return (new WeakReference (owner), registration.Methods, registration.Callback);
				} finally {
					// Remove callbacks even if a disposal assertion fails.
					independent.UnregisterNativeMethods ();
				}
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static (WeakReference Owner, WeakReference Methods, WeakReference Callback) RegisterWithoutKeepingOwner ()
		{
			var type = new JniType (ClassName);
			var registration = RegisterCapturedDelegate (type, 30);
			return (new WeakReference (type), registration.Methods, registration.Callback);
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Delegate registration tests run only on runtimes supporting dynamic code.")]
		static (WeakReference Methods, WeakReference Callback) RegisterCapturedDelegate (JniType type, int offset)
		{
			var callback = new AddCallback ((env, self, a, b) => a + b + offset);
			var registration = new JniNativeMethodRegistration ("add", "(II)I", callback);
			var methods = new [] { registration };
			unsafe {
				// SAFETY: Callback has the JNI ABI; array ownership transfers to the registration owner on success.
				type.RegisterNativeMethods (methods);
			}
			return (new WeakReference (methods), new WeakReference (callback));
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Delegate registration tests run only on runtimes supporting dynamic code.")]
		static (WeakReference Methods, WeakReference Callback) RegisterInvalidMethods (JniType type)
		{
			int offset = 1;
			var callback = new AddCallback ((env, self, a, b) => a + b + offset);
			var methods = new [] {
				new JniNativeMethodRegistration ("add", "(II)I", callback),
				default (JniNativeMethodRegistration),
			};
			var error = Assert.Throws<ArgumentException> (() => {
				unsafe {
					// SAFETY: The valid callback matches JNI; the null marshaler must be rejected before JNI.
					type.RegisterNativeMethods (methods);
				}
			});
			if (error == null)
				throw new InvalidOperationException ("Invalid registration was not rejected before JNI.");
			Assert.AreEqual ("methods", error.ParamName);
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
					// SAFETY: JNI synchronously reads this two-element argument buffer for the matching (II)I method.
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
