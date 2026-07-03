using System;
using Java.Interop.Tools.Generator;
using NUnit.Framework;

namespace Java.Interop.Tools.Generator_Tests;

[TestFixture]
public class AndroidSdkVersionTests
{
	[Test]
	public void CompareTo()
	{
		var a   = new AndroidSdkVersion (1);
		var b   = new AndroidSdkVersion (1);
		var c   = new AndroidSdkVersion (36, 1);
		Assert.AreEqual (0, a.CompareTo (b), $"1 == 1");
		Assert.AreEqual (1, ((IComparable)a).CompareTo (null), $"1 < null");
		Assert.IsTrue (a.CompareTo (c) < 0, $"1 < 36.1");
		Assert.IsTrue (c.CompareTo (a) > 0, $"36.1 > 1");
	}

	[Test]
	public void Operators ()
	{
		var a   = new AndroidSdkVersion (1);
		var b   = new AndroidSdkVersion (1);
		var c   = new AndroidSdkVersion (36, 1);

		Assert.IsFalse (a < b);
		Assert.IsTrue (a <= b);
		Assert.IsTrue (a < c);
		Assert.IsTrue (a <= c);

		Assert.IsFalse (a > b);
		Assert.IsTrue (a >= b);
		Assert.IsTrue (c > a);
		Assert.IsTrue (c >= a);
	}

	[Test]
	public void TryParse ()
	{
		AndroidSdkVersion v;
		Assert.IsFalse (AndroidSdkVersion.TryParse (null, out v));
		Assert.IsFalse (AndroidSdkVersion.TryParse ("", out v));

		Assert.IsTrue (AndroidSdkVersion.TryParse ("1", out v));
		Assert.AreEqual (1, v.ApiLevel);
		Assert.AreEqual (0, v.MinorRelease);

		Assert.IsTrue (AndroidSdkVersion.TryParse ("36.1", out v));
		Assert.AreEqual (36,    v.ApiLevel);
		Assert.AreEqual (1,     v.MinorRelease);
	}

	[Test]
	public new void ToString ()
	{
		Assert.AreEqual ("0",       new AndroidSdkVersion (0).ToString ());
		Assert.AreEqual ("36",      new AndroidSdkVersion (36).ToString ());
		Assert.AreEqual ("36.1",    new AndroidSdkVersion (36, 1).ToString ());
	}
}
