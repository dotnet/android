using System;
using System.Threading.Tasks;

using NUnit.Framework;

using Android.App;
using Android.OS;
using Android.Runtime;

#pragma warning disable CA1422 // This fixture intentionally exercises the platform-obsolete CursorLoader binding.

namespace Android.ContentTests {

	[TestFixture]
	public class CursorLoaderTest {

		[Test]
		[Category ("ThresholdDispatch")]
		public async Task LoadInBackgroundDispatch ()
		{
			var completion = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var looper = Looper.MainLooper ?? throw new InvalidOperationException ("The Android main looper is unavailable.");
			using (var handler = new Handler (looper)) {
				if (!handler.Post (() => {
					try {
						using (var loader = CreateCursorLoader ()) {
							using (var cursor = loader.LoadInBackground ()) {
								Assert.IsNotNull (cursor);
							}
						}
						using (var loader = new ManagedCursorLoader ()) {
							using (var cursor = loader.CallBaseLoadInBackground ()) {
								Assert.IsNotNull (cursor);
							}
							Assert.IsFalse (loader.OverrideInvoked);
						}
						completion.SetResult (true);
					} catch (Exception e) {
						completion.SetException (e);
					}
				})) {
					Assert.Fail ("Could not post the CursorLoader test to the Android main looper.");
				}
				await completion.Task;
			}
		}

		static Android.Content.CursorLoader CreateCursorLoader ()
		{
			return new Android.Content.CursorLoader (
					Application.Context,
					Android.Provider.Settings.System.GetUriFor (Android.Provider.Settings.System.ScreenBrightness),
					null, null, null, null);
		}

	}

	public class ManagedCursorLoader : Android.Content.CursorLoader {

		public ManagedCursorLoader ()
			: base (Application.Context,
					Android.Provider.Settings.System.GetUriFor (Android.Provider.Settings.System.ScreenBrightness),
					null, null, null, null)
		{
		}

		protected ManagedCursorLoader (IntPtr javaReference, JniHandleOwnership transfer)
			: base (javaReference, transfer)
		{
		}

		public bool OverrideInvoked { get; private set; }

		public override Java.Lang.Object? LoadInBackground ()
		{
			OverrideInvoked = true;
			return null;
		}

		public Java.Lang.Object? CallBaseLoadInBackground ()
		{
			return base.LoadInBackground ();
		}
	}

	#pragma warning restore CA1422
}
