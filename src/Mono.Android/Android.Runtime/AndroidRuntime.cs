using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

using Java.Interop;
using Microsoft.Android.Runtime;
using RuntimeFeature = Microsoft.Android.Runtime.RuntimeFeature;

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

	internal class AndroidObjectReferenceManager : JniRuntime.JniObjectReferenceManager {
		public override int GlobalReferenceCount {
			get {return RuntimeNativeMethods._monodroid_gref_get ();}
		}

		public override int WeakGlobalReferenceCount {
			get {return RuntimeNativeMethods._monodroid_weak_gref_get ();}
		}

		public override JniObjectReference CreateLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			var r = base.CreateLocalReference (value, ref localReferenceCount);

			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_lref_log_new (localReferenceCount, r.Handle, (byte) 'L', tname, tid, from, 1);
				}
			}

			return r;
		}

		public override void DeleteLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_lref_log_delete (localReferenceCount - 1, value.Handle, (byte) 'L', tname, tid, from, 1);
				}
			}
			base.DeleteLocalReference (ref value, ref localReferenceCount);
		}

		public override void CreatedLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			base.CreatedLocalReference (value, ref localReferenceCount);
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_lref_log_new (localReferenceCount, value.Handle, (byte) 'L', tname, tid, from, 1);
				}
			}
		}

		public override IntPtr ReleaseLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			var r = base.ReleaseLocalReference (ref value, ref localReferenceCount);
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_lref_log_delete (localReferenceCount - 1, value.Handle, (byte) 'L', tname, tid, from, 1);
				}
			}
			return r;
		}

		public override bool LogGlobalReferenceMessages => Logger.LogGlobalRef;
		public override bool LogLocalReferenceMessages  => Logger.LogLocalRef;

		public override void WriteLocalReferenceLine (string format, params object?[] args)
		{
			if (!RuntimeFeature.ObjectReferenceLogging) {
				return;
			}
			if (!Logger.LogLocalRef) {
				return;
			}

			RuntimeNativeMethods._monodroid_gref_log ("[LREF] " + string.Format (CultureInfo.InvariantCulture, format, args));
			RuntimeNativeMethods._monodroid_gref_log ("\n");
		}

		public override void WriteGlobalReferenceLine (string format, params object?[] args)
		{
			if (!RuntimeFeature.ObjectReferenceLogging) {
				return;
			}
			if (!Logger.LogGlobalRef) {
				return;
			}

			RuntimeNativeMethods._monodroid_gref_log (string.Format (CultureInfo.InvariantCulture, format, args));
			RuntimeNativeMethods._monodroid_gref_log ("\n");
		}

		public override JniObjectReference CreateGlobalReference (JniObjectReference value)
		{
			var r = base.CreateGlobalReference (value);

			int gc;
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					var ctype = GetObjectRefType (value.Type);
					var ntype = GetObjectRefType (r.Type);
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					gc = RuntimeNativeMethods._monodroid_gref_log_new (value.Handle, ctype, r.Handle, ntype, tname, tid, from, 1);
				} else {
					gc = RuntimeNativeMethods._monodroid_gref_inc ();
				}
			} else {
				// Duplicated intentionally: the trimmer removes the outer `if` block entirely when
				// ObjectReferenceLogging is disabled, so the counter increment must appear in both branches.
				gc = RuntimeNativeMethods._monodroid_gref_inc ();
			}

			if (gc >= JNIEnvInit.gref_gc_threshold) {
				Logger.Log (LogLevel.Warn, "monodroid-gc", gc + " outstanding GREFs. Performing a full GC!");
				System.GC.WaitForPendingFinalizers ();
				System.GC.Collect ();
			}

			return r;
		}

		static byte GetObjectRefType (JniObjectReferenceType type)
		{
			switch (type) {
				case JniObjectReferenceType.Invalid:	    return (byte) 'I';
				case JniObjectReferenceType.Local:        return (byte) 'L';
				case JniObjectReferenceType.Global:       return (byte) 'G';
				case JniObjectReferenceType.WeakGlobal:   return (byte) 'W';
				default:                                  return (byte) '*';
			}
		}

		public override void DeleteGlobalReference (ref JniObjectReference value)
		{
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					var ctype = GetObjectRefType (value.Type);
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_gref_log_delete (value.Handle, ctype, tname, tid, from, 1);
				} else {
					RuntimeNativeMethods._monodroid_gref_dec ();
				}
			} else {
				RuntimeNativeMethods._monodroid_gref_dec ();
			}

			base.DeleteGlobalReference (ref value);
		}

		public override JniObjectReference CreateWeakGlobalReference (JniObjectReference value)
		{
			var r = base.CreateWeakGlobalReference (value);

			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					var ctype = GetObjectRefType (value.Type);
					var ntype = GetObjectRefType (r.Type);
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_weak_gref_new (value.Handle, ctype, r.Handle, ntype, tname, tid, from, 1);
				} else {
					RuntimeNativeMethods._monodroid_weak_gref_inc ();
				}
			} else {
				RuntimeNativeMethods._monodroid_weak_gref_inc ();
			}

			return r;
		}

		public override void DeleteWeakGlobalReference (ref JniObjectReference value)
		{
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					var ctype = GetObjectRefType (value.Type);
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_weak_gref_delete (value.Handle, ctype, tname, tid, from, 1);
				} else {
					RuntimeNativeMethods._monodroid_weak_gref_dec ();
				}
			} else {
				RuntimeNativeMethods._monodroid_weak_gref_dec ();
			}

			base.DeleteWeakGlobalReference (ref value);
		}
	}
}
#endif // JAVA_INTEROP
