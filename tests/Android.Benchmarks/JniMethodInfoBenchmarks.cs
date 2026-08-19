using BenchmarkDotNet.Attributes;

namespace Xamarin.Android.Benchmarks;

public unsafe class JniMethodInfoBenchmarks
{
	const string EncodedMember = "hashCode.()I";

	readonly Java.Lang.String peer;
	readonly Java.Interop.JniPeerMembers.JniInstanceMethods methods;

	public JniMethodInfoBenchmarks ()
	{
		peer = new Java.Lang.String ("benchmark");
		methods = peer.JniPeerMembers.InstanceMethods;
	}

	[GlobalSetup]
	public void Setup ()
	{
		_ = InvokeWithStringCache ();
		_ = InvokeGeneratedBinding ();
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		peer.Dispose ();
	}

	[Benchmark (Baseline = true)]
	public int InvokeWithStringCache ()
	{
		return methods.InvokeVirtualInt32Method (EncodedMember, peer, null);
	}

	[Benchmark]
	public int InvokeGeneratedBinding ()
	{
		return peer.GetHashCode ();
	}
}
