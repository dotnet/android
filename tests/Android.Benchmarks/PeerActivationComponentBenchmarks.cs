using Android.Runtime;
using BenchmarkDotNet.Attributes;
using Java.Interop;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
public class PeerActivationComponentBenchmarks
{
	IntPtr objectClass;
	IntPtr reference;

	[GlobalSetup]
	public void Setup ()
	{
		IntPtr localReference = JNIEnv.NewString ("benchmark");
		try {
			reference = JNIEnv.NewGlobalRef (localReference);
		} finally {
			JNIEnv.DeleteLocalRef (localReference);
		}

		IntPtr localClass = JNIEnv.GetObjectClass (reference);
		try {
			objectClass = JNIEnv.NewGlobalRef (localClass);
		} finally {
			JNIEnv.DeleteLocalRef (localClass);
		}
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		DeleteGlobalReference (ref objectClass);
		DeleteGlobalReference (ref reference);
	}

	[Benchmark]
	public int GetObjectClass ()
	{
		JniObjectReference result = JniEnvironment.Types.GetObjectClass (new JniObjectReference (reference));
		try {
			return result.IsValid ? 1 : 0;
		} finally {
			JniObjectReference.Dispose (ref result);
		}
	}

	[Benchmark]
	public string? GetJniTypeName ()
	{
		return JniEnvironment.Types.GetJniTypeNameFromClass (new JniObjectReference (objectClass));
	}

	[Benchmark]
	public bool ValidateKnownSealedType ()
	{
		JniObjectReference targetClass = JniEnvironment.Types.FindClass ("java/lang/String");
		try {
			return JniEnvironment.Types.IsInstanceOf (new JniObjectReference (reference), targetClass);
		} finally {
			JniObjectReference.Dispose (ref targetClass);
		}
	}

	[Benchmark]
	public int GetIdentityHashCode ()
	{
		return JniEnvironment.References.GetIdentityHashCode (new JniObjectReference (reference));
	}

	[Benchmark]
	public int CreateGlobalReference ()
	{
		JniObjectReference result = new JniObjectReference (reference).NewGlobalRef ();
		try {
			return result.IsValid ? 1 : 0;
		} finally {
			JniObjectReference.Dispose (ref result);
		}
	}

	static void DeleteGlobalReference (ref IntPtr value)
	{
		if (value == IntPtr.Zero)
			return;
		JNIEnv.DeleteGlobalRef (value);
		value = IntPtr.Zero;
	}
}
