using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tools.BytecodeTests {

	static class ConfiguredJdkInfo {

		static JdkInfo info;

		static ConfiguredJdkInfo ()
		{
			var jdkPath = ReadJavaSdkDirectoryFromJdkInfoProps ();
			if (jdkPath == null)
				return;
			info    = new JdkInfo (jdkPath);
		}

		public static Version Version => info?.Version;

		static string ReadJavaSdkDirectoryFromJdkInfoProps ()
		{
			var location    = typeof (ConfiguredJdkInfo).Assembly.Location;
			var binDir      = Path.GetDirectoryName (Path.GetDirectoryName (location));
			var testDir     = Path.GetFileName (Path.GetDirectoryName (location));
			if (!testDir.StartsWith ("Test", StringComparison.OrdinalIgnoreCase)) {
				return null;
			}
			var buildName   = testDir.Replace ("Test", "Build");
			if (buildName.IndexOf ("-", StringComparison.Ordinal) >= 0) {
				buildName   = buildName.Substring (0, buildName.IndexOf ('-'));
			}
			var jdkPropFile = Path.Combine (binDir, buildName, "JdkInfo.props");
			if (!File.Exists (jdkPropFile)) {
				return null;
			}

			var msbuild     = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");

			var jdkProps    = XDocument.Load (jdkPropFile);
			var jdkPath     = jdkProps.Elements ()
				.Elements (msbuild + "PropertyGroup")
				.Elements (msbuild + "JavaSdkDirectory")
				.FirstOrDefault ();
			if (jdkPath == null) {
				return null;
			}
			return jdkPath.Value;
		}
	}
}
