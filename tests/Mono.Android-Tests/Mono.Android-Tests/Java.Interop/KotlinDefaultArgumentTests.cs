using Microsoft.VisualStudio.TestTools.UnitTesting;

using Net.Dot.Android.Test.Kotlin;

namespace Java.InteropTests {

	// Calls the Kotlin default-argument overloads of the library in tests/Mono.Android-Tests.Gradle.
	[TestClass]
	public class KotlinDefaultArgumentTests {

		[TestMethod]
		public void Method ()
		{
			// fun scale(value: Int, factor: Int = 2, offset: Int = factor + 1)
			Assert.AreEqual (3 * 2 + 3, DefaultArguments.Scale (3));
			Assert.AreEqual (3 * 5 + 6, DefaultArguments.Scale (3, 5));
			Assert.AreEqual (3 * 5 + 1, DefaultArguments.Scale (3, 5, 1));
		}

		[TestMethod]
		public void Constructor ()
		{
			// class Counter(val start: Int, val step: Int = start * 2)
			Assert.AreEqual (8, new Counter (4).Step);
			Assert.AreEqual (1, new Counter (4, 1).Step);
		}

		[TestMethod]
		public void VirtualDispatch ()
		{
			// Calculator.compute(a, b = 10) is overridden by Multiplier.compute(a, b) = a * b.
			Assert.AreEqual (3 + 10, new Calculator ().Compute (3));
			Assert.AreEqual (3 * 10, new Multiplier ().Compute (3));
		}
	}
}
