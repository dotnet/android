using System;
using System.Collections;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests
{
	static partial class EnvironmentHelperAssertions
	{
		internal static partial void AreEqual (object expected, object actual, string message) => Assert.AreEqual (expected, actual, message);
		internal static partial void Greater (IComparable actual, IComparable expected) => Assert.Greater (actual, expected);
		internal static partial void GreaterOrEqual (IComparable actual, IComparable expected) => Assert.GreaterOrEqual (actual, expected);
		internal static partial void Less (IComparable actual, IComparable expected) => Assert.Less (actual, expected);
		internal static partial void IsNotEmpty (IEnumerable actual, string message) => Assert.IsNotEmpty (actual, message);
		internal static partial void IsNotEmpty (string actual, string message) => Assert.IsNotEmpty (actual, message);
	}
}
