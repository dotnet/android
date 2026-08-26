using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class JavaToolTaskTests
	{
		[TestCase (null, null, "")]
		[TestCase ("-Dfoo=bar", "21.0.8", "-Dfoo=bar")]
		[TestCase (null, "25.0.4.1", "--enable-native-access=ALL-UNNAMED --sun-misc-unsafe-memory-access=allow")]
		[TestCase ("-Dfoo=bar", "25.0.4.1", "-Dfoo=bar --enable-native-access=ALL-UNNAMED --sun-misc-unsafe-memory-access=allow")]
		[TestCase ("--enable-native-access=ALL-UNNAMED", "25.0.4.1", "--enable-native-access=ALL-UNNAMED --sun-misc-unsafe-memory-access=allow")]
		public void GetJavaOptions (string? javaOptions, string? jdkVersion, string expected)
		{
			Assert.AreEqual (expected, JavaToolTask.GetJavaOptions (javaOptions, jdkVersion));
		}
	}
}
