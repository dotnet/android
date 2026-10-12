using System;
using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class PathUtilTests
{
	[TestCase ("foo", "foo", ".", StringComparison.Ordinal)]
	[TestCase ("foo", "foo/bar", "bar", StringComparison.Ordinal)]
	[TestCase ("foo/bar", "foo", "..", StringComparison.Ordinal)]
	[TestCase ("foo", "food", "../food", StringComparison.Ordinal)]
	[TestCase ("FOO", "foo/bar", "bar", StringComparison.OrdinalIgnoreCase)]
	[TestCase ("foo/bar", "foo/baz", "../baz", StringComparison.Ordinal)]
	public void RelativePathPreservesCommonPrefixComparison (
		string from, string to, string expected, StringComparison comparison)
	{
		string root = Path.GetFullPath (".");
		Assert.AreEqual (expected.Replace ('/', Path.DirectorySeparatorChar),
			PathUtil.GetRelativePath (Path.Combine (root, from), Path.Combine (root, to), comparison));
	}
}
