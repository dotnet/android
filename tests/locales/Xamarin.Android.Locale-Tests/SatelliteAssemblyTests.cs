using System;
using System.Globalization;

using NUnit.Framework;

namespace Xamarin.Android.LocaleTests {

	[TestFixture]
	public class SatelliteAssemblyTests {

		[Test]
		public void SatelliteAssemblyValues ()
		{
			var en  = CultureInfo.GetCultureInfo ("en-US");	// fallback values
			var de  = CultureInfo.GetCultureInfo ("de-DE");
			var fr  = CultureInfo.GetCultureInfo ("fr-FR");

			var app = new System.Resources.ResourceManager ("Xamarin.Android.LocaleTests.strings", this.GetType ().Assembly);

			CheckSatelliteString ("AppString", en, "App English", app.GetString);
			CheckSatelliteString ("AppString", de, "App German",  app.GetString);
			CheckSatelliteString ("AppString", fr, "App French",  app.GetString);

			CheckSatelliteString ("LibString", en, "Lib English", LibraryResources.SatelliteResources.GetString);
			CheckSatelliteString ("LibString", de, "Lib German",  LibraryResources.SatelliteResources.GetString);
			CheckSatelliteString ("LibString", fr, "Lib French",  LibraryResources.SatelliteResources.GetString);
		}

		static void CheckSatelliteString (string resourceName, CultureInfo culture, string expected, Func<string, CultureInfo, string> getString)
		{
			var actual = getString (resourceName, culture);
			if (actual != expected)
				throw new InvalidOperationException (
					string.Format ("Satellite assembly resource lookup failed for resource '{0}' from culture '{1}'.\n" +
				               "Expected='{2}'\n" +
				               "  Actual='{3}'",
				               resourceName, culture.Name, expected, actual));
		}
	}
}

