using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests
{
	[TestFixture]
	public class LocalizationTests
	{
		// https://bugzilla.xamarin.com/show_bug.cgi?id=31705
		[Test]
		public void EmbeddedResources_ShouldBeLocalized ()
		{
			CultureInfo culture = Thread.CurrentThread.CurrentCulture;
			CultureInfo uiCulture = Thread.CurrentThread.CurrentUICulture;

			Assert.AreEqual ("a", AppResources.String1, "Embedded string resource did not contain expected value.");

			Thread.CurrentThread.CurrentCulture = new CultureInfo ("it-IT");
			Thread.CurrentThread.CurrentUICulture = new CultureInfo ("it-IT");

			Assert.AreEqual ("b", AppResources.String1,
				"Embedded string resource did not contain expected value after changing CultureInfo.");

			Thread.CurrentThread.CurrentCulture = culture;
			Thread.CurrentThread.CurrentUICulture = uiCulture;
		}
	}
}
