using System;
using Java.Interop.Tools.Generator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.Interop.Tools.Generator_Tests;

[TestClass]
public class NamingConverterTests
{
	[TestMethod]
	public void ParseApiLevel_Exceptions ()
	{
		Assert.ThrowsExactly<NotSupportedException> (() => NamingConverter.ParseApiLevel (@"..\..\bin\BuildDebug\api\api-CANARY.xml.in"));
	}

	[TestMethod]
	public void ParseApiLevel ()
	{
		AndroidSdkVersion v;

		v = NamingConverter.ParseApiLevel (@"..\..\bin\BuildDebug\api\api-28.xml.in");
		Assert.AreEqual (28,    v.ApiLevel);
		Assert.AreEqual (0,     v.MinorRelease);

		v = NamingConverter.ParseApiLevel (@"…\Xamarin-Work\…\bin\BuildDebug\api\api-36.1.xml.in");
		Assert.AreEqual (36,    v.ApiLevel);
		Assert.AreEqual (1,     v.MinorRelease);

		v = NamingConverter.ParseApiLevel (@"..\..\bin\BuildDebug\api\api-36.1.xml.in");
		Assert.AreEqual (36,    v.ApiLevel);
		Assert.AreEqual (1,     v.MinorRelease);

		v = NamingConverter.ParseApiLevel (@"..\..\bin\BuildDebug\api\api-R.xml.in");
		Assert.AreEqual (30,    v.ApiLevel);
		Assert.AreEqual (0,     v.MinorRelease);
	}
}
