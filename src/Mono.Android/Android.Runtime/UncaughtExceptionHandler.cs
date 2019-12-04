using System;

namespace Android.Runtime {

	sealed class UncaughtExceptionHandler : Java.Lang.Object, Java.Lang.Thread.IUncaughtExceptionHandler {

		Java.Lang.Thread.IUncaughtExceptionHandler defaultHandler;

		public UncaughtExceptionHandler (Java.Lang.Thread.IUncaughtExceptionHandler defaultHandler)
		{
			this.defaultHandler = defaultHandler;
		}

		internal Java.Lang.Thread.IUncaughtExceptionHandler DefaultHandler {
			get { return defaultHandler; }
		}

		public void UncaughtException (Java.Lang.Thread thread, Java.Lang.Throwable ex)
		{
			System.Diagnostics.Debugger.Mono_UnhandledException (ex);

			try {
				var jltp = ex as JavaProxyThrowable;
				Exception innerException = jltp?.InnerException;
				var args  = new UnhandledExceptionEventArgs (innerException ?? ex, isTerminating: true);
				AppDomain.CurrentDomain.DoUnhandledException (args);
			}
			catch (Exception e) {
				Logger.Log (LogLevel.Error, "monodroid", "Exception thrown while raising AppDomain.UnhandledException event: " + e.ToString ());
			}
			if (defaultHandler != null)
				defaultHandler.UncaughtException (thread, ex);
		}
	}
}
