using System;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;

namespace Mono.Android_Test.Library
{
	// Only the app manifest entry should root this type; a component attribute would mask a rooting failure.
	[Register ("net/dot/android/test/TrimmableManifestOnlyActivity")]
	public class TrimmableManifestOnlyActivity : Activity
	{
		public TrimmableManifestOnlyActivity ()
		{
		}

		public TrimmableManifestOnlyActivity (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			var preferences = GetSharedPreferences ("TrimmableManifestOnlyActivity", FileCreationMode.Private);
			using var editor = preferences.Edit ();
			editor.PutBoolean ("created", true);
			editor.Apply ();
			Finish ();
		}
	}
}
