using System;

using NUnit.Framework;

using Android.OS;

namespace Android.OSTests;

[TestFixture]
public class AsyncTaskTests {
	[Test]
	public void GenericAsyncTaskUsesAsyncTaskClass ()
	{
		using var task = new StringAsyncTask ();

		Assert.AreNotEqual (IntPtr.Zero, task.GetThresholdClass ());
	}

	class StringAsyncTask : AsyncTask<string, string, string> {
		public IntPtr GetThresholdClass ()
		{
			return ThresholdClass;
		}

		protected override string RunInBackground (params string [] @params)
		{
			return "";
		}
	}
}
