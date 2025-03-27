using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.BaseTests;

[TestFixture]
public class JniRuntimeJniValueManagerContractExtras : JavaVMFixture {

	[Test]
	public void CreatePeer_FloatIsNotNullableSingle ()
	{
		JniObjectReference float_ref;
#pragma warning disable CS0618
		using (var value = new Java.Lang.Float (1.0f)) {
			float_ref   = value.PeerReference.NewLocalRef ();
		}
#pragma warning restore CS0618
		try {
			var peer    = JniEnvironment.Runtime.ValueManager.CreatePeer (ref float_ref, JniObjectReferenceOptions.Copy, targetType: null);
			Assert.IsNotNull (peer, "Could not create Java.Lang.Float peer!");
			Assert.AreEqual (
					typeof (Java.Lang.Float),
					peer!.GetType (),
					$"Peer type mismatch: expected Java.Lang.Float, got {peer.GetType ()}");
		} finally {
			JniObjectReference.Dispose (ref float_ref);
		}
	}
}
