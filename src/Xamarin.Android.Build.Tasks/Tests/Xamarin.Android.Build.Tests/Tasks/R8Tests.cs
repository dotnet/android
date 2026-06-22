using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class R8Tests
	{
		[TestCase ("-keep class com.example.Foo { *; }", false, "")]
		[TestCase ("-dontwarn com.example.**",           false, "")]
		[TestCase ("# -printmapping comment",            false, "")]
		[TestCase ("",                                   false, "")]
		[TestCase ("-printmapping mapping.txt",          true,  "-printmapping")]
		[TestCase ("  -printmapping mapping.txt",        true,  "-printmapping")]
		[TestCase ("\t-printseeds seeds.txt",            true,  "-printseeds")]
		[TestCase ("-printusage usage.txt",              true,  "-printusage")]
		[TestCase ("-printconfiguration config.txt",     true,  "-printconfiguration")]
		[TestCase ("-dump dump.txt",                     true,  "-dump")]
		[TestCase ("-PrintMapping mapping.txt",          true,  "-printmapping")] // case-insensitive
		[TestCase ("-DUMP dump.txt",                     true,  "-dump")]
		public void TryGetDisallowedOption (string line, bool expected, string expectedOption)
		{
			var actual = R8.TryGetDisallowedOption (line, out var option);
			Assert.AreEqual (expected, actual);
			Assert.AreEqual (expectedOption, option);
		}
	}
}

