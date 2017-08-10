using System;
using System.Reflection;

using NUnit.Framework;

namespace Xamarin.Android.LocaleTests
{
	[TestFixture]
	public class EnvironmentTests
	{
		[Test (Description="https://bugzilla.xamarin.com/show_bug.cgi?id=58673")]
		public void EnvironmentVariablesFromLibraryProjectsAreMerged ()
		{
			var v = Environment.GetEnvironmentVariable ("THIS_IS_MY_ENVIRONMENT");
			Assert.AreEqual (v, "Well, hello there!");
		}
	}
}

