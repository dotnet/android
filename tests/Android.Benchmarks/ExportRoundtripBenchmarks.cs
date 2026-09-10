using System.Runtime.CompilerServices;
using Android.Runtime;
using BenchmarkDotNet.Attributes;
using Java.Interop;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
public unsafe class ExportRoundtripBenchmarks
{
	const int Value = 42;
	const long State = 0x123456789;
	const string TextValue = "benchmark";
	const string PrimitiveRoundtripSignature = "(IJ)J";
	const string StringRoundtripSignature = "(IJLjava/lang/String;)J";

	readonly ExportRoundtripPeer peer = new ();
	readonly Java.Lang.String text = new (TextValue);
	IntPtr primitiveRoundtripMethod;
	IntPtr stringRoundtripMethod;

	[GlobalSetup]
	public void Setup ()
	{
		IntPtr peerClass = JNIEnv.GetObjectClass (peer.Handle);
		try {
			primitiveRoundtripMethod = JNIEnv.GetMethodID (peerClass, "primitiveRoundtrip", PrimitiveRoundtripSignature);
			stringRoundtripMethod = JNIEnv.GetMethodID (peerClass, "stringRoundtrip", StringRoundtripSignature);
		} finally {
			JNIEnv.DeleteLocalRef (peerClass);
		}

		long expected = DirectManagedCall ();
		long primitiveResult = JniPrimitiveRoundtrip ();
		long stringResult = JniStringRoundtrip ();
		if (primitiveResult != State + Value)
			throw new InvalidOperationException ($"Expected primitive roundtrip result {State + Value}, but received {primitiveResult}.");
		if (stringResult != expected)
			throw new InvalidOperationException ($"Expected string roundtrip result {expected}, but received {stringResult}.");
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		text.Dispose ();
		peer.Dispose ();
	}

	long DirectManagedCall ()
	{
		return peer.Roundtrip (Value, State, TextValue);
	}

	[Benchmark]
	public long JniPrimitiveRoundtrip ()
	{
		JValue* arguments = stackalloc JValue [2];
		arguments [0] = new JValue (Value);
		arguments [1] = new JValue (State);
		return JNIEnv.CallLongMethod (peer.Handle, primitiveRoundtripMethod, arguments);
	}

	[Benchmark]
	public long JniStringRoundtrip ()
	{
		JValue* arguments = stackalloc JValue [3];
		arguments [0] = new JValue (Value);
		arguments [1] = new JValue (State);
		arguments [2] = new JValue (text);
		return JNIEnv.CallLongMethod (peer.Handle, stringRoundtripMethod, arguments);
	}
}

class ExportRoundtripPeer : Java.Lang.Object
{
	[Export ("primitiveRoundtrip")]
	[MethodImpl (MethodImplOptions.NoInlining)]
	public long PrimitiveRoundtrip (int value, long state)
	{
		return state + value;
	}

	[Export ("stringRoundtrip")]
	[MethodImpl (MethodImplOptions.NoInlining)]
	public long Roundtrip (int value, long state, string text)
	{
		return state + value + text.Length;
	}
}
