using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.UnitTests;

public static class AndroidTestAssert
{
	public static void DoesNotThrow (Action action, string? message = null)
	{
		try {
			action ();
		} catch (Exception exception) {
			Assert.Fail ($"{message} Unexpected exception: {exception}");
		}
	}
}
