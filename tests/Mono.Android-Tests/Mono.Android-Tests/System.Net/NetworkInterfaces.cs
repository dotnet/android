using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Java.Net;

using NUnit.Framework;

using MNetworkInterface = System.Net.NetworkInformation.NetworkInterface;
using JNetworkInterface = Java.Net.NetworkInterface;

namespace System.NetTests
{
	[TestFixture]
	public class NetworkInterfacesTest
	{
		sealed class InterfaceInfo
		{
			public string Name { get; set; }
			public bool IsLoopback { get; set; }
			public bool IsUp { get; set; }
			public byte[] HardwareAddress { get; set; }
			public List <IPAddress> Addresses { get; set; }

			public override string ToString ()
			{
				return string.Format ("[InterfaceInfo: Name={0}, IsLoopback={1}, IsUp={2}, HardwareAddress={3}, Addresses={4}]", Name, IsLoopback, IsUp, HardwareAddressToString (), AddressesToString ());
			}

			public override bool Equals (object obj)
			{
				if (obj == null)
					return false;
				if (obj == this)
					return true;

				var other = obj as InterfaceInfo;
				if (other == null)
					return false;

				return Name == other.Name &&
					IsLoopback == other.IsLoopback &&
					IsUp == other.IsUp &&
					HardwareAddressesAreEqual (HardwareAddress, other.HardwareAddress) &&
					AddressesAreEqual (Addresses, other.Addresses);
			}

			string HardwareAddressToString ()
			{
				if (HardwareAddress == null || HardwareAddress.Length == 0)
					return "<>";
				var ret = new List<string> ();
				foreach (byte b in HardwareAddress)
					ret.Add (b.ToString ("X02"));
				return "<" + String.Join (":", ret) + ">";
			}

			string AddressesToString ()
			{
				if (Addresses == null || Addresses.Count == 0)
					return "<>";
				var ret = new List<string> ();
				foreach (IPAddress a in Addresses)
					ret.Add (a.ToString ());
				return "<" + String.Join (", ", ret) + ">";
			}

			bool AddressesAreEqual (List <IPAddress> one, List <IPAddress> two)
			{
				if (one == two)
					return true;
				if (one == null || two == null)
					return false;
				if (one.Count != two.Count)
					return false;

				foreach (IPAddress addr in one) {
					if (!two.Contains (addr))
						return false;
				}

				return true;
			}

			bool HardwareAddressesAreEqual (byte[] one, byte[] two)
			{
				// Under API 33 .Net doesn't return the hardware address. So we need to ignore it
				if (Android.OS.Build.VERSION.SdkInt == Android.OS.BuildVersionCodes.Tiramisu)
					return true;
				if (one == two)
					return true;
				if (one == null || two == null)
					return false;
				if (one.Length != two.Length)
					return false;

				for (int i = 0; i < one.Length; i++) {
					if (one [i] != two [i])
						return false;
				}

				return true;
			}
		}

		[Test, Category("NetworkInterfaces")]
		public void DotNetInterfacesShouldEqualJavaInterfaces ()
		{
			List <InterfaceInfo> dotnetInterfaces = GetInfos (MNetworkInterface.GetAllNetworkInterfaces ());
			List <InterfaceInfo> javaInterfaces = GetInfos (JNetworkInterface.NetworkInterfaces);

			Console.WriteLine ("Mono interfaces:");
			foreach (InterfaceInfo inf in dotnetInterfaces)
				 Console.WriteLine (inf);

			Console.WriteLine ("Java interfaces:");
			foreach (InterfaceInfo inf in javaInterfaces)
				Console.WriteLine (inf);

			Assert.IsNotNull (dotnetInterfaces, "#1.1");
			Assert.IsTrue (dotnetInterfaces.Count > 0, "#1.2");

			Assert.IsNotNull (javaInterfaces, "#2.1");
			Assert.IsTrue (javaInterfaces.Count > 0, "#2.2");

			Assert.AreEqual (dotnetInterfaces.Count, javaInterfaces.Count, "#3.1");

			int counter = 4;
			foreach (InterfaceInfo inf in dotnetInterfaces) {
				counter++;
				Assert.IsNotNull (inf, String.Format ("#{0}.1", counter));
				Assert.IsFalse (String.IsNullOrEmpty (inf.Name), String.Format ("#{0}.2", counter));
				Assert.IsTrue (javaInterfaces.Contains (inf), "#{0}.3 ({1} not found in Java interfaces)", counter, inf.Name);
				Console.WriteLine ("Interface {0}: passed", inf.Name);
			}
		}

		List <IPAddress> CollectAddresses (MNetworkInterface inf)
		{
			var ret = new List <IPAddress> ();

			foreach (UnicastIPAddressInformation addr in inf.GetIPProperties ().UnicastAddresses)
				ret.Add (addr.Address);

			return ret;
		}

		List <IPAddress> CollectAddresses (JNetworkInterface inf)
		{
			var ret = new List <IPAddress> ();

			Java.Util.IEnumeration addresses = inf.InetAddresses;
			while (addresses.HasMoreElements) {
				var addr = addresses.NextElement () as InetAddress;
				if (addr == null)
					continue;
				var ipv6 = addr as Inet6Address;
				if (ipv6 != null && (ipv6.IsLinkLocalAddress || ipv6.IsMCLinkLocal))
					ret.Add (new IPAddress (addr.GetAddress (), ipv6.ScopeId));
				else
					ret.Add (new IPAddress (addr.GetAddress ()));
			}

			return ret;
		}

		bool IsInterfaceUp (MNetworkInterface inf)
		{
			switch (inf.OperationalStatus) {
				case OperationalStatus.Dormant:
				case OperationalStatus.Up:
					return true;

				default:
					// Android considers 'lo' to be always up
					return IsLoopbackInterface (inf);
			}
		}

		byte[] GetHardwareAddress (MNetworkInterface inf)
		{
			byte[] bytes = inf.GetPhysicalAddress ().GetAddressBytes ();
			// Map to android's idea of device address
			if (bytes.Length == 0 || inf.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
				return null;
			// if all the bytes are zero return null like Java does.
			if (bytes.All (x => x == 0))
				return null;

			return bytes;
		}

		List <InterfaceInfo> GetInfos (MNetworkInterface[] interfaces)
		{
			var ret = new List <InterfaceInfo> ();

			foreach (MNetworkInterface inf in interfaces) {
				Console.WriteLine ($"inf: {inf} (name: {inf.Name}; type: {inf.NetworkInterfaceType})");
				ret.Add (new InterfaceInfo {
					Name = inf.Name,
					IsLoopback = IsLoopbackInterface (inf),
					IsUp = IsInterfaceUp (inf),
					HardwareAddress = GetHardwareAddress (inf),
					Addresses = CollectAddresses (inf)
				});
			}

			return ret;
		}

		List <InterfaceInfo> GetInfos (Java.Util.IEnumeration interfaces)
		{
			var ret = new List <InterfaceInfo> ();

			while (interfaces.HasMoreElements) {
				var inf = interfaces.NextElement () as JNetworkInterface;
				if (inf == null)
					continue;

				ret.Add (new InterfaceInfo {
					Name = inf.Name,
					IsLoopback = inf.IsLoopback,
					IsUp = inf.IsUp,
					HardwareAddress = inf.GetHardwareAddress (),
					Addresses = CollectAddresses (inf)
				});
			}

			return ret;
		}

		static bool IsLoopbackInterface (MNetworkInterface inf)
		{
			// Android 30 will not tell us the interface type if the app targets API 30, we need to look at the
			// name then.
			return inf.NetworkInterfaceType == NetworkInterfaceType.Loopback || String.Compare ("lo", inf.Name, StringComparison.OrdinalIgnoreCase) == 0;
		}
	}
}
