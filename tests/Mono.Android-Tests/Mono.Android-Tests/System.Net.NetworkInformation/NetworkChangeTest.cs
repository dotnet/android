using System;
using System.Net;
using System.Net.NetworkInformation;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.NetTests {

	[TestClass]
	public class NetworkChangeTest
	{
		[TestMethod]
		public void NetworkAvailabilityChanged ()
		{
			NetworkAvailabilityChangedEventHandler h = (o, e) => {
				Console.WriteLine ("NetworkAvailabilityChanged called");
			};
			NetworkChange.NetworkAvailabilityChanged += h;
			NetworkChange.NetworkAvailabilityChanged -= h;
		}
	}
}