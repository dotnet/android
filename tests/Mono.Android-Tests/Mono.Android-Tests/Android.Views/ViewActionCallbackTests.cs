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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.RuntimeTests {

	[TestClass]
	public class ViewActionCallbackTests {

		[TestMethod]
		public async Task RemoveCallbacksPreservesSiblingViewPosts ()
		{
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);
			await WithActivity (content => {
				var first = new View (content.Context);
				var second = new View (content.Context);
				content.AddView (first);
				content.AddView (second);
				Assert.IsTrue (second.Post (action));
				Assert.IsTrue (first.Post (action));
				Assert.IsTrue (first.Post (action));
				Assert.IsTrue (first.RemoveCallbacks (action));
			});
			Assert.AreEqual (1, calls);
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public async Task RemoveCallbacksPreservesDrawableCallbacks (bool primeViewSchedule)
		{
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);
			await WithActivity (content => {
				var view = new View (content.Context);
				var background = new ColorDrawable ();
				view.Background = background;
				content.AddView (view);
				long now = SystemClock.UptimeMillis ();
				if (primeViewSchedule) {
					view.ScheduleDrawable (background, action, now);
					view.RemoveCallbacks (action);
				}
				background.ScheduleSelf (action, now);
				Assert.IsTrue (view.Post (action));
				Assert.IsTrue (view.RemoveCallbacks (action));
			});
			Assert.AreEqual (1, calls);
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public async Task UnscheduleCancelsViewScheduledDrawable (bool unscheduleSelf)
		{
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);
			await WithActivity (content => {
				var view = new View (content.Context);
				var background = new ColorDrawable ();
				view.Background = background;
				content.AddView (view);
				long now = SystemClock.UptimeMillis ();
				view.ScheduleDrawable (background, action, now);
				background.ScheduleSelf (action, now);
				Assert.IsTrue (view.Post (action));
				if (unscheduleSelf)
					background.UnscheduleSelf (action);
				else
					view.UnscheduleDrawable (background, action);
			});
			Assert.AreEqual (1, calls);
		}

		[TestMethod]
		public async Task RemoveCallbacksCancelsScheduledDrawable ()
		{
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);
			await WithActivity (content => {
				var view = new View (content.Context);
				var background = new ColorDrawable ();
				view.Background = background;
				var sibling = new View (content.Context);
				var siblingBackground = new ColorDrawable ();
				sibling.Background = siblingBackground;
				content.AddView (sibling);
				content.AddView (view);
				long now = SystemClock.UptimeMillis ();
				view.ScheduleDrawable (background, action, now);
				view.ScheduleDrawable (background, action, now);
				sibling.ScheduleDrawable (siblingBackground, action, now);
				Assert.IsTrue (view.RemoveCallbacks (action));
			});
			Assert.AreEqual (1, calls);
		}

		[TestMethod]
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
			Assert.AreEqual (1, callback.Pending.Count);
			second.UnscheduleSelf (action);
			Assert.AreEqual (0, callback.Pending.Count);
		}

		static async Task WithActivity (Action<ViewGroup> test)
		{
			var ready = new TaskCompletionSource<ViewActionCallbackActivity> (TaskCreationOptions.RunContinuationsAsynchronously);
			var handlerDone = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var animationDone = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			ViewActionCallbackActivity.Ready = a => ready.TrySetResult (a);
			using var intent = new Intent (Application.Context, typeof (ViewActionCallbackActivity));
			intent.AddFlags (ActivityFlags.NewTask);
			ViewActionCallbackActivity activity = null;
			Java.Lang.Runnable animationSentinel = null;
			try {
				Application.Context.StartActivity (intent);
				activity = await ready.Task.WaitAsync (TimeSpan.FromSeconds (10));
				await OnMainThread (() => {
					Assert.IsTrue (activity.Content.IsAttachedToWindow);
					test (activity.Content);
					Assert.IsTrue (activity.Content.Post (() => handlerDone.TrySetResult ()));
					animationSentinel = new Java.Lang.Runnable (() => animationDone.TrySetResult ());
					activity.Content.PostOnAnimation (animationSentinel);
				});
				await Task.WhenAll (handlerDone.Task, animationDone.Task).WaitAsync (TimeSpan.FromSeconds (30));
			} finally {
				ViewActionCallbackActivity.Ready = null;
				if (activity != null)
					await OnMainThread (() => {
						if (animationSentinel != null) {
							activity.Content.RemoveCallbacks (animationSentinel);
							animationSentinel.Dispose ();
						}
						activity.Finish ();
					});
			}
		}

		static Task OnMainThread (Action action)
		{
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
