using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class JavaToolTaskTests
	{
		[TestMethod]
		[DataRow (null, null, "")]
		[DataRow ("-Dfoo=bar", "21.0.8", "-Dfoo=bar")]
		[DataRow (null, "25.0.4.1", "--enable-native-access=ALL-UNNAMED --sun-misc-unsafe-memory-access=allow")]
		[DataRow ("-Dfoo=bar", "25.0.4.1", "-Dfoo=bar --enable-native-access=ALL-UNNAMED --sun-misc-unsafe-memory-access=allow")]
		[DataRow ("--enable-native-access=ALL-UNNAMED", "25.0.4.1", "--enable-native-access=ALL-UNNAMED --sun-misc-unsafe-memory-access=allow")]
		public void GetJavaOptions (string? javaOptions, string? jdkVersion, string expected)
		{
			Assert.AreEqual (expected, JavaToolTask.GetJavaOptions (javaOptions, jdkVersion));
		}
	}
}
