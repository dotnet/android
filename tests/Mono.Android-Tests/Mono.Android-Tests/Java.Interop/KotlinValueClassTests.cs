using Java.Lang;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Net.Dot.Android.Test.Kotlin;

namespace Java.InteropTests {

	// Calls the Kotlin library in tests/Mono.Android-Tests.Gradle.
	[TestClass]
	public class KotlinValueClassTests {

		[TestMethod]
		public void UnboxedParametersAndReturn ()
		{
			Assert.AreEqual (new Meters (3.5), ValueClasses.Add (new Meters (1.5), new Meters (2)));
		}

		[TestMethod]
		public void ExtensionReceiver ()
		{
			Assert.AreEqual (new Meters (4), ValueClasses.Doubled (new Meters (2)));
		}

		[TestMethod]
		public void BoxedParameter ()
		{
			Assert.AreEqual (new Meters (2), ValueClasses.OrZero (new Meters (2)));
			Assert.AreEqual (new Meters (0), ValueClasses.OrZero (null));
		}

		[TestMethod]
		public void BoxedReturn ()
		{
			Assert.AreEqual (new Id (42), ValueClasses.FindId (42));
			Assert.IsNull (ValueClasses.FindId (-1));
		}

		[TestMethod]
		public void Property ()
		{
			var original = ValueClasses.DefaultLength;
			try {
				ValueClasses.DefaultLength = new Meters (7);
				Assert.AreEqual (new Meters (7), ValueClasses.DefaultLength);
			} finally {
				ValueClasses.DefaultLength = original;
			}
		}

		[TestMethod]
		public void GenericReturn ()
		{
			var lengths = ValueClasses.Lengths (3);
			Assert.AreEqual (3, lengths.Count);
			Assert.AreEqual (new Meters (2), Meters.FromJniHandle (lengths [2].Handle, Android.Runtime.JniHandleOwnership.DoNotTransfer));
		}

		[TestMethod]
		public void CreateRunsInitBlock ()
		{
			Assert.AreEqual (new Meters (2), Meters.Create (2));
			Assert.ThrowsExactly<IllegalArgumentException> (() => Meters.Create (-1));
		}
	}
}
