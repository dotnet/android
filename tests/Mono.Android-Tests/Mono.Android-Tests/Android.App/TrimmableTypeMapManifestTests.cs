using System;
using System.Threading;

using Android.App;
using Android.Content;

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
			var context = Application.Context;
			using var preferences = context.GetSharedPreferences ("TrimmableManifestOnlyActivity", FileCreationMode.Private);
			using (var editor = preferences.Edit ()) {
				editor.Remove ("created");
				Assert.IsTrue (editor.Commit (), "Could not clear the activity's previous result.");
			}

			using var component = new ComponentName (context.PackageName, ActivityName);
			using var intent = new Intent ();
			intent.SetComponent (component);
			intent.AddFlags (ActivityFlags.NewTask);
			context.StartActivity (intent);

			var deadline = DateTime.UtcNow.AddSeconds (10);
			while (!preferences.GetBoolean ("created", false) && DateTime.UtcNow < deadline) {
				Thread.Sleep (100);
			}

			Assert.IsTrue (preferences.GetBoolean ("created", false),
				$"Managed OnCreate did not run for manifest-only component '{ActivityName}'.");
		}
	}
}
