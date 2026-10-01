using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

public class BaseTestTests : BaseTest
{
	[TestCase ("Trailing spaces", TestName = "Trailing spaces {}")]
	[TestCase ("Trailing periods", TestName = "Trailing periods...")]
	[TestCase ("Trailing spaces and periods", TestName = "Trailing spaces and periods . . ")]
	[TestCase ("Internal spaces remain", TestName = "Internal spaces remain")]
	public void TestNamesAreSafeDirectoryComponents (string expected)
	{
		Assert.AreEqual (expected, TestName);
	}
}
