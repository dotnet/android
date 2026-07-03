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
		}

		[Test]
		public void GenericConstructor_StoresJniNameAndTargetType ()
		{
			var proxy = new GenericProxy ();

			Assert.AreEqual ("custom/GenericProxy", proxy.JniName);
			Assert.AreEqual (typeof (ProxyTestPeer), proxy.TargetType);
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

	sealed class ExplicitNameProxy : JavaPeerProxy
	{
		public ExplicitNameProxy ()
			: base ("custom/ExplicitName", typeof (ProxyTestPeer))
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}

	sealed class GenericProxy : JavaPeerProxy<ProxyTestPeer>
	{
		public GenericProxy ()
			: base ("custom/GenericProxy")
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}
}
