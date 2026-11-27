using System;

using Android.Runtime;
using Android.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Android.RuntimeTests {

	[TestClass]
	public class CharSequenceTest {

		[TestMethod]
		public void ToLocalJniHandle ()
		{
			using (var s = new Java.Lang.String ("s")) {
				var p = CharSequence.ToLocalJniHandle (s);
				JNIEnv.DeleteLocalRef (p);
			}
		}

		// JNI jchar is an unsigned 16-bit value; the tests below verify both Java implementations preserve all bits
		// through the generated managed char API.
		[TestMethod]
		public void JavaLangStringCharAtPreservesNonAsciiCharacters ()
		{
			const string value = "A\u00E4\u0100\u200B\u4E2D\uFFFF";

			using (var javaString = new Java.Lang.String (value))
				AssertCharAt (javaString, value);
		}

		[TestMethod]
		public void SpannableStringBuilderCharAtPreservesNonAsciiCharacters ()
		{
			const string value = "A\u00E4\u0100\u200B\u4E2D\uFFFF";

			using (var builder = new SpannableStringBuilder (value))
				AssertCharAt (builder, value);
		}

		static void AssertCharAt (Java.Lang.ICharSequence actual, string expected)
		{
			for (int i = 0; i < expected.Length; i++) {
				// The char local verifies the managed projection, while the value verifies the 16-bit JNI ABI.
				char actualChar = actual.CharAt (i);
				Assert.AreEqual (expected [i], actualChar, $"Character at index {i}: expected U+{(int) expected [i]:X4}, actual U+{(int) actualChar:X4}");
			}
		}
	}
}
