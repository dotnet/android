using Android.Runtime;
using BenchmarkDotNet.Attributes;
using Java.Interop;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
[InvocationCount (OperationsPerIteration)]
public class UriParseAllocationBenchmarks
{
	const int OperationsPerIteration = 512;
	const string UriValue = "https://example.com/benchmark?q=android";

	readonly global::Android.Net.Uri? [] boundResults = new global::Android.Net.Uri? [OperationsPerIteration];
	readonly global::Android.Net.Uri? [] activatedResults = new global::Android.Net.Uri? [OperationsPerIteration];
	readonly IntPtr [] parsedReferences = new IntPtr [OperationsPerIteration];
	IntPtr uriClass;
	IntPtr uriString;
	JniMethodInfo? parseMethodInfo;
	int boundIndex;
	int activationIndex;

	[GlobalSetup]
	public void Setup ()
	{
		uriClass = JNIEnv.FindClass ("android/net/Uri");
		IntPtr localString = JNIEnv.NewString (UriValue);
		try {
			uriString = JNIEnv.NewGlobalRef (localString);
		} finally {
			JNIEnv.DeleteLocalRef (localString);
		}
		IntPtr parseMethod = JNIEnv.GetStaticMethodID (uriClass, "parse", "(Ljava/lang/String;)Landroid/net/Uri;");
		parseMethodInfo = new JniMethodInfo (parseMethod, isStatic: true);
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		CleanupBoundParse ();
		CleanupActivation ();
		DeleteGlobalReference (ref uriString);
		DeleteGlobalReference (ref uriClass);
	}

	[IterationSetup (Target = nameof (BoundParse))]
	public void SetupBoundParse () => boundIndex = 0;

	[IterationCleanup (Target = nameof (BoundParse))]
	public void CleanupBoundParse () => DisposeAll (boundResults);

	[Benchmark]
	public global::Android.Net.Uri? BoundParse ()
	{
		return boundResults [boundIndex++] = global::Android.Net.Uri.Parse (UriValue);
	}

	[Benchmark]
	public int RawJniParseWithCachedString ()
	{
		// SAFETY: Setup owns uriString as a global reference until Cleanup.
		IntPtr result = unsafe (ParseUri (uriString));
		JNIEnv.DeleteLocalRef (result);
		return result == IntPtr.Zero ? 0 : 1;
	}

	[Benchmark]
	public int RawJniParseWithNewString ()
	{
		IntPtr localString = JNIEnv.NewString (UriValue);
		IntPtr result = IntPtr.Zero;
		try {
			// SAFETY: localString remains live until the finally block below.
			result = unsafe (ParseUri (localString));
			return result == IntPtr.Zero ? 0 : 1;
		} finally {
			JNIEnv.DeleteLocalRef (result);
			JNIEnv.DeleteLocalRef (localString);
		}
	}

	[IterationSetup (Target = nameof (ActivateParsedUri))]
	public void SetupActivation ()
	{
		activationIndex = 0;
		for (int i = 0; i < parsedReferences.Length; i++) {
			// SAFETY: Setup owns uriString as a global reference until Cleanup.
			IntPtr localReference = unsafe (ParseUri (uriString));
			try {
				parsedReferences [i] = JNIEnv.NewGlobalRef (localReference);
			} finally {
				JNIEnv.DeleteLocalRef (localReference);
			}
		}
	}

	[IterationCleanup (Target = nameof (ActivateParsedUri))]
	public void CleanupActivation ()
	{
		DisposeAll (activatedResults);
		for (int i = 0; i < parsedReferences.Length; i++)
			DeleteGlobalReference (ref parsedReferences [i]);
	}

	[Benchmark]
	public global::Android.Net.Uri? ActivateParsedUri ()
	{
		int index = activationIndex++;
		return activatedResults [index] = Java.Lang.Object.GetObject<global::Android.Net.Uri> (
			parsedReferences [index],
			JniHandleOwnership.DoNotTransfer);
	}

	/// <safety>
	/// <paramref name="stringReference"/> must be a live JNI reference to a Java string
	/// for the duration of the synchronous call.
	/// </safety>
	unsafe IntPtr ParseUri (IntPtr stringReference)
	{
		JniMethodInfo methodInfo = parseMethodInfo
			?? throw new InvalidOperationException ("The URI parse method is unavailable.");
		JniArgumentValue argument = new JniArgumentValue (stringReference);
		// SAFETY: Setup resolves Uri.parse(String); argument is live on the stack and
		// the caller supplies the live string reference required by its signature.
		return unsafe (JniEnvironment.StaticMethods.CallStaticObjectMethod (
			new JniObjectReference (uriClass),
			methodInfo,
			&argument)).Handle;
	}

	static void DisposeAll (global::Android.Net.Uri? [] values)
	{
		for (int i = 0; i < values.Length; i++) {
			values [i]?.Dispose ();
			values [i] = null;
		}
	}

	static void DeleteGlobalReference (ref IntPtr reference)
	{
		if (reference == IntPtr.Zero)
			return;
		JNIEnv.DeleteGlobalRef (reference);
		reference = IntPtr.Zero;
	}
}
