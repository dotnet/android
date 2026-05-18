using System.Linq;
using Android.Runtime;
using Java.Security;
using Javax.Net.Ssl;
using NUnit.Framework;

namespace Xamarin.Android.NetTests
{
	[TestFixture]
	public class TrustManagerMarshallingTests
	{
		[Test]
		public void TrustManagerFactory_GetTrustManagers_ReturnsIX509TrustManager ()
		{
			var tmf = TrustManagerFactory.GetInstance (TrustManagerFactory.DefaultAlgorithm);
			tmf.Init ((KeyStore?) null);

			var trustManagers = tmf.GetTrustManagers ();
			Assert.IsNotNull (trustManagers, "GetTrustManagers returned null");
			Assert.IsTrue (trustManagers.Length > 0, "GetTrustManagers returned empty array");

			bool foundX509 = false;
			foreach (var tm in trustManagers) {
				if (tm is IX509TrustManager) {
					foundX509 = true;
				}
			}

			Assert.IsTrue (foundX509,
				$"No ITrustManager element was marshalled as IX509TrustManager. " +
				$"Types found: {string.Join (", ", trustManagers.Select (t => t.GetType ().FullName))}");
		}
	}
}
