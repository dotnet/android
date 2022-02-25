using System;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

using NUnit.Framework;

namespace Android.AppTests
{
	[TestFixture]
	public class ApplicationTest
	{
		[Test]
		public void ApplicationContextIsApp ()
		{
			Assert.IsTrue (Application.Context is App);
			Assert.IsTrue (App.Created);
		}

		[Test]
		public void SynchronizationContext_Is_ThreadingSynchronizationContextCurrent ()
		{
			bool same = false;
			Application.SynchronizationContext.Send (_ => {
					var c = System.Threading.SynchronizationContext.Current;
					same = object.ReferenceEquals (c, Application.SynchronizationContext);
			}, null);
			Assert.IsTrue (same);
		}

		[Test]
		public void SynchronizationContext_Post_DoesNotBlock ()
		{
			// To ensure we're on the main thread:
			bool sendFinishedBeforePost = false;
			Application.SynchronizationContext.Send (_ => {
					bool postWasExecuted  = false;
					Application.SynchronizationContext.Post (_2 => {
							postWasExecuted = true;
					}, null);
					if (!postWasExecuted)
						sendFinishedBeforePost = true;
			}, null);
			Assert.IsTrue (sendFinishedBeforePost);
		}

		[Test]
		public void EnsureAndroidManifestIsUpdated ()
		{
			var klass	  = Java.Lang.Class.FromType (typeof (RenamedActivity));
			var context = Application.Context;
			using (var n = new ComponentName (context, klass)) {
				var activityInfo  = context.PackageManager.GetActivityInfo (n, 0);
				var configChanges = activityInfo.ConfigChanges;
				Assert.AreEqual (ConfigChanges.KeyboardHidden, configChanges & ConfigChanges.KeyboardHidden);
			}
		}
	}

#if ANDROID_30
	[Application (Debuggable=true)]
#endif
	public class App : Application {

		public static bool            Created;

		public App (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
			Created = true;
		}

		public override void OnCreate ()
		{
			base.OnCreate ();
		}
	}

	[Activity]
	public class RenamedActivity : Activity {

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			Finish ();
		}
	}
}
