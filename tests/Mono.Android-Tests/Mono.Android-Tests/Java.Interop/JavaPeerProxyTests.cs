using System;

using Android.Runtime;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaPeerProxyTests
	{
		[Test]
		public void Constructor_StoresJniNameAndTargetType ()
		{
			var proxy = new ExplicitNameProxy ();

			Assert.AreEqual ("custom/ExplicitName", proxy.JniName);
			Assert.AreEqual (typeof (ProxyTestPeer), proxy.TargetType);
			Assert.IsNull (proxy.InvokerType);
		}

		[Test]
		public void Constructor_StoresInvokerType ()
		{
			var proxy = new InvokerProxy ();

			Assert.AreEqual ("custom/InvokerProxy", proxy.JniName);
			Assert.AreEqual (typeof (ProxyTestPeer), proxy.TargetType);
			Assert.AreEqual (typeof (ProxyTestPeerInvoker), proxy.InvokerType);
		}
	}

	[Register ("test/ProxyTestPeer", DoNotGenerateAcw = true)]
	sealed class ProxyTestPeer : Java.Lang.Object
	{
		public ProxyTestPeer ()
		{
		}

		public ProxyTestPeer (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	sealed class ProxyTestPeerInvoker : Java.Lang.Object
	{
		public ProxyTestPeerInvoker ()
		{
		}

		public ProxyTestPeerInvoker (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	sealed class ExplicitNameProxy : JavaPeerProxy
	{
		public ExplicitNameProxy ()
			: base ("custom/ExplicitName", typeof (ProxyTestPeer), invokerType: null)
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}

	sealed class InvokerProxy : JavaPeerProxy<ProxyTestPeer>
	{
		public InvokerProxy ()
			: base ("custom/InvokerProxy", typeof (ProxyTestPeerInvoker))
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}
}
