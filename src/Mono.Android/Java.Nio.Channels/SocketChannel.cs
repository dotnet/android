using System;
using Java.Net;

namespace Java.Nio.Channels
{
	partial class SocketChannel
	{
		INetworkChannel INetworkChannel.Bind (SocketAddress address)
		{
			return Bind (address);
		}
		INetworkChannel INetworkChannel.SetOption (ISocketOption option, Java.Lang.Object value)
		{
			return SetOption (option, value);
		}
	}
}
