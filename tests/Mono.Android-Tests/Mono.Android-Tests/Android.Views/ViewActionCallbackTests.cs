using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;

using Java.Interop;
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
				// Attached views share the window Handler, so their runnables must differ.
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
					// An earlier, already-cancelled View.ScheduleDrawable must not widen later cancellations.
					view.ScheduleDrawable (background, action, now);
					view.RemoveCallbacks (action);
				}
				// View.removeCallbacks() removes Choreographer callbacks with any token.
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
				Assert.IsTrue (view.Post (action));                      // runs: not a drawable callback
				if (unscheduleSelf)
					background.UnscheduleSelf (action);
				else
					view.UnscheduleDrawable (background, action);
			});
			Assert.AreEqual (1, calls);
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public async Task RemoveCallbacksCancelsScheduledDrawable (bool attachFirst)
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
				if (attachFirst)
					content.AddView (view);
				long now = SystemClock.UptimeMillis ();
				view.ScheduleDrawable (background, action, now);
				view.ScheduleDrawable (background, action, now);
				sibling.ScheduleDrawable (siblingBackground, action, now);   // runs: different View
				Assert.IsTrue (view.RemoveCallbacks (action));
				if (!attachFirst)
					content.AddView (view);
			});
			Assert.AreEqual (1, calls);
		}

		static int scheduledCalls;
		static readonly Action rootedScheduledAction = () => Interlocked.Increment (ref scheduledCalls);

		[TestMethod]
		public async Task ScheduledDrawableRunnablesDoNotAccumulateOnView ()
		{
			scheduledCalls = 0;
			CapturingView view = null;
			await OnMainThread (() => {
				view = new CapturingView (Application.Context) { Background = new ColorDrawable () };
				long now = SystemClock.UptimeMillis ();
				// Detached: these stay in the View's run queue until it is attached.
				view.ScheduleDrawable (view.Background, rootedScheduledAction, now);
				view.ScheduleDrawable (view.Background, rootedScheduledAction, now);
			});

			// A long-lived View and a rooted Action must not keep runnables of transient drawables alive.
			var runnables = new List<WeakReference> ();
			for (int round = 0; round < 40 && (runnables.Count == 0 || runnables.Exists (r => r.IsAlive)); round++) {
				await OnMainThread (() => ScheduleTransientDrawables (view, 100, round == 0 ? runnables : null));
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				Java.Lang.Runtime.GetRuntime ().Gc ();
				Java.Interop.JniEnvironment.Runtime.ValueManager.CollectPeers ();
				await Task.Delay (50);
			}
			Assert.AreEqual (100, runnables.Count);
			Assert.IsFalse (runnables.Exists (r => r.IsAlive), "A View must not retain runnables of collected drawables.");

			await WithActivity (content => {
				Assert.IsTrue (view.RemoveCallbacks (rootedScheduledAction));
				content.AddView (view);
			});
			Assert.AreEqual (0, scheduledCalls, "The surviving drawable's callbacks must stay cancelable.");
		}

		[TestMethod]
		public async Task RemoveCallbacksFindsScheduledDrawableAfterDrawableCollectionAndOwnerSweep ()
		{
			scheduledCalls = 0;
			int controlCalls = 0;
			CapturingView view = null;
			WeakReference[] references = null;
			object drawableOwner = null;
			await OnMainThread (() => {
				view = new CapturingView (Application.Context);
				references = ScheduleAndDetachDrawable (view, out drawableOwner);
				Assert.IsTrue (view.Post (() => Interlocked.Increment (ref controlCalls)));
			});

			bool swept = false;
			for (int round = 0; round < 50 && !swept; round++) {
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				Java.Lang.Runtime.GetRuntime ().Gc ();
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				swept = SweepDrawableOwner (drawableOwner);
				await Task.Delay (50);
			}
			Assert.IsTrue (swept, "The collected drawable owner and its JNI weak reference should be swept.");
			Assert.IsFalse (references [0].IsAlive, "The detached drawable should be collectible.");
			Assert.IsTrue (references [1].IsAlive, "The View's run queue should keep the scheduled runnable alive.");

			var ownerReference = new WeakReference (drawableOwner);
			drawableOwner = null;
			for (int round = 0; round < 50 && ownerReference.IsAlive; round++) {
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				await Task.Delay (50);
			}
			Assert.IsFalse (ownerReference.IsAlive, "The swept drawable owner must no longer be rooted by the registry.");

			await OnMainThread (() => Assert.IsTrue (view.RemoveCallbacks (rootedScheduledAction)));
			await WithActivity (content => content.AddView (view));
			Assert.AreEqual (1, controlCalls, "The detached View's queued callbacks must run after attachment.");
			Assert.AreEqual (0, scheduledCalls, "RemoveCallbacks must cancel detached drawable work after its drawable owner is swept.");
			GC.KeepAlive (references);
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		[DynamicDependency ("FindOwner", typeof (Java.Lang.Thread.RunnableImplementor))]
		static WeakReference[] ScheduleAndDetachDrawable (CapturingView view, out object drawableOwner)
		{
			var drawable = new ColorDrawable ();
			view.ScheduleDrawable (drawable, rootedScheduledAction, 0);
			var findOwner = typeof (Java.Lang.Thread.RunnableImplementor).GetMethod ("FindOwner", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull (findOwner);
			drawableOwner = findOwner.Invoke (null, [drawable]);
			Assert.IsNotNull (drawableOwner);
			var references = new WeakReference [] {
				new WeakReference (drawable),
				new WeakReference (view.Captured, trackResurrection: true),
			};
			view.Captured = null;
			drawable.Dispose ();
			return references;
		}

		[DynamicDependency ("ownersLock", typeof (Java.Lang.Thread.RunnableImplementor))]
		[DynamicDependency ("RemoveCollectedOwners", typeof (Java.Lang.Thread.RunnableImplementor))]
		[DynamicDependency (DynamicallyAccessedMemberTypes.NonPublicNestedTypes, typeof (Java.Lang.Thread.RunnableImplementor))]
		static bool SweepDrawableOwner (object drawableOwner)
		{
			var type = typeof (Java.Lang.Thread.RunnableImplementor);
			var ownerType = type.GetNestedType ("Owner", BindingFlags.NonPublic);
			Assert.IsNotNull (ownerType);
			var referenceField = ownerType.GetField ("Reference", BindingFlags.Instance | BindingFlags.Public);
			var lockField = type.GetField ("ownersLock", BindingFlags.Static | BindingFlags.NonPublic);
			var sweep = type.GetMethod ("RemoveCollectedOwners", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull (referenceField);
			Assert.IsNotNull (lockField);
			Assert.IsNotNull (sweep);
			var sync = lockField.GetValue (null) as Lock ?? throw new InvalidOperationException ("The owner registry lock is missing.");
			// Run the registry's sweep under its normal lock so the test does not depend on its threshold.
			lock (sync) {
				var reference = (JniObjectReference) referenceField.GetValue (drawableOwner);
				if (reference.IsValid && !JniEnvironment.Types.IsSameObject (reference, default))
					return false;
				sweep.Invoke (null, null);
				reference = (JniObjectReference) referenceField.GetValue (drawableOwner);
				Assert.IsFalse (reference.IsValid, "Sweeping must dispose the drawable owner's JNI weak reference.");
				return true;
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static void ScheduleTransientDrawables (CapturingView view, int count, List<WeakReference> runnables)
		{
			for (int i = 0; i < count; i++) {
				using var drawable = new ColorDrawable ();
				view.ScheduleDrawable (drawable, rootedScheduledAction, 0);
				view.UnscheduleDrawable (drawable, rootedScheduledAction);
				runnables?.Add (new WeakReference (view.Captured));
				view.Captured = null;
			}
		}

		sealed class CapturingView : View {
			public Java.Lang.IRunnable Captured;

			public CapturingView (Context context) : base (context) {}

			protected override bool VerifyDrawable (Drawable who) => true;

			public override void ScheduleDrawable (Drawable who, Java.Lang.IRunnable what, long when)
			{
				Captured = what;
				base.ScheduleDrawable (who, what, when);
			}
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
			Assert.AreEqual (1, callback.Pending.Count, "Siblings forward through the same parent.");
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
					// Detached View queues are transferred to the Handler when added; attached
					// drawable callbacks use the animation queue. Wait for both queues.
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
			// The UI thread does not inherit the test's execution context.
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
