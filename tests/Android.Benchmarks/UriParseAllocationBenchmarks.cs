using Android.Runtime;
using BenchmarkDotNet.Attributes;
using Java.Interop;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
[InvocationCount (OperationsPerIteration)]
public unsafe class UriParseAllocationBenchmarks
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
		IntPtr result = ParseUri (uriString);
		JNIEnv.DeleteLocalRef (result);
		return result == IntPtr.Zero ? 0 : 1;
	}

	[Benchmark]
	public int RawJniParseWithNewString ()
	{
		IntPtr localString = JNIEnv.NewString (UriValue);
		IntPtr result = IntPtr.Zero;
		try {
			result = ParseUri (localString);
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
			IntPtr localReference = ParseUri (uriString);
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

	IntPtr ParseUri (IntPtr stringReference)
	{
		JniMethodInfo methodInfo = parseMethodInfo
			?? throw new InvalidOperationException ("The URI parse method is unavailable.");
		JniArgumentValue argument = new JniArgumentValue (stringReference);
		return JniEnvironment.StaticMethods.CallStaticObjectMethod (
			new JniObjectReference (uriClass),
			methodInfo,
			&argument).Handle;
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
