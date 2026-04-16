using Java.Interop;
using Microsoft.Android.Runtime;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class TrimmableTypeMapTypeManagerTests
	{
		// Test subclass that allows instantiation without full TrimmableTypeMap initialization.
		// GetStaticMethodFallbackTypesCore does not use TrimmableTypeMap.Instance, so the test
		// can run without an initialized TrimmableTypeMap singleton.
		sealed class TestableTrimmableTypeMapTypeManager : TrimmableTypeMapTypeManager
		{
		}

		[Test]
		public void GetStaticMethodFallbackTypes_WithPackageName_ReturnsDesugarFallbacks ()
		{
			using var manager = new TestableTrimmableTypeMapTypeManager ();
			var fallbacks = manager.GetStaticMethodFallbackTypes ("android/app/Activity");
			Assert.IsNotNull (fallbacks);
			Assert.AreEqual (2, fallbacks!.Count);
			Assert.AreEqual ("android/app/DesugarActivity$_CC", fallbacks [0]);
			Assert.AreEqual ("android/app/Activity$-CC", fallbacks [1]);
		}

		[Test]
		public void GetStaticMethodFallbackTypes_WithoutPackageName_ReturnsDesugarFallbacks ()
		{
			using var manager = new TestableTrimmableTypeMapTypeManager ();
			var fallbacks = manager.GetStaticMethodFallbackTypes ("Activity");
			Assert.IsNotNull (fallbacks);
			Assert.AreEqual (2, fallbacks!.Count);
			Assert.AreEqual ("DesugarActivity$_CC", fallbacks [0]);
			Assert.AreEqual ("Activity$-CC", fallbacks [1]);
		}

		[Test]
		public void GetStaticMethodFallbackTypes_WithDeepPackageName_ReturnsDesugarFallbacks ()
		{
			using var manager = new TestableTrimmableTypeMapTypeManager ();
			var fallbacks = manager.GetStaticMethodFallbackTypes ("com/example/package/MyInterface");
			Assert.IsNotNull (fallbacks);
			Assert.AreEqual (2, fallbacks!.Count);
			Assert.AreEqual ("com/example/package/DesugarMyInterface$_CC", fallbacks [0]);
			Assert.AreEqual ("com/example/package/MyInterface$-CC", fallbacks [1]);
		}
	}
}
