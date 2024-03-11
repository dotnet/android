using System.Linq;
using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven_Tests;

public class MavenVersionTests
{
	[Test]
	public void ParseTest ()
	{
		TestParse ("", null, null, null, false);
		TestParse ("1", "1", null, null, true);
		TestParse ("1.2", "1", "2", null, true);
		TestParse ("1.2.3", "1", "2", "3", true);

		TestParse ("1-BETA", "1-BETA", null, null, true);
		TestParse ("1-ALPHA.2-BETA", "1-ALPHA", "2-BETA", null, true);
		TestParse ("1-ALPHA.2-BETA.3-GAMMA", "1-ALPHA", "2-BETA", "3-GAMMA", true);

		TestParse ("1-ALPHA-RC3.2.3", "1-ALPHA-RC3", "2", "3", true);

		// 4th part isn't valid
		TestParse ("1.2.3.4", "1", "2", "3", false);

		// Versions must be numeric
		TestParse ("A.B.C", "A", "B", "C", false);
		TestParse ("A-B.2.3", "A-B", "2", "3", false);
	}

	[Test]
	public void SortTest ()
	{
		// Normal versions
		TestSort ("1,2", "1", "2");
		TestSort ("1,2", "2", "1");
		TestSort ("1.1,1.2", "1.2", "1.1");
		TestSort ("1.1.1,1.1.2", "1.1.2", "1.1.1");

		// Qualifiers are always "before" "release" versions when version numbers are equal
		TestSort ("1.2-beta-2,1.2", "1.2", "1.2-beta-2");
		TestSort ("1-beta-2,1", "1", "1-beta-2");
		TestSort ("1.1.1-beta-2,1.1.1", "1.1.1", "1.1.1-beta-2");

		// Qualifiers don't matter if the versions don't match
		TestSort ("1-RC,2", "1-RC", "2");
		TestSort ("1,2-RC", "1", "2-RC");

		// Qualifiers are sorted with simple string sort
		TestSort ("1.2-alpha-6,1.2-beta-2", "1.2-alpha-6", "1.2-beta-2");

		// If any version is "nonstandard", a simple string sort is used
		TestSort ("1.0.1.0,1.0.10.1,1.0.10.2,1.0.9.3", "1.0.9.3", "1.0.10.1", "1.0.1.0", "1.0.10.2");
	}

	static void TestParse (string input, string? major, string? minor, string? patch, bool isValid)
	{
		var v = MavenVersion.Parse (input);

		Assert.AreEqual (major, v.Major);
		Assert.AreEqual (minor, v.Minor);
		Assert.AreEqual (patch, v.Patch);
		Assert.AreEqual (isValid, v.IsValid);
	}

	static void TestSort (string expected, params string [] values)
	{
		var sorted = values.Select (v => MavenVersion.Parse (v)).Order ();
		var formatted = string.Join (',', sorted.Select (v => v.RawVersion));

		Assert.AreEqual (expected, formatted);
	}
}
