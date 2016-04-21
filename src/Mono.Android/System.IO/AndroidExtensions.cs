using System;
using System.Net.Sockets;

namespace System.IO {

	public static class AndroidExtensions {

		public static bool IsDataAvailable (this Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");

			var ns = stream as NetworkStream;
			if (ns != null)
				return ns.DataAvailable;

			var isi = stream as Android.Runtime.InputStreamInvoker;
			if (isi != null)
				return isi.BaseInputStream.Available () > 0;

			return false;
		}
	}
}
