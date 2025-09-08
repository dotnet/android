using System;
using Java.Interop.Tools.Generator;
using NUnit.Framework;

namespace Java.Interop.Tools.Generator_Tests;

[TestFixture]
public class NamingConverterTests
{
	[Test]
	public void ParseApiLevel_Exceptions ()
	{
		Assert.Throws<NotSupportedException> (() => NamingConverter.ParseApiLevel (@"..\..\bin\BuildDebug\api\api-CANARY.xml.in"));
	}

	[Test]
	public void ParseApiLevel ()
	{
		AndroidSdkVersion v;

		v = NamingConverter.ParseApiLevel (@"..\..\bin\BuildDebug\api\api-28.xml.in");
		Assert.AreEqual (28,    v.ApiLevel);
		Assert.AreEqual (0,     v.MinorRelease);

		v = NamingConverter.ParseApiLevel (@"..\..\bin\BuildDebug\api\api-36.1.xml.in");
		Assert.AreEqual (36,    v.ApiLevel);
		Assert.AreEqual (1,     v.MinorRelease);

		v = NamingConverter.ParseApiLevel (@"..\..\bin\BuildDebug\api\api-R.xml.in");
		Assert.AreEqual (30,    v.ApiLevel);
		Assert.AreEqual (0,     v.MinorRelease);
	}
}
