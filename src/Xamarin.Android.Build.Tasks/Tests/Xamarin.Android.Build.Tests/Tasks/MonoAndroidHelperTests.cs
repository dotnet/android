using NUnit.Framework;
using System;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class MonoAndroidHelperTests : BaseTest
	{
		[Test]
		public void TestStringEquals_DefaultComparison ()
		{
			// Test default Ordinal comparison
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("Hello", "Hello"));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("Hello", "hello"));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("WORLD", "world"));
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("", ""));
		}

		[Test]
		public void TestStringEquals_OrdinalComparison ()
		{
			// Test explicit Ordinal comparison
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("Hello", "Hello", StringComparison.Ordinal));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("Hello", "hello", StringComparison.Ordinal));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("WORLD", "world", StringComparison.Ordinal));
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("", "", StringComparison.Ordinal));
		}

		[Test]
		public void TestStringEquals_OrdinalIgnoreCaseComparison ()
		{
			// Test explicit OrdinalIgnoreCase comparison
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("Hello", "hello", StringComparison.OrdinalIgnoreCase));
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("WORLD", "world", StringComparison.OrdinalIgnoreCase));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("Hello", "World", StringComparison.OrdinalIgnoreCase));
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("", "", StringComparison.OrdinalIgnoreCase));
		}

		[Test]
		public void TestStringEquals_NullHandling ()
		{
			// Test null handling
			Assert.IsTrue (MonoAndroidHelper.StringEquals (null, null));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("test", null));
			Assert.IsFalse (MonoAndroidHelper.StringEquals (null, "test"));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("", null));
			Assert.IsFalse (MonoAndroidHelper.StringEquals (null, ""));
		}

		[Test]
		public void TestStringEquals_ReplaceStringCompareUseCases ()
		{
			// Test cases that replicate the original String.Compare use cases

			// Replaces: String.Compare("typemap", Mode, StringComparison.OrdinalIgnoreCase) == 0
			string mode = "TYPEMAP";
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("typemap", mode, StringComparison.OrdinalIgnoreCase));

			// Replaces: String.Compare("environment", Mode, StringComparison.OrdinalIgnoreCase) == 0  
			mode = "Environment";
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("environment", mode, StringComparison.OrdinalIgnoreCase));

			// Replaces: String.Compare(abi, item.GetMetadata(), StringComparison.Ordinal) != 0
			string abi = "arm64-v8a";
			string metadata = "x86_64";
			Assert.IsFalse (MonoAndroidHelper.StringEquals (abi, metadata, StringComparison.Ordinal));

			// Replaces: String.Compare("merge", current.LocalName, StringComparison.Ordinal) == 0
			string localName = "merge";
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("merge", localName, StringComparison.Ordinal));
		}
	}
}