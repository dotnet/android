using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;

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

		(int lineNumber, string? methodName, string? className) GetFrameInfo (StackFrame? managedFrame, MethodBase? managedMethod)
		{
			string? methodName = null;
			string? className = null;

			if (managedFrame == null) {
				if (managedMethod != null) {
					methodName = managedMethod.Name;
					className = managedMethod.DeclaringType?.FullName;
				}

				return (-1, methodName, className);
			}

			int lineNumber = -1;
			lineNumber = managedFrame.GetFileLineNumber ();
			if (lineNumber == 0) {
				// -2 means it's a native frame
				lineNumber = managedFrame.HasNativeImage () ? -2 : -1;
			}

			if (managedMethod != null) {
				// If we have no line number information and if it's a managed frame, add the
				// IL offset.
				if (lineNumber == -1 && managedFrame.HasILOffset ()) {
					methodName = $"{managedMethod.Name} + 0x{managedFrame.HasILOffset:x}";
				} else {
					methodName = managedMethod.Name;
				}

				return (lineNumber, methodName, managedMethod.DeclaringType?.FullName);
			}

			string frameString = managedFrame.ToString ();
			var sb = new StringBuilder ();

			// We take the part of the returned string that stretches from the beginning to the first space character
			// and treat it as the method name.
			// https://github.com/dotnet/runtime/blob/18c3ad05c3fc127c3b7f37c49bc350bf7f8264a0/src/coreclr/nativeaot/System.Private.CoreLib/src/Internal/DeveloperExperience/DeveloperExperience.cs#L15-L55
			int pos = frameString.IndexOf (' ');
			string? fullName = null;
			if (pos > 1) {
				fullName = frameString.Substring (0, pos);
			}

			if (!String.IsNullOrEmpty (fullName) && (pos = fullName.LastIndexOf ('.')) >= 1) {
				className = pos + 1 < fullName.Length ? fullName.Substring (pos + 1) : null;
				fullName = fullName.Substring (0, pos);
			}

			if (!String.IsNullOrEmpty (fullName)) {
				sb.Append (fullName);
			} else if (managedFrame.HasNativeImage ()) {
				// We have no name, so we'll put the native IP
				nint nativeIP = managedFrame.GetNativeIP ();
				sb.Append (CultureInfo.InvariantCulture, $"Native 0x{nativeIP:x}");
			}

			if (sb.Length > 0) {
				// We will also append information native offset information, if available and only if we
				// have recorded any previous information, since the offset without context is useless.
				int nativeOffset = managedFrame.GetNativeOffset ();
				if (nativeOffset != StackFrame.OFFSET_UNKNOWN) {
					sb.Append (" + ");
					sb.Append (CultureInfo.InvariantCulture, $"0x{nativeOffset:x}");
				}
			}

			if (sb.Length > 0) {
				methodName = sb.ToString ();
			}

			return (lineNumber, methodName, className);
		}

		void TranslateStackTrace ()
		{
			// FIXME: https://github.com/xamarin/xamarin-android/issues/8724
			// StackFrame.GetMethod() will return null under NativeAOT;
			// However, you can still get useful information from StackFrame.ToString():
			// MainActivity.OnCreate() + 0x37 at offset 55 in file:line:column <filename unknown>:0:0
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

			const string Unknown = "Unknown";
			for (int i = 0; i < frames.Length; i++) {
				StackFrame managedFrame = frames[i];
				MethodBase? managedMethod = StackFrameGetMethod (managedFrame);

				// https://developer.android.com/reference/java/lang/StackTraceElement?hl=en#StackTraceElement(java.lang.String,%20java.lang.String,%20java.lang.String,%20int)
				(int lineNumber, string? methodName, string? declaringClass) = GetFrameInfo (managedFrame, managedMethod);
				var throwableFrame = new StackTraceElement (
					declaringClass: declaringClass ?? Unknown,
					methodName: methodName ?? Unknown,
					fileName: managedFrame?.GetFileName (),
					lineNumber: lineNumber
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
