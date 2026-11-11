using Android.Runtime;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TrimmableTypeMapCallbacks;

using static Java.InteropTests.TrimmableTypeMapTestHelpers;

namespace Java.InteropTests
{
	[TestClass]
	[TestCategory ("TrimmableTypeMapRuntimeCoverage")]
	public class TrimmableTypeMapDirectCallbackTests
	{
		[TestMethod]
		public void CompactConnectors_DispatchBothOverloadsToDirectCallbacks ()
		{
			DirectCallbackBase.LastIntValue = 0;
			DirectCallbackBase.LastLongValue = 0;

			using var peer = new DirectCallbackPeer ();
			var intMethod = GetRequiredMethodID (peer.Class.Handle, "remove", "(I)V");
			var longMethod = GetRequiredMethodID (peer.Class.Handle, "remove", "(J)V");

			JNIEnv.CallVoidMethod (peer.Handle, intMethod, new JValue (42));
			JNIEnv.CallVoidMethod (peer.Handle, longMethod, new JValue (3_000_000_000L));

			Assert.AreEqual (42, DirectCallbackBase.LastIntValue);
			Assert.AreEqual (3_000_000_000L, DirectCallbackBase.LastLongValue);
		}

		[TestMethod]
		public void QualifiedConnector_UsesCallbackOnDifferentType ()
		{
			QualifiedCallbackHost.Invocations = 0;

			using var peer = new DirectCallbackPeer ();
			var method = GetRequiredMethodID (peer.Class.Handle, "qualified", "(I)I");

			Assert.AreEqual (142, JNIEnv.CallIntMethod (peer.Handle, method, new JValue (42)));
			Assert.AreEqual (1, QualifiedCallbackHost.Invocations);
		}
	}
}
