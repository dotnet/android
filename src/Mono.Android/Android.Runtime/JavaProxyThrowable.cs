using System;
using System.Diagnostics;
using System.Reflection;

using StackTraceElement = Java.Lang.StackTraceElement;

namespace Android.Runtime {

	class JavaProxyThrowable : Java.Lang.Error {

		public  readonly Exception InnerException;

		protected JavaProxyThrowable (string message, Exception innerException)
			: base (message)
		{
			InnerException  = innerException;
		}

		public static JavaProxyThrowable Create (Exception? innerException)
		{
			if (innerException == null) {
				throw new ArgumentNullException (nameof (innerException));
			}

			var proxy = new JavaProxyThrowable (innerException.Message, innerException);

			try {
				proxy.TranslateStackTrace ();
			} catch {
				// We shouldn't throw here, just try to do the best we can do
				proxy = new JavaProxyThrowable (innerException.ToString (), innerException);
			}

			return proxy;
		}

		void TranslateStackTrace ()
		{
			var trace = new StackTrace (InnerException, fNeedFileInfo: true);
			if (trace.FrameCount <= 0) {
				return;
			}

			StackFrame[] frames = trace.GetFrames ();
			StackTraceElement[] elements = new StackTraceElement[frames.Length];

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

			SetStackTrace (elements);
		}
	}
}
