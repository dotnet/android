using System;

namespace Xamarin.Android.Tools
{
	class App
	{
		static void Main(string[] args)
		{
			foreach (var path in args) {
				PrintProperties (path);
			}
			if (args.Length != 0)
				return;
			foreach (var jdk in JdkInfo.GetKnownSystemJdkInfos ()) {
				Console.WriteLine ($"Found JDK: {jdk.HomePath}");
				Console.WriteLine ($"  Locator: {jdk.Locator}");
				// Force parsing of java properties.
				var keys = jdk.JavaSettingsPropertyKeys;
			}
		}

		static void PrintProperties (string jdkPath)
		{
			try {
				var jdk = new JdkInfo (jdkPath, "ls-jdks");
				Console.WriteLine ($"Property settings for JDK Path: {jdk.HomePath}");
				foreach (var key in jdk.JavaSettingsPropertyKeys) {
					if (!jdk.GetJavaSettingsPropertyValues (key, out var v)) {
						Console.Error.WriteLine ($"ls-jdks: Could not retrieve value for key {key}.");
						continue;
					}
					Console.WriteLine ($"    {key} = {string.Join (Environment.NewLine + "        ", v)}");
				}
			}
			catch (Exception e) {
				Console.Error.WriteLine (e);
			}
		}
	}
}
