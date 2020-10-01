using System;
using System.Net;
using System.Net.Sockets;

namespace LinkTestLib
{
	// https://bugzilla.xamarin.com/show_bug.cgi?id=21578
	// https://bugzilla.xamarin.com/show_bug.cgi?id=22183
	public static class Bug21578
	{
		public static string MulticastOption_ShouldNotBeStripped ()
		{
			try {
				using (var client = new UdpClient ()) {
					var multicastAddress = IPAddress.Parse ("224.0.0.251");
					const int ifaceIndex = 0;
					var multOpt = new MulticastOption (multicastAddress, ifaceIndex);
					client.Client.SetSocketOption (SocketOptionLevel.IP, SocketOptionName.AddMembership, multOpt);
					return "[PASS] SetSocketOption was not stripped.";
				}
			} catch (Exception ex) {
				if (ex is SocketException && ex.Message.Contains("Network subsystem is down")) {
					return "[IGNORE] SetSocketOption test was inconclusive.";
				}
				return $"[FAIL] SetSocketOption was stripped!\n{ex}";
			}
		}

		public static string MulticastOption_ShouldNotBeStripped2 ()
		{
			try {
				using (var clientWriter = new UdpClient ()) {
					var multicastAddress = IPAddress.Parse ("224.0.0.224");
					clientWriter.JoinMulticastGroup (multicastAddress);
					return "[PASS] JoinMulticastGroup was not stripped";
				}
			} catch (Exception ex) {
				if (ex is SocketException && ex.Message.Contains ("Network subsystem is down")) {
					return "[IGNORE] MulticastGroup test was inconclusive.";
				}
				return $"[FAIL] SetSocketOption was stripped!\n{ex}";
			}
		}
	}
}
