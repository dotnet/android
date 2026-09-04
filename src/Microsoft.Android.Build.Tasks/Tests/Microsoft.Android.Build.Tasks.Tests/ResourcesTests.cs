using System.Globalization;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ResourcesTests
{
	[Test]
	public void LocalizedResourcesAreAvailable ()
	{
		var originalCulture = CultureInfo.CurrentUICulture;
		try {
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo ("de");

			StringAssert.StartsWith ("Der Typ", Xamarin.Android.Tasks.Properties.Resources.XA4250);
			StringAssert.StartsWith ("[ExportField] kann", Java.Interop.Localization.Resources.JavaCallableWrappers_XA4205);
		} finally {
			CultureInfo.CurrentUICulture = originalCulture;
		}
	}
}
