using System;
using System.Reflection;

using NUnit.Framework;

namespace Xamarin.Android.LocaleTests
{
	[TestFixture]
	public class TimeZoneTests
	{
		[Test (Description="Test for xambug #4902")]
		public void GetDefaultTimeZone_NoPersistSysTimeZone ()
		{
			Assert.AreEqual (TimeZoneInfo.Local.DisplayName, Java.Util.TimeZone.Default.ID, "#1");
		}
	}
}

