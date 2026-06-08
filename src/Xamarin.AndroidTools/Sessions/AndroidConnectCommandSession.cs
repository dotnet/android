//
// AndroidConnectCommandConnection.cs
//
// Author:
//       Stephen Shaw <shaw@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Xamarin.AndroidTools
{
	class AndroidConnectCommandSession : AndroidCommandSession
	{
		Socket client;

		public AndroidConnectCommandSession (IPAddress ipAddress = null, int port = 0) : base (ipAddress, port)
		{
			client = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			client.ExclusiveAddressUse = false;
		}

		protected override IAsyncResult BeginConnectStream (AsyncCallback callback, object state)
		{
			return client.BeginConnect (new IPEndPoint (Address, Port), callback, state);
		}

		protected override Stream EndConnectStream (IAsyncResult result)
		{
			client.EndConnect (result);
			client.NoDelay = true;
			return new NetworkStream (client, true);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && client != null) {
				client.Dispose ();
				client = null;
			}
		}
	}
}
