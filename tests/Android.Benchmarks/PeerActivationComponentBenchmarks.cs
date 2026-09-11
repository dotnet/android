using Android.Runtime;
using BenchmarkDotNet.Attributes;
using Java.Interop;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
[WarmupCount (5)]
[IterationCount (15)]
public class PeerActivationComponentBenchmarks
{
	IntPtr equivalentReference;
	IntPtr identityHashCodeMethod;
	IntPtr objectClass;
	IntPtr reference;
	JniObjectReference systemClass;
	JniMethodInfo? identityHashCodeMethodInfo;
	Java.Lang.String? peer;
	WeakReference<Java.Lang.String>? weakPeer;

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

		equivalentReference = JNIEnv.NewGlobalRef (reference);
		systemClass = JniEnvironment.Types.FindClass ("java/lang/System");
		identityHashCodeMethod = JNIEnv.GetStaticMethodID (
			systemClass.Handle,
			"identityHashCode",
			"(Ljava/lang/Object;)I");
		identityHashCodeMethodInfo = new JniMethodInfo (identityHashCodeMethod, isStatic: true);
		peer = Java.Lang.Object.GetObject<Java.Lang.String> (reference, JniHandleOwnership.DoNotTransfer);
		if (peer == null)
			throw new InvalidOperationException ("Could not create the Java string peer.");
		weakPeer = new WeakReference<Java.Lang.String> (peer);
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		weakPeer = null;
		peer?.Dispose ();
		peer = null;
		identityHashCodeMethodInfo = null;
		JniObjectReference.Dispose (ref systemClass);
		DeleteGlobalReference (ref equivalentReference);
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
	public unsafe int GetIdentityHashCodeWithExceptionCheck ()
	{
		var method = identityHashCodeMethodInfo ??
			throw new InvalidOperationException ("The identity hash code method is unavailable.");
		var args = stackalloc JniArgumentValue [1];
		args [0] = new JniArgumentValue (new JniObjectReference (reference));
		return JniEnvironment.StaticMethods.CallStaticIntMethod (systemClass, method, args);
	}

	[Benchmark]
	public int GetIdentityHashCodeWithPrecachedMethod ()
	{
		return JNIEnv.CallStaticIntMethod (
			systemClass.Handle,
			identityHashCodeMethod,
			new JValue (reference));
	}

	[Benchmark]
	public bool ExceptionCheck ()
	{
		return JniEnvironment.Exceptions.ExceptionCheck ();
	}

	[Benchmark]
	public bool ExceptionOccurred ()
	{
		var exception = JniEnvironment.Exceptions.ExceptionOccurred ();
		if (!exception.IsValid)
			return false;

		JniObjectReference.Dispose (ref exception);
		throw new InvalidOperationException ("No Java exception should be pending during the benchmark.");
	}

	[Benchmark]
	public bool IsSameObject ()
	{
		return JniEnvironment.Types.IsSameObject (
			new JniObjectReference (reference),
			new JniObjectReference (equivalentReference));
	}

	[Benchmark]
	public int WeakReferenceTryGetTarget ()
	{
		var reference = weakPeer;
		if (reference != null && reference.TryGetTarget (out var target))
			return target.JniIdentityHashCode;
		return 0;
	}

	[Benchmark]
	public bool WeakReferenceIsSameObject ()
	{
		var reference = weakPeer;
		return reference != null &&
			reference.TryGetTarget (out var target) &&
			JniEnvironment.Types.IsSameObject (
				new JniObjectReference (this.reference),
				target.PeerReference);
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
