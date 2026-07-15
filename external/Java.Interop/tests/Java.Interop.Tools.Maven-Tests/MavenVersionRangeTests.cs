using System.Linq;
using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven_Tests;

public class MavenVersionRangeTests
{
	[Test]
	public void ParseTest ()
	{
		TestSingleRange ("1.0", "1.0", null, true, false, true, false);
		TestSingleRange ("(,1.0]", null, "1.0", false, true, false, true);
		TestSingleRange ("[1.0]", "1.0", "1.0", true, true, true, true);
		TestSingleRange ("[1.2,1.3]", "1.2", "1.3", true, true, true, true);
		TestSingleRange ("[1.0,2.0)", "1.0", "2.0", true, true, true, false);
		TestSingleRange ("[1.5,)", "1.5", null, true, false, true, false);

		var multi_range_1 = MavenVersionRange.Parse ("(,1.0],[1.2,)").ToArray ();
		TestSingleRange (multi_range_1 [0], null, "1.0", false, true, false, true);
		TestSingleRange (multi_range_1 [1], "1.2", null, true, false, true, false);

		var multi_range_2 = MavenVersionRange.Parse ("(,1.1),(1.1,)").ToArray ();
		TestSingleRange (multi_range_2 [0], null, "1.1", false, true, false, false);
		TestSingleRange (multi_range_2 [1], "1.1", null, true, false, false, false);
	}

	[Test]
	public void ContainsVersionTest ()
	{
		TestContainsVersion ("1.0", false, true, true, true, true);
		TestContainsVersion ("(,1.0]", true, true, false, false, false);
		TestContainsVersion ("[1.0]", false, true, false, false, false);
		TestContainsVersion ("[1.0,2.0]", false, true, true, true, false);
		TestContainsVersion ("(1.0,2.0)", false, false, true, false, false);
		TestContainsVersion ("[1.5,)", false, false, true, true, true);
	}

	static void TestSingleRange (string value, string? minVersion, string? maxVersion, bool hasLowerBound, bool hasUpperBound, bool isMinInclusive, bool isMaxInclusive)
		=> TestSingleRange (MavenVersionRange.Parse (value).Single (), minVersion, maxVersion, hasLowerBound, hasUpperBound, isMinInclusive, isMaxInclusive);

	static void TestSingleRange (MavenVersionRange range, string? minVersion, string? maxVersion, bool hasLowerBound, bool hasUpperBound, bool isMinInclusive, bool isMaxInclusive)
	{
		Assert.AreEqual (hasLowerBound, range.HasLowerBound);
		Assert.AreEqual (hasUpperBound, range.HasUpperBound);
		Assert.AreEqual (minVersion, range.MinVersion);
		Assert.AreEqual (maxVersion, range.MaxVersion);
		Assert.AreEqual (isMinInclusive, range.IsMinInclusive);
		Assert.AreEqual (isMaxInclusive, range.IsMaxInclusive);
	}

	static void TestContainsVersion (string value, bool contains0_8, bool contains1_0, bool contains1_5, bool contains2_0, bool contains2_5)
	{
		var range = MavenVersionRange.Parse (value).Single ();

		Assert.AreEqual (contains0_8, range.ContainsVersion (MavenVersion.Parse ("0.8")));
		Assert.AreEqual (contains1_0, range.ContainsVersion (MavenVersion.Parse ("1.0")));
		Assert.AreEqual (contains1_5, range.ContainsVersion (MavenVersion.Parse ("1.5")));
		Assert.AreEqual (contains2_0, range.ContainsVersion (MavenVersion.Parse ("2.0")));
		Assert.AreEqual (contains2_5, range.ContainsVersion (MavenVersion.Parse ("2.5")));
	}
}
