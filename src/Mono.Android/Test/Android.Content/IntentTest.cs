using System;

using Android.Content;

using NUnit.Framework;

namespace Android.ContentTests
{
	[TestFixture]
	public class IntentTest
	{
		[Test]
		public void PutCharSequenceArrayListExtra_NullValue ()
		{
			using (var intent = new Intent ()) {
				intent.PutCharSequenceArrayListExtra ("null", null);
				Assert.AreEqual (null, intent.GetCharSequenceArrayListExtra ("null"));
			}
		}
	}
}
