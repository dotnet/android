using System;
using System.Threading;

using Android.App;
using Android.Content;

using Mono.Android_Test.Library;

using NUnit.Framework;

namespace Android.AppTests
{
	[TestFixture]
	[Category ("TrimmableTypeMapRuntimeCoverage")]
	public class TrimmableTypeMapManifestTests
	{
		// A type reference here would root the library Activity independently of the manifest.
		const string ActivityName = "net.dot.android.test.TrimmableManifestOnlyActivity";

		[Test]
		public void ManifestOnlyLibraryActivity_SurvivesTrimmingAndRunsManagedOnCreate ()
		{
			Interlocked.Exchange (ref TrimmableManifestActivityState.OnCreateCount, 0);

			var context = Application.Context;
			using var component = new ComponentName (context.PackageName, ActivityName);
			using var intent = new Intent ();
			intent.SetComponent (component);
			intent.AddFlags (ActivityFlags.NewTask);
			context.StartActivity (intent);

			var deadline = DateTime.UtcNow.AddSeconds (10);
			while (Volatile.Read (ref TrimmableManifestActivityState.OnCreateCount) == 0 && DateTime.UtcNow < deadline) {
				Thread.Sleep (100);
			}

			Assert.AreEqual (1, Volatile.Read (ref TrimmableManifestActivityState.OnCreateCount),
				$"Managed OnCreate did not run exactly once for manifest-only component '{ActivityName}'.");
		}
	}
}
