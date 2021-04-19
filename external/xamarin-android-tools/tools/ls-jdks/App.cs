using System;

namespace Xamarin.Android.Tools
{
	class App
	{
		static void Main(string[] args)
		{
			foreach (var jdk in JdkInfo.GetKnownSystemJdkInfos ()) {
				Console.WriteLine ($"Found JDK: {jdk.HomePath}");
				Console.WriteLine ($"  Locator: {jdk.Locator}");
			}
		}
	}
}
