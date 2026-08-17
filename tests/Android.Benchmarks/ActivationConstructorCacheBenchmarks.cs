using System.Reflection;
using System.Runtime.CompilerServices;
using Android.Runtime;
using BenchmarkDotNet.Attributes;

namespace Xamarin.Android.Benchmarks;

public class ActivationConstructorCacheBenchmarks
{
	delegate object CreateProxyDelegate (Type type, IntPtr handle, JniHandleOwnership transfer);

	readonly CreateProxyDelegate createProxy;
	readonly Java.Lang.String peer;

	public ActivationConstructorCacheBenchmarks ()
	{
		const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
		var method = typeof (Java.Interop.TypeManager).GetMethod (
			"CreateProxy",
			flags,
			null,
			[typeof (Type), typeof (IntPtr), typeof (JniHandleOwnership)],
			null);
		if (method == null)
			throw new InvalidOperationException ("Could not find TypeManager.CreateProxy.");

		createProxy = method.CreateDelegate<CreateProxyDelegate> ();
		peer = new Java.Lang.String ("benchmark");
	}

	[GlobalSetup]
	public void Setup ()
	{
		_ = CreateProxy ();
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		peer.Dispose ();
	}

	[Benchmark]
	public int CreateProxy ()
	{
		IntPtr reference = JNIEnv.NewLocalRef (peer.Handle);
		Java.Lang.String? proxy = null;
		try {
			proxy = (Java.Lang.String) createProxy (typeof (Java.Lang.String), reference, JniHandleOwnership.TransferLocalRef);
			reference = IntPtr.Zero;
			return RuntimeHelpers.GetHashCode (proxy);
		} finally {
			if (reference != IntPtr.Zero)
				JNIEnv.DeleteLocalRef (reference);
			proxy?.Dispose ();
		}
	}
}
