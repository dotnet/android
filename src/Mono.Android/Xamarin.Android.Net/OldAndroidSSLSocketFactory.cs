using Java.Net;
using Javax.Net.Ssl;

namespace Xamarin.Android.Net
{
	// Context: https://github.com/xamarin/xamarin-android/issues/1615
	//
	// Code based on the code provided in the issue above
	//
	class OldAndroidSSLSocketFactory : SSLSocketFactory
	{
		readonly SSLSocketFactory factory = (SSLSocketFactory)Default;

		public override string[] GetDefaultCipherSuites ()
		{
			return factory.GetDefaultCipherSuites ();
		}

		public override string[] GetSupportedCipherSuites ()
		{
			return factory.GetSupportedCipherSuites ();
		}
		public override Socket CreateSocket (InetAddress address, int port, InetAddress localAddress, int localPort)
		{
			return EnableTlsOnSocket (factory.CreateSocket (address, port, localAddress, localPort));
		}

		public override Socket CreateSocket (InetAddress host, int port)
		{
			return EnableTlsOnSocket (factory.CreateSocket (host, port));
		}

		public override Socket CreateSocket (string host, int port, InetAddress localHost, int localPort)
		{
			return EnableTlsOnSocket (factory.CreateSocket (host, port, localHost, localPort));
		}

		public override Socket CreateSocket (string host, int port)
		{
			return EnableTlsOnSocket (factory.CreateSocket (host, port));
		}

		public override Socket CreateSocket (Socket s, string host, int port, bool autoClose)
		{
			return EnableTlsOnSocket (factory.CreateSocket (s, host, port, autoClose));
		}

		public override Socket CreateSocket ()
		{
			return EnableTlsOnSocket (factory.CreateSocket ());
		}

		private Socket EnableTlsOnSocket (Socket socket)
		{
			if (socket is SSLSocket sslSocket) {
				sslSocket.SetEnabledProtocols (sslSocket.GetSupportedProtocols ());
			}
			return socket;
		}
	}
}
