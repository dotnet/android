using System.Linq;
using Android.Runtime;
using Java.Security;
using Javax.Net.Ssl;
using Microsoft.Android.Runtime;
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

		[Test]
		public void JavaInterfaceLookup_BaseInterfaceReturnType_UsesDerivedInterfaceProxy ()
		{
			AssumeTrimmableTypeMapEnabled ();

			// Mirrors API 21-23 TrustManagerImpl: the Java signature returns the
			// base interface, but the concrete object advertises a derived interface.
			using var provider = global::Net.Dot.Android.Test.InterfaceMarshalling.ExtendedValueProviderAsValueProvider;
			Assert.IsNotNull (provider, "Expected Java fixture to return a ValueProvider instance.");

			if (provider is not global::Net.Dot.Android.Test.IExtendedValueProvider extendedProvider) {
				Assert.Fail ($"Expected ValueProvider to be marshalled as IExtendedValueProvider. Type found: {provider.GetType ().FullName}");
				return;
			}

			Assert.AreEqual (42, provider.Value);
			Assert.AreEqual (84, extendedProvider.OtherValue);
		}

		static void AssumeTrimmableTypeMapEnabled ()
		{
			if (!IsTrimmableTypeMapEnabled ()) {
				Assert.Ignore ("TrimmableTypeMap feature switch is off; test only relevant for the trimmable typemap path.");
			}
		}

		static bool IsTrimmableTypeMapEnabled ()
			=> System.AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap", out bool isEnabled) && isEnabled;
	}
}
