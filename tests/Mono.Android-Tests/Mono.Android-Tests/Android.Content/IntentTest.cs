using System;

using Android.Content;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Android.ContentTests
{
	[TestClass]
	public class IntentTest
	{
		[TestMethod]
		public void PutCharSequenceArrayListExtra_NullValue ()
		{
			using (var intent = new Intent ()) {
				intent.PutCharSequenceArrayListExtra ("null", null);
				Assert.AreEqual (null, intent.GetCharSequenceArrayListExtra ("null"));
			}
		}
	}
}
