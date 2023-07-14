using System;
using System.Diagnostics;
using System.Reflection;

using StackTraceElement = Java.Lang.StackTraceElement;

namespace Android.Runtime {

	sealed class JavaProxyThrowable : Java.Lang.Error {

		public  readonly Exception InnerException;

		JavaProxyThrowable (string message, Exception innerException)
			: base (message)
		{
			InnerException  = innerException;
		}

		public static JavaProxyThrowable Create (Exception? innerException, bool appendJavaStackTrace = false)
		{
			if (innerException == null) {
				throw new ArgumentNullException (nameof (innerException));
			}

			var proxy = new JavaProxyThrowable (innerException.Message, innerException);

			try {
				proxy.TranslateStackTrace (appendJavaStackTrace);
			} catch {
				// We shouldn't throw here, just try to do the best we can do
				proxy = new JavaProxyThrowable (innerException.ToString (), innerException);
			}

			return proxy;
		}

		void TranslateStackTrace (bool appendJavaStackTrace)
		{
			var trace = new StackTrace (InnerException, fNeedFileInfo: true);
			if (trace.FrameCount <= 0) {
				return;
			}

			StackTraceElement[]? javaTrace = null;
			if (appendJavaStackTrace) {
				try {
					javaTrace = Java.Lang.Thread.CurrentThread ()?.GetStackTrace ();
				} catch {
					// Ignore
				}
			}

			StackFrame[] frames = trace.GetFrames ();
			int nElements = frames.Length + (javaTrace?.Length ?? 0);
			StackTraceElement[] elements = new StackTraceElement[nElements];

			for (int i = 0; i < frames.Length; i++) {
				StackFrame managedFrame = frames[i];
				MethodBase? managedMethod = managedFrame.GetMethod ();

				var throwableFrame = new StackTraceElement (
					declaringClass: managedMethod?.DeclaringType?.FullName,
					methodName: managedMethod?.Name,
					fileName: managedFrame?.GetFileName (),
					lineNumber: managedFrame?.GetFileLineNumber () ?? 0
				);

				elements[i] = throwableFrame;
			}

			if (javaTrace != null) {
				for (int i = frames.Length; i < nElements; i++) {
					elements[i] = javaTrace[i - frames.Length];
				}
			}

			SetStackTrace (elements);
		}
	}
}
