using System;
using System.Text;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class TypeMapHelperTests
{
	[TestCase ("")]
	[TestCase ("java/lang/Object")]
	[TestCase ("\u010D\u017E\U0001F600")]
	public void GetBytesPreservesUtf8Encoding (string value)
	{
		byte [] expected = Encoding.UTF8.GetBytes (value);
		Span<byte> buffer = stackalloc byte [expected.Length];
		TypeMapHelper.GetBytes (value, Encoding.UTF8, buffer);
		CollectionAssert.AreEqual (expected, buffer.ToArray ());
	}

	[Test]
	public void GetBytesRejectsShortDestination ()
	{
		byte [] buffer = new byte [1];
		Assert.Throws<ArgumentException> (() => TypeMapHelper.GetBytes ("two", Encoding.UTF8, buffer));
	}
}
