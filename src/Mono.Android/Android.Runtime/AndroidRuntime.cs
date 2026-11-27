using System;
using System.Diagnostics;
using System.Threading;

using Java.Interop;
using RuntimeFeature = Microsoft.Android.Runtime.RuntimeFeature;

namespace Android.Runtime {

	sealed class AndroidRuntime : JniRuntime {

		public const string InternalDllName = "xa-internal-api";

		/// <safety>
		/// <paramref name="vm"/> must be zero or a live JavaVM for this runtime's lifetime.
		/// <paramref name="jnienv"/> must be zero or a live environment on the current thread. At least one
		/// must be nonzero; if both are supplied, the environment must belong to that VM. If only the
		/// environment is supplied, its VM must remain live for the runtime's lifetime.
		/// <paramref name="classLoader"/> must be zero or a live JNI reference until initialization duplicates it;
		/// any local reference must belong to the current thread.
		/// </safety>
		internal unsafe AndroidRuntime (IntPtr jnienv,
				IntPtr vm,
				IntPtr classLoader,
				JniRuntime.JniTypeManager typeManager,
				JniRuntime.JniValueManager valueManager)
			: base (new JniRuntime.CreationOptions {
				EnvironmentPointer      = jnienv,
				ClassLoader             = new JniObjectReference (classLoader, JniObjectReferenceType.Global),
				InvocationPointer       = vm,
				ObjectReferenceManager  = new ManagedObjectReferenceManager (),
				TypeManager             = typeManager,
				ValueManager            = valueManager,
			})
		{
		}

		public override void FailFast (string? message)
		{
			// NativeAOT previously inherited the base implementation.
			if (RuntimeFeature.IsNativeAotRuntime) {
				base.FailFast (message);
			} else {
				AndroidEnvironment.FailFast (message);
			}
		}

		public override string? GetCurrentManagedThreadName ()
		{
			return Thread.CurrentThread.Name;
		}

		public override string GetCurrentManagedThreadStackTrace (int skipFrames, bool fNeedFileInfo)
		{
			return new StackTrace (skipFrames, fNeedFileInfo)
				.ToString ();
		}

		public override Exception? GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			return AndroidExceptionManager.GetExceptionForThrowable (ref reference, options);
		}

		public override void OnUserUnhandledException (ref JniTransition transition, Exception e)
		{
			// Raise the UnhandledExceptionRaiser event via TryRaiseUnhandledException().
			// If a subscriber sets Handled = true, the exception is considered handled
			// and we return without transitioning to JNI.
			// See: https://github.com/dotnet/android/issues/10654
			if (AndroidEnvironment.TryRaiseUnhandledException (e)) {
				return;
			}

			base.OnUserUnhandledException (ref transition, e);
		}

		public override void RaisePendingException (Exception pendingException)
		{
			AndroidExceptionManager.RaisePendingException (pendingException);
		}
	}
}
