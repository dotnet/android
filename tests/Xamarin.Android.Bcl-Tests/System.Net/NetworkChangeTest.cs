using System;
using System.Net;
using System.Net.NetworkInformation;

using NUnit.Framework;

namespace BclTests {

	[TestFixture]
	public class NetworkChangeTest
	{
		[Test]
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

