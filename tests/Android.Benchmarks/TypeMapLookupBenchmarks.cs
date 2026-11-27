using Android.Runtime;
using BenchmarkDotNet.Attributes;
using Java.Interop;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
public class TypeMapLookupBenchmarks
{
	readonly JniRuntime.JniTypeManager typeManager = JniRuntime.CurrentRuntime.TypeManager;
	readonly JniTypeSignature frameworkTypeSignature = new ("android/app/Activity");
	readonly JniTypeSignature applicationTypeSignature = new ("net/dot/android/benchmarks/BenchmarkInstrumentation");
	readonly JniTypeSignature missingTypeSignature = new ("net/dot/android/benchmarks/Missing");

	[GlobalSetup]
	public void Setup ()
	{
		if (GetFrameworkType () != typeof (global::Android.App.Activity))
			throw new InvalidOperationException ("The framework typemap entry is unavailable.");
		if (GetApplicationType () != typeof (BenchmarkInstrumentation))
			throw new InvalidOperationException ("The application typemap entry is unavailable.");
		if (GetMissingType () is not null)
			throw new InvalidOperationException ("The missing typemap entry unexpectedly resolved.");
		if (!GetFrameworkTypeSignature ().IsValid)
			throw new InvalidOperationException ("The framework JNI signature is unavailable.");
		if (!GetApplicationTypeSignature ().IsValid)
			throw new InvalidOperationException ("The application JNI signature is unavailable.");
		if (!GetClosedGenericTypeSignature ().IsValid)
			throw new InvalidOperationException ("The closed generic JNI signature is unavailable.");
	}

	[Benchmark]
	public Type? GetFrameworkType ()
	{
		return typeManager.GetType (frameworkTypeSignature);
	}

	[Benchmark]
	public Type? GetApplicationType ()
	{
		return typeManager.GetType (applicationTypeSignature);
	}

	[Benchmark]
	public Type? GetMissingType ()
	{
		return typeManager.GetType (missingTypeSignature);
	}

	[Benchmark]
	public JniTypeSignature GetFrameworkTypeSignature ()
	{
		return typeManager.GetTypeSignature (typeof (global::Android.App.Activity));
	}

	[Benchmark]
	public JniTypeSignature GetApplicationTypeSignature ()
	{
		return typeManager.GetTypeSignature (typeof (BenchmarkInstrumentation));
	}

	[Benchmark]
	public JniTypeSignature GetClosedGenericTypeSignature ()
	{
		return typeManager.GetTypeSignature (typeof (JavaList<string>));
	}
}
