#nullable enable

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {
	[TestFixture]
	public class AndroidValueManagerFinalizePeerTests
	{
		[Test]
		public void FinalizePeer_WhenJavaPeerIsCollected_RemovesSurfacedPeer ()
		{
			var manager = new TestAndroidValueManager {
				TryGCResult = true,
			};
			using (var value = new Java.Lang.Object ()) {
				manager.AddPeer (value);

				manager.FinalizePeer (value);

				Assert.That (manager.TryGCCalls, Is.EqualTo (1));
				Assert.That (value.PeerReference.IsValid, Is.False);
				Assert.That (manager.GetSurfacedPeers (), Is.Empty);
			}
		}

		[Test]
		public void FinalizePeer_WhenJavaPeerIsStillAlive_ReRegistersForFinalization ()
		{
			var manager = new TestAndroidValueManager {
				TryGCResult = false,
			};
			using (var value = new Java.Lang.Object ()) {
				manager.AddPeer (value);

				manager.FinalizePeer (value);

				Assert.That (manager.TryGCCalls, Is.EqualTo (1));
				Assert.That (value.PeerReference.IsValid, Is.True);
				Assert.That (manager.GetSurfacedPeers (), Has.Count.EqualTo (1));
			}
		}

		class TestAndroidValueManager : Android.Runtime.AndroidValueManager
		{
			public int TryGCCalls { get; private set; }
			public bool TryGCResult { get; set; }

			internal protected override bool TryGC (IJavaPeerable value, ref JniObjectReference handle)
			{
				TryGCCalls++;
				if (TryGCResult) {
					JniObjectReference.Dispose (ref handle);
					handle = new JniObjectReference ();
					return true;
				}

				var newHandle = handle.NewGlobalRef ();
				JniObjectReference.Dispose (ref handle);
				handle = newHandle;
				return false;
			}
		}
	}
}
