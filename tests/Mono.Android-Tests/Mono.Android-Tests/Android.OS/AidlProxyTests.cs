using Android.Apptests.Aidl;
using Android.OS;

using NUnit.Framework;

namespace Android.OSTests
{
	[TestFixture]
	[Category ("Aidl")]
	public class AidlProxyTests
	{
		[Test]
		public void OnewayMethodUsesOnewayTransaction ()
		{
			using (var binder = new TrackingBinder ())
			using (var proxy = new IAidlProxyTestStub.Proxy (binder)) {
				proxy.SendNotification ();

				Assert.IsNull (binder.Reply, "A one-way Proxy call should not use a reply Parcel.");
				Assert.AreEqual (TransactionFlags.Oneway, binder.Flags, "A one-way Proxy call should use the one-way transaction flag.");
			}
		}

		sealed class TrackingBinder : Binder
		{
			public TransactionFlags Flags { get; private set; }
			public Parcel Reply { get; private set; }

			protected override bool OnTransact (int code, Parcel data, Parcel reply, int flags)
			{
				Flags = (TransactionFlags) flags;
				Reply = reply;
				return true;
			}
		}
	}
}
