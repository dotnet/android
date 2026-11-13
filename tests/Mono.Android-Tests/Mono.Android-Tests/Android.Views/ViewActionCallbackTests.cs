using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class ViewActionCallbackTests {

		[Test]
		public async Task RemoveCallbacksPreservesSiblingViewPosts ()
		{
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);
			await WithActivity (content => {
				var first = new View (content.Context);
				var second = new View (content.Context);
				content.AddView (first);
				content.AddView (second);
				// Attached views share the window Handler, so their runnables must differ.
				Assert.IsTrue (second.Post (action));
				Assert.IsTrue (first.Post (action));
				Assert.IsTrue (first.Post (action));
				Assert.IsTrue (first.RemoveCallbacks (action));
			});
			Assert.AreEqual (1, calls);
		}

		[TestCase (false)]
		[TestCase (true)]
		public async Task UnscheduleSelfPreservesOtherDrawablesOnView (bool attachFirst)
		{
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);
			await WithActivity (content => {
				var view = new View (content.Context);
				var foreground = new ColorDrawable ();
				var background = new ColorDrawable ();
				view.Foreground = foreground;
				view.Background = background;
				if (attachFirst)
					content.AddView (view);
				// A detached View queues runnables without the drawable, and an attached one shares
				// the Choreographer queue, so each drawable must have its own runnable.
				long now = SystemClock.UptimeMillis ();
				foreground.ScheduleSelf (action, now);
				background.ScheduleSelf (action, now);
				background.ScheduleSelf (action, now);
				background.UnscheduleSelf (action);
				if (!attachFirst)
					content.AddView (view);
			});
			Assert.AreEqual (1, calls);
		}

		[Test]
		public async Task RemoveCallbacksPreservesDrawableCallbacks ()
		{
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);
			await WithActivity (content => {
				var view = new View (content.Context);
				var background = new ColorDrawable ();
				view.Background = background;
				content.AddView (view);
				// View.removeCallbacks() removes Choreographer callbacks with any token.
				background.ScheduleSelf (action, SystemClock.UptimeMillis ());
				Assert.IsTrue (view.Post (action));
				Assert.IsTrue (view.RemoveCallbacks (action));
			});
			Assert.AreEqual (1, calls);
		}

		[Test]
		public void UnscheduleSelfPreservesNestedSiblings ()
		{
			using var first = new ColorDrawable ();
			using var second = new ColorDrawable ();
			using var parent = new LayerDrawable ([first, second]);
			using var callback = new RecordingCallback ();
			parent.Callback = callback;
			Action action = () => {};
			first.ScheduleSelf (action, 0);
			first.ScheduleSelf (action, 0);
			second.ScheduleSelf (action, 0);
			first.UnscheduleSelf (action);
			Assert.AreEqual (1, callback.Pending.Count, "Siblings forward through the same parent.");
			second.UnscheduleSelf (action);
			Assert.AreEqual (0, callback.Pending.Count);
		}

		static async Task WithActivity (Action<ViewGroup> test)
		{
			var ready = new TaskCompletionSource<ViewActionCallbackActivity> (TaskCreationOptions.RunContinuationsAsynchronously);
			ViewActionCallbackActivity.Ready = a => ready.TrySetResult (a);
			using var intent = new Intent (Application.Context, typeof (ViewActionCallbackActivity));
			intent.AddFlags (ActivityFlags.NewTask);
			ViewActionCallbackActivity activity = null;
			try {
				Application.Context.StartActivity (intent);
				activity = await ready.Task.WaitAsync (TimeSpan.FromSeconds (10));
				await OnMainThread (() => test (activity.Content));
				// Let posted runnables and the next animation frames run.
				await Task.Delay (500);
				await OnMainThread (() => {});
			} finally {
				ViewActionCallbackActivity.Ready = null;
				if (activity != null)
					await OnMainThread (activity.Finish);
			}
		}

		static Task OnMainThread (Action action)
		{
			// The UI thread does not inherit NUnit's AsyncLocal test context.
			var context = ExecutionContext.Capture ();
			var done = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			using var handler = new Handler (Looper.MainLooper);
			Assert.IsTrue (handler.Post (() => {
				try {
					if (context == null)
						action ();
					else
						ExecutionContext.Run (context, _ => action (), null);
					done.TrySetResult ();
				} catch (Exception e) {
					done.TrySetException (e);
				}
			}));
			return done.Task.WaitAsync (TimeSpan.FromSeconds (10));
		}

		sealed class RecordingCallback : Java.Lang.Object, Drawable.ICallback {
			public List<Java.Lang.IRunnable> Pending { get; } = new ();

			public void InvalidateDrawable (Drawable who) {}

			public void ScheduleDrawable (Drawable who, Java.Lang.IRunnable what, long when) => Pending.Add (what);

			public void UnscheduleDrawable (Drawable who, Java.Lang.IRunnable what) =>
				Pending.RemoveAll (r => global::Android.Runtime.JNIEnv.IsSameObject (r.Handle, what.Handle));
		}
	}

	[Activity]
	public class ViewActionCallbackActivity : Activity {
		public static Action<ViewActionCallbackActivity> Ready { get; set; }

		public ViewGroup Content { get; private set; }

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			var content = new FrameLayout (this);
			Content = content;
			SetContentView (content);
			content.Post (() => Ready?.Invoke (this));
		}
	}
}
