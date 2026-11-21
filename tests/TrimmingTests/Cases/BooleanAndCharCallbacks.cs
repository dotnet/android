#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.booleanandcharcallbacks
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/11168 and https://github.com/dotnet/android/issues/11773
// (fixed by https://github.com/dotnet/android/pull/11802): Java calls back into managed code through generated
// forwarders that reference the binding's n_* callback. For JNI `boolean` and `char` the forwarder's signature did not
// match n_*, so the trimmer removed the callback (CoreCLR: MissingMethodException; NativeAOT: "will always throw").

using System;
using Android.App;
using Android.OS;
using Java.Lang;
using Microsoft.Android.TrimmingTests.Expectations;

[Activity (Label = "BooleanAndCharCallbacks", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			// Handler.Callback.handleMessage () returns a JNI boolean.
			var callback = new MessageCallback ();
			var looper = Looper.MainLooper ?? throw new InvalidOperationException ("There is no main looper.");
			var message = Message.Obtain () ?? throw new InvalidOperationException ("Message.Obtain () returned null.");
			new Handler (looper, callback).DispatchMessage (message);
			if (!callback.Handled) {
				throw new InvalidOperationException ("Java did not call Handler.Callback.handleMessage ().");
			}

			// StringBuilder (CharSequence) calls CharSequence.charAt (), which returns a JNI char.
			var text = new Java.Lang.StringBuilder (new ManagedCharSequence ("abc")).ToString ();
			if (text != "abc") {
				throw new InvalidOperationException ($"StringBuilder copied '{text}' from the managed CharSequence.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (System.Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

class MessageCallback : Java.Lang.Object, Handler.ICallback
{
	public bool Handled { get; private set; }

	[Kept]
	public bool HandleMessage (Message message) => Handled = true;
}

class ManagedCharSequence : Java.Lang.Object, ICharSequence
{
	readonly string value;

	public ManagedCharSequence (string value) => this.value = value;

	[Kept]
	public char CharAt (int index) => value [index];

	[Kept]
	public int Length () => value.Length;

	public ICharSequence SubSequenceFormatted (int start, int end) => new Java.Lang.String (value.Substring (start, end - start));

	public override string ToString () => value;

	public System.Collections.Generic.IEnumerator<char> GetEnumerator () => value.GetEnumerator ();

	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () => GetEnumerator ();
}
