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
		public void ExplicitJniName_WinsOverTargetAttribute ()
		{
			var proxy = new ExplicitNameProxy ();

			Assert.AreEqual ("custom/ExplicitName", proxy.JniName);
			Assert.AreEqual (typeof (ProxyTestPeer), proxy.TargetType);
			Assert.IsNull (proxy.InvokerType);
		}

		[Test]
		public void LegacyConstructor_UsesTargetAttribute ()
		{
			var proxy = new LegacyProxy ();

			Assert.AreEqual ("test/ProxyTestPeer", proxy.JniName);
			Assert.AreEqual (typeof (ProxyTestPeer), proxy.TargetType);
			Assert.IsNull (proxy.InvokerType);
		}

		[Test]
		public void LegacyConstructor_ThrowsForTypeWithoutJniAttribute ()
		{
			var proxy = new LegacyUnregisteredProxy ();

			var ex = Assert.Throws<InvalidOperationException> (() => _ = proxy.JniName);
			Assert.That (ex?.Message, Does.Contain ("No JNI name is available"));
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

	sealed class UnregisteredProxyPeer : Java.Lang.Object
	{
		public UnregisteredProxyPeer ()
		{
		}

		public UnregisteredProxyPeer (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	sealed class ExplicitNameProxy : JavaPeerProxy<ProxyTestPeer>
	{
		public ExplicitNameProxy ()
			: base ("custom/ExplicitName", invokerType: null)
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}

	sealed class LegacyProxy : JavaPeerProxy
	{
		public LegacyProxy ()
			: base (typeof (ProxyTestPeer), invokerType: null)
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}

	sealed class LegacyUnregisteredProxy : JavaPeerProxy
	{
		public LegacyUnregisteredProxy ()
			: base (typeof (UnregisteredProxyPeer), invokerType: null)
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}
}
