using Java.Interop;

namespace Java.Interop.Samples.NativeAotFromAndroid;

[JniTypeSignature ("my/MainActivity")]
public class MainActivity : Android.App.Activity {

	public MainActivity ()
	{
		Console.WriteLine ("MainActivity..ctor()");
	}

	protected override void OnCreate (Android.OS.Bundle? savedInstanceState)
	{
		Console.WriteLine ($"MainActivity.OnCreate(): savedInstanceState? {savedInstanceState != null}");
		base.OnCreate (savedInstanceState);
		SetContentView (R.layout.activity_main);

		PrintGrefInfo ();
	}

	static void PrintGrefInfo ()
	{
		var runtime = JniEnvironment.Runtime;
		var peers   = runtime.ValueManager.GetSurfacedPeers ();
		Console.WriteLine ($"Created {runtime.ObjectReferenceManager.GlobalReferenceCount} GREFs; Surfaced {peers.Count} peers");
		for (int i = 0; i < peers.Count; ++i) {
			Console.WriteLine ($"  SurfacedPeers[{i,3}] = {ToString (peers[i])}");
		}
	}

	static string ToString (JniSurfacedPeerInfo peer)
	{
		if (!peer.SurfacedPeer.TryGetTarget (out var p)) {
			return $"JniSurfacedPeerInfo(IdentityHashCode=0x{peer.JniIdentityHashCode:x})";
		}
		return $"JniSurfacedPeerInfo(PeerReference={p.PeerReference} IdentityHashCode=0x{peer.JniIdentityHashCode:x} Instance.Type={p.GetType ()})";
	}
}
