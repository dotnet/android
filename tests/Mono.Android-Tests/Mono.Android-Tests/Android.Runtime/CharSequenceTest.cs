using System;

using Android.Runtime;

using NUnit.Framework;

namespace Android.RuntimeTests {

	[TestFixture]
	public class CharSequenceTest {

		[Test]
		public void ToLocalJniHandle ()
		{
			using (var s = new Java.Lang.String ("s")) {
				var p = CharSequence.ToLocalJniHandle (s);
				JNIEnv.DeleteLocalRef (p);
			}
		}
	}
}
