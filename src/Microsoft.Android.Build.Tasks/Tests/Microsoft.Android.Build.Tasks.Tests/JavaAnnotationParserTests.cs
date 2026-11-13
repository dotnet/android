using System.Collections.Generic;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class JavaAnnotationParserTests
{
	[TestCase (0L, "0L")]
	[TestCase (long.MaxValue, "9223372036854775807L")]
	[TestCase (long.MinValue, "-9223372036854775808L")]
	public void LongValuesUseJavaLongLiteral (long value, string expected)
	{
		var parser = new JavaAnnotationParser (new Dictionary<string, AssemblyIndex> (), _ => null);
		Assert.AreEqual (expected, parser.ManagedValueToJavaSource ("System.Int64", value));
	}
}
