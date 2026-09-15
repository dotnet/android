using System;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Apptests.Aidl;
using Android.Content;
using Android.OS;

using NUnit.Framework;

namespace Android.OSTests
{
	[TestFixture]
	[Category ("Aidl")]
	public class AidlProxyTests
	{
		static readonly TimeSpan waitTimeout = TimeSpan.FromSeconds (10);

		[Test]
		public async Task OnewayMethodDoesNotBlock ()
		{
			var context = Application.Context;
			using (var intent = new Intent (context, typeof (AidlProxyTestService)))
			using (var connection = new AidlProxyTestServiceConnection ()) {
				Assert.IsTrue (context.BindService (intent, connection, Bind.AutoCreate), "Service should bind.");
				try {
					var service = IAidlProxyTestStub.AsInterface (await connection.Connected);
					Assert.IsNotNull (service);

					using (var gate = new BlockingBinder ()) {
						try {
							service.Block (gate);
							gate.WaitUntilEntered ();
						} finally {
							gate.Release ();
						}
						gate.WaitUntilCompleted ();
					}
				} finally {
					context.UnbindService (connection);
				}
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void SynchronousVoidMethodRecyclesParcels (bool throwFromBinder)
		{
			using (var binder = new TrackingBinder (throwFromBinder))
			using (var proxy = new IAidlProxyTestStub.Proxy (binder)) {
				if (throwFromBinder)
					Assert.Throws<Java.Lang.RuntimeException> (() => proxy.Ping ());
				else
					proxy.Ping ();

				AssertParcelsRecycled (binder);
			}
		}

		sealed class BlockingBinder : Binder
		{
			readonly ManualResetEventSlim entered = new ManualResetEventSlim ();
			readonly ManualResetEventSlim completed = new ManualResetEventSlim ();
			readonly ManualResetEventSlim release = new ManualResetEventSlim ();

			public void Release ()
			{
				release.Set ();
			}

			public void WaitUntilEntered ()
			{
				Assert.IsTrue (entered.Wait (waitTimeout), "Timed out waiting for the remote one-way method to enter its gate.");
			}

			public void WaitUntilCompleted ()
			{
				Assert.IsTrue (completed.Wait (waitTimeout), "Timed out waiting for the remote one-way method to complete.");
			}

			protected override bool OnTransact (int code, Parcel data, Parcel reply, int flags)
			{
				if (code == Binder.InterfaceConsts.FirstCallTransaction) {
					entered.Set ();
					release.Wait ();
				} else {
					completed.Set ();
				}
				reply.WriteNoException ();
				return true;
			}

			protected override void Dispose (bool disposing)
			{
				if (disposing) {
					release.Set ();
					release.Dispose ();
					completed.Dispose ();
					entered.Dispose ();
				}
				base.Dispose (disposing);
			}
		}

		static void AssertParcelsRecycled (TrackingBinder binder)
		{
			var first = Parcel.Obtain ();
			var second = Parcel.Obtain ();
			try {
				Assert.AreSame (binder.Data, first, "The data Parcel should be first in the pool.");
				Assert.AreSame (binder.Reply, second, "The reply Parcel should be returned to the pool.");
			} finally {
				second.Recycle ();
				first.Recycle ();
			}
		}

		sealed class TrackingBinder : Binder
		{
			readonly bool throwFromBinder;

			public Parcel Data { get; private set; }
			public Parcel Reply { get; private set; }

			public TrackingBinder (bool throwFromBinder)
			{
				this.throwFromBinder = throwFromBinder;
			}

			protected override bool OnTransact (int code, Parcel data, Parcel reply, int flags)
			{
				Data = data;
				Reply = reply;

				if (throwFromBinder)
					throw new Java.Lang.RuntimeException ("Expected test exception.");

				reply.WriteNoException ();
				return true;
			}
		}

		sealed class AidlProxyTestServiceConnection : Java.Lang.Object, IServiceConnection
		{
			readonly TaskCompletionSource<IBinder> connected = new TaskCompletionSource<IBinder> (TaskCreationOptions.RunContinuationsAsynchronously);

			public Task<IBinder> Connected => connected.Task;

			public void OnBindingDied (ComponentName name)
			{
				connected.TrySetException (new InvalidOperationException ("The remote service binding died."));
			}

			public void OnNullBinding (ComponentName name)
			{
				connected.TrySetException (new InvalidOperationException ("The remote service returned a null binding."));
			}

			public void OnServiceConnected (ComponentName name, IBinder service)
			{
				if (service == null)
					connected.TrySetException (new InvalidOperationException ("The remote service returned a null Binder."));
				else
					connected.TrySetResult (service);
			}

			public void OnServiceDisconnected (ComponentName name)
			{
			}
		}
	}

	[Service (Name = "android.apptests.AidlProxyTestService", Process = ":aidl_proxy_test", Exported = false)]
	public class AidlProxyTestService : Android.App.Service
	{
		readonly AidlProxyTestStub binder = new AidlProxyTestStub ();

		public override IBinder OnBind (Intent intent)
		{
			return binder;
		}

		sealed class AidlProxyTestStub : IAidlProxyTestStub
		{
			public override void Block (IBinder gate)
			{
				Transact (gate, Binder.InterfaceConsts.FirstCallTransaction);
				Transact (gate, Binder.InterfaceConsts.FirstCallTransaction + 1);
			}

			static void Transact (IBinder gate, int code)
			{
				using (var data = Parcel.Obtain ())
				using (var reply = Parcel.Obtain ()) {
					gate.Transact (code, data, reply, TransactionFlags.None);
					reply.ReadException ();
				}
			}

			public override void Ping ()
			{
			}
		}
	}
}
