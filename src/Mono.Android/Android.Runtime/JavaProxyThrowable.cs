using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

		public static JavaProxyThrowable Create (Exception innerException)
		{
			if (innerException == null) {
				throw new ArgumentNullException (nameof (innerException));
			}

			// We prepend managed exception type to message since Java will see `JavaProxyThrowable` instead.
			var proxy = new JavaProxyThrowable ($"[{innerException.GetType ()}]: {innerException.Message}", innerException);

			try {
				proxy.TranslateStackTrace ();
			} catch (Exception ex) {
				// We shouldn't throw here, just try to do the best we can do
				Console.WriteLine ($"JavaProxyThrowable: translation threw an exception: {ex}");
				proxy = new JavaProxyThrowable (innerException.ToString (), innerException);
			}

			return proxy;
		}

		void TranslateStackTrace ()
		{
			[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "StackFrame.GetMethod() is \"best attempt\", we handle null & exceptions")]
			static MethodBase? StackFrameGetMethod (StackFrame frame) =>
				frame.GetMethod ();

			var trace = new StackTrace (InnerException, fNeedFileInfo: true);
			if (trace.FrameCount <= 0) {
				return;
			}

			StackTraceElement[]? javaTrace = null;
			try {
				javaTrace = GetStackTrace ();
			} catch (Exception ex) {
				// Report...
				Console.WriteLine ($"JavaProxyThrowable: obtaining Java stack trace threw an exception: {ex}");
				// ..but ignore
			}


			StackFrame[] frames = trace.GetFrames ();
			int nElements = frames.Length + (javaTrace?.Length ?? 0);
			StackTraceElement[] elements = new StackTraceElement[nElements];

			for (int i = 0; i < frames.Length; i++) {
				StackFrame managedFrame = frames[i];
				MethodBase? managedMethod = StackFrameGetMethod (managedFrame);

				var throwableFrame = new StackTraceElement (
					declaringClass: managedMethod?.DeclaringType?.FullName,
					methodName: managedMethod?.Name,
					fileName: managedFrame?.GetFileName (),
					lineNumber: managedFrame?.GetFileLineNumber () ?? -1
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
