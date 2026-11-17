using System.Net.NetworkInformation;

namespace System.NetTests; 

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