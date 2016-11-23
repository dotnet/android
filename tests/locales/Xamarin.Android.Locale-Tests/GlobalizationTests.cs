using System;
using System.Globalization;

using NUnit.Framework;

namespace Xamarin.Android.LocaleTests {

	[TestFixture]
	public class GlobalizationTests {

		[Test]
		public void CultureInfo_CurrentCultureIsFrench ()
		{
			// CultureInfo.CurrentCulture should be French because
			// Environment.txt sets LANG=fr-FR.
			Assert.IsNotNull (CultureInfo.CurrentCulture);
			Assert.AreEqual ("French (France)", CultureInfo.CurrentCulture.DisplayName);
		}

		[Test]
		public void CultureInfo_CurrentUICultureIsFrench ()
		{
			// CultureInfo.CurrentCulture should be French because
			// Environment.txt sets LANG=fr-FR.
			Assert.IsNotNull (CultureInfo.CurrentUICulture);
			Assert.AreEqual ("French (France)", CultureInfo.CurrentUICulture.DisplayName);
		}
	}
}

