using System.Runtime.ExceptionServices;

using Java.Interop;

namespace Microsoft.Android.Runtime;

class UncaughtExceptionMarshaler (Java.Lang.Thread.IUncaughtExceptionHandler? OriginalHandler)
	: Java.Lang.Object, Java.Lang.Thread.IUncaughtExceptionHandler
{
	public void UncaughtException (Java.Lang.Thread thread, Java.Lang.Throwable exception)
	{
		var e = (JniEnvironment.Runtime.ValueManager.PeekValue (exception.PeerReference) as System.Exception)
			?? exception;

		AndroidLog.Print (AndroidLogLevel.Fatal, "DOTNET", $"FATAL UNHANDLED EXCEPTION: {e}");

		ExceptionHandling.RaiseAppDomainUnhandledExceptionEvent(e);

		OriginalHandler?.UncaughtException (thread, exception);
	}
}
