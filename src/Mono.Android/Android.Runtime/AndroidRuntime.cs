using System;
using System.Diagnostics;
using System.Threading;

using Java.Interop;

#if JAVA_INTEROP
namespace Android.Runtime {

	class AndroidRuntime : JniRuntime {

		public const string InternalDllName = RuntimeConstants.InternalDllName;

		internal AndroidRuntime (IntPtr jnienv,
				IntPtr vm,
				IntPtr classLoader,
				JniRuntime.JniTypeManager typeManager,
				JniRuntime.JniValueManager valueManager)
			: base (new AndroidRuntimeOptions (jnienv,
					vm,
					classLoader,
					typeManager,
					valueManager))
		{
			// This is not ideal, but we need to set this while the runtime is initializing but we can't do it directly from the `JNIEnvInit.Initialize` method, since
			// it lives in an assembly that does not reference Mono.Android.  So we do it here, because this class is instantiated by JNIEnvInit.Initialize.
			AndroidEnvironmentInternal.UnhandledExceptionHandler = AndroidEnvironment.UnhandledException;
		}

		public override void FailFast (string? message)
		{
			AndroidEnvironment.FailFast (message);
		}

		public override string GetCurrentManagedThreadName ()
		{
			return Thread.CurrentThread.Name!;
		}

		public override string GetCurrentManagedThreadStackTrace (int skipFrames, bool fNeedFileInfo)
		{
			return new StackTrace (skipFrames, fNeedFileInfo)
				.ToString ();
		}

		public override Exception? GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			if (!reference.IsValid)
				return null;
			var peeked      = JniEnvironment.Runtime.ValueManager.PeekPeer (reference);
			if (peeked is JavaProxyThrowable proxyThrowable) {
				JniObjectReference.Dispose (ref reference, options);
				return proxyThrowable.InnerException;
			}
			var peekedExc   = peeked as Exception;
			if (peekedExc == null) {
				var throwable = Java.Lang.Object.GetObject<Java.Lang.Throwable> (reference.Handle, JniHandleOwnership.DoNotTransfer);
				JniObjectReference.Dispose (ref reference, options);
				return throwable;
			}
			JniObjectReference.Dispose (ref reference, options);
			var unwrapped = JniEnvironment.Runtime.ValueManager.PeekValue (peeked!.PeerReference) as Exception;
			if (unwrapped != null) {
				return unwrapped;
			}
			return peekedExc;
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
			var je  = pendingException as JavaException;
			if (je == null) {
				je  = JavaProxyThrowable.Create (pendingException);
			}
			JniEnvironment.Exceptions.Throw (je.PeerReference);
			GC.KeepAlive (je);
		}
	}

	class AndroidRuntimeOptions : JniRuntime.CreationOptions {
		public AndroidRuntimeOptions (IntPtr jnienv,
				IntPtr vm,
				IntPtr classLoader,
				JniRuntime.JniTypeManager typeManager,
				JniRuntime.JniValueManager valueManager)
		{
			EnvironmentPointer      = jnienv;
			ClassLoader             = new JniObjectReference (classLoader, JniObjectReferenceType.Global);
			InvocationPointer       = vm;
			ObjectReferenceManager  = new ManagedObjectReferenceManager ();
			TypeManager             = typeManager;
			ValueManager            = valueManager;
			JniAddNativeMethodRegistrationAttributePresent = false;
		}
	}
}
#endif // JAVA_INTEROP
