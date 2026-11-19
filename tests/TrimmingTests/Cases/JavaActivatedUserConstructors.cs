#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.javaactivateduserconstructors
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/11123: when Java creates a managed peer, the type map must call
// the matching user constructor. Handler.Post () failed with "Default constructor not found for type
// Java.Lang.Thread+RunnableImplementor", and a Java.Lang.Exception subclass created from Java with a cause skipped its
// (Throwable) constructor.

using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[Activity (Label = "JavaActivatedUserConstructors", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			// The app also creates the exception itself, which keeps it and its type map proxy.
			var cause = new Java.Lang.RuntimeException ("cause");
			if (new CausedException (cause).Cause?.Message != "cause") {
				throw new InvalidOperationException ("The managed (Throwable) constructor did not pass the cause on.");
			}
			CausedException.LastCause = null;

			// Create the exception from Java so that Java calls its (Throwable) constructor.
			var exceptionType = Java.Lang.Class.ForName ("net.dot.trimmingtests.CausedException", true, ClassLoader);
			var constructor = exceptionType.GetConstructor (Java.Lang.Class.ForName ("java.lang.Throwable"))
				?? throw new InvalidOperationException ("CausedException has no (Throwable) constructor.");
			var instance = constructor.NewInstance (cause.JavaCast<Java.Lang.Object> ()) ?? throw new InvalidOperationException ("newInstance returned null.");
			// Java.Lang.Throwable doesn't derive from Java.Lang.Object, so look the new peer up as a Throwable.
			var exception = Java.Lang.Object.GetObject<Java.Lang.Throwable> (instance.Handle, JniHandleOwnership.DoNotTransfer);
			if (exception?.GetType ().Name != "CausedException" || CausedException.LastCause?.Message != "cause") {
				throw new InvalidOperationException ($"Java created {exception?.GetType ()} with cause '{CausedException.LastCause?.Message}'.");
			}

			// Java runs the posted Action through Java.Lang.Thread.RunnableImplementor; the callback reports the result.
			if (!new Handler (Looper.MainLooper!).Post (() => Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS"))) {
				throw new InvalidOperationException ("Handler.Post () failed.");
			}
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

[Android.Runtime.Register ("net/dot/trimmingtests/CausedException")]
public class CausedException : Java.Lang.Exception
{
	public static Java.Lang.Throwable? LastCause;

	[Kept]
	public CausedException (Java.Lang.Throwable cause)
		: base (cause)
	{
		LastCause = cause;
	}
}
