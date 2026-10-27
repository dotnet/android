using System;
using System.Diagnostics;
using System.Threading;

using Java.Interop;

namespace Android.Runtime {

	class AndroidRuntime : JniRuntime {

		public const string InternalDllName = "xa-internal-api";

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
		}
	}
}
