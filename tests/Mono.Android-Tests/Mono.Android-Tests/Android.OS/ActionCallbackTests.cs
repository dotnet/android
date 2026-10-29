using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Java.Interop;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	[Category ("ActionCallbacks")]
	public class ActionCallbackTests {
		[OneTimeSetUp]
		public void LogFrameworkIdentity ()
		{
			var version = typeof (Handler).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute> ();
			global::Android.Util.Log.Info ("ActionCallbacks", $"Mono.Android={version?.InformationalVersion}; runtime={RuntimeInformation.FrameworkDescription}");
		}

		[TestCase ("Post")]
		[TestCase ("PostDelayed")]
		[TestCase ("PostAtTime")]
		[TestCase ("PostAtFrontOfQueue")]
		public async Task RepeatedPostsAreCanceled (string method)
		{
			int calls = 0;
			await OnHandler (handler => {
				Action action = () => calls++;
				for (int i = 0; i < 50; i++) {
					for (int post = 0; post < 2; post++) {
						bool posted = method switch {
							"Post" => handler.Post (action),
							"PostDelayed" => handler.PostDelayed (action, 0),
							"PostAtTime" => handler.PostAtTime (action, SystemClock.UptimeMillis ()),
							"PostAtFrontOfQueue" => handler.PostAtFrontOfQueue (action),
							_ => throw new ArgumentException (nameof (method)),
						};
						Assert.IsTrue (posted);
					}
					handler.RemoveCallbacks (action);
				}
			});
			Assert.AreEqual (0, calls);
		}

		[TestCase (false)]
		[TestCase (true)]
		public async Task CancellationIsScopedToHandler (bool cancelBoth)
		{
			int calls = 0;
			await OnHandler (handler => {
				using var other = new Handler (handler.Looper);
				Action action = () => calls++;
				Assert.IsTrue (handler.Post (action));
				Assert.IsTrue (handler.Post (action));
				Assert.IsTrue (other.Post (action));
				Assert.IsTrue (other.Post (action));
				handler.RemoveCallbacks (action);
				if (cancelBoth)
					other.RemoveCallbacks (action);
			});
			Assert.AreEqual (cancelBoth ? 0 : 2, calls);
		}

		[TestCase (false, false, 3)]
		[TestCase (true, false, 0)]
		[TestCase (false, true, 0)]
		public async Task CancellationPreservesOtherTokens (bool cancelRemaining, bool nullToken, int expected)
		{
			int calls = 0;
			using var first = new Java.Lang.String ("first");
			using var second = new Java.Lang.String ("second");
			await OnHandler (handler => {
				Action action = () => calls++;
				long now = SystemClock.UptimeMillis ();
				Assert.IsTrue (handler.PostAtTime (action, first, now));
				Assert.IsTrue (handler.PostAtTime (action, first, now));
				Assert.IsTrue (handler.PostAtTime (action, second, now));
				Assert.IsTrue (handler.PostAtTime (action, second, now));
				Assert.IsTrue (handler.Post (action));
				handler.RemoveCallbacks (action, nullToken ? null : first);
				if (cancelRemaining)
					handler.RemoveCallbacks (action);
			});
			Assert.AreEqual (expected, calls);
		}

		[Test]
		public async Task ExecutionDoesNotForgetOtherPosts ()
		{
			int calls = 0;
			await OnHandler (handler => {
				Action action = () => {};
				action = () => {
					calls++;
					Assert.IsTrue (handler.PostAtFrontOfQueue (() => handler.RemoveCallbacks (action)));
				};
				Assert.IsTrue (handler.Post (action));
				Assert.IsTrue (handler.Post (action));
			});
			Assert.AreEqual (1, calls);
		}

		[Test]
		public void UnattachedViewsHaveIndependentQueues ()
		{
			using var first = new View (Application.Context);
			using var second = new View (Application.Context);
			Action action = () => {};
			Assert.IsTrue (first.Post (action));
			Assert.IsTrue (first.PostDelayed (action, 1000));
			Assert.IsTrue (second.Post (action));
			Assert.IsTrue (first.RemoveCallbacks (action));
			Assert.IsTrue (second.RemoveCallbacks (action), "Canceling one View must not forget the other View.");
		}

		[Test]
		public void DrawableCancellationDoesNotDisposeUnschedulingNoOps ()
		{
			using var drawable = new ColorDrawable ();
			using var callback = new DrawableCallback { IgnoreUnschedule = true };
			drawable.Callback = callback;
			int calls = 0;
			Action action = () => calls++;
			drawable.ScheduleSelf (action, 0);
			drawable.ScheduleSelf (action, 0);
			drawable.UnscheduleSelf (action);
			Assert.AreEqual (2, callback.Runnables.Count);
			foreach (var runnable in callback.Runnables.ToArray ())
				runnable.Run ();
			Assert.AreEqual (2, calls, "A callback that declines to unschedule still owns live runnables.");
		}

		[Test]
		public void DrawableCancellationIsScopedToOwner ()
		{
			using var first = new ColorDrawable ();
			using var second = new ColorDrawable ();
			using var callback = new DrawableCallback ();
			first.Callback = callback;
			second.Callback = callback;
			Action action = () => {};
			first.ScheduleSelf (action, 0);
			first.ScheduleSelf (action, 0);
			second.ScheduleSelf (action, 0);
			first.UnscheduleSelf (action);
			Assert.AreEqual (1, callback.Runnables.Count);
			second.UnscheduleSelf (action);
			Assert.AreEqual (0, callback.Runnables.Count);
		}

		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void NestedDrawableCancellationPreservesSiblings (bool scheduleThroughParent, bool cancelThroughAlias)
		{
			using var first = new ColorDrawable ();
			using var second = new ColorDrawable ();
			using var parent = new LayerDrawable ([first, second]);
			using var alias = new ColorDrawableAlias (first.Handle);
			using var callback = new DrawableCallback ();
			parent.Callback = callback;
			Action action = () => {};
			if (scheduleThroughParent) {
				parent.ScheduleDrawable (first, action, 0);
				parent.ScheduleDrawable (first, action, 0);
				parent.ScheduleDrawable (second, action, 0);
			} else {
				first.ScheduleSelf (action, 0);
				first.ScheduleSelf (action, 0);
				second.ScheduleSelf (action, 0);
			}
			Assert.AreEqual (3, callback.Runnables.Count);
			var canceledChild = cancelThroughAlias ? alias : first;
			if (scheduleThroughParent)
				parent.UnscheduleDrawable (canceledChild, action);
			else
				canceledChild.UnscheduleSelf (action);
			Assert.AreEqual (1, callback.Runnables.Count, "Forwarding under a shared parent must not cancel a sibling's callback.");
			if (scheduleThroughParent)
				parent.UnscheduleDrawable (second, action);
			else
				second.UnscheduleSelf (action);
			Assert.AreEqual (0, callback.Runnables.Count);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void CallbackBookkeepingDoesNotScaleQuadratically (bool measureExecution)
		{
			MeasureCallbackBatch (256, measureExecution);
			var small = new double [3];
			var large = new double [3];
			for (int i = 0; i < small.Length; i++) {
				small [i] = MeasureCallbackBatch (4_000, measureExecution);
				large [i] = MeasureCallbackBatch (16_000, measureExecution);
			}
			Array.Sort (small);
			Array.Sort (large);
			double smallMedian = small [1];
			double largeMedian = large [1];
			string operation = measureExecution ? "Execution cleanup" : "Posting";
			global::Android.Util.Log.Info ("ActionCallbacks", $"{operation}: 4000={smallMedian:F2}ms; 16000={largeMedian:F2}ms");
			// Four times the callbacks should not approach quadratic (16x) cost.
			// Medians and an additive allowance tolerate timer noise and scheduling pauses.
			Assert.Less (largeMedian, smallMedian * 10 + 100, $"{operation} must not scan every registration for each callback.");
		}

		static double MeasureCallbackBatch (int count, bool measureExecution)
		{
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			using var drawable = new ColorDrawable ();
			using var callback = new CallbackBatch ();
			drawable.Callback = callback;
			callback.Runnables.Capacity = count;
			int calls = 0;
			Action action = () => calls++;
			var watch = Stopwatch.StartNew ();
			for (int i = 0; i < count; i++)
				drawable.ScheduleSelf (action, 0);
			double posting = watch.Elapsed.TotalMilliseconds;
			watch.Restart ();
			foreach (var runnable in callback.Runnables)
				runnable.Run ();
			double execution = watch.Elapsed.TotalMilliseconds;
			Assert.AreEqual (count, calls);
			Assert.IsTrue (callback.Runnables.TrueForAll (runnable => runnable.Handle == IntPtr.Zero), "Execution must dispose every callback.");
			return measureExecution ? execution : posting;
		}

		[Test]
		public void ThrowingCallbackIsDisposedWithoutForgettingOtherPosts ()
		{
			using var drawable = new ColorDrawable ();
			using var callback = new DrawableCallback ();
			drawable.Callback = callback;
			Action action = () => throw new InvalidOperationException ("Expected callback failure.");
			drawable.ScheduleSelf (action, 0);
			drawable.ScheduleSelf (action, 0);
			var runnable = callback.Runnables [0];
			callback.Runnables.RemoveAt (0);
			Assert.Throws<InvalidOperationException> (() => runnable.Run ());
			Assert.AreEqual (IntPtr.Zero, runnable.Handle);
			drawable.UnscheduleSelf (action);
			Assert.AreEqual (0, callback.Runnables.Count);
		}

		[Test]
		public void ViewDrawableCancellationPreservesOtherDrawables ()
		{
			using var view = new SchedulingView ();
			using var first = new ColorDrawable ();
			using var second = new ColorDrawable ();
			Action action = () => {};
			view.ScheduleDrawable (first, action, 0);
			view.ScheduleDrawable (first, action, 0);
			view.ScheduleDrawable (second, action, 0);
			view.UnscheduleDrawable (first, action);
			Assert.AreEqual (1, view.Callback.Runnables.Count);
			view.UnscheduleDrawable (second, action);
			Assert.AreEqual (0, view.Callback.Runnables.Count);
		}

		[TestCase ("Clip")]
		[TestCase ("Scale")]
		[TestCase ("Inset")]
		[TestCase ("Rotate")]
		[TestCase ("Layer")]
		[TestCase ("Container")]
		public void DrawableForwardersCancelRepeatedPosts (string kind)
		{
			using var child = new ColorDrawable ();
			using Drawable owner = kind switch {
				"Clip" => new ClipDrawable (child, GravityFlags.Left, ClipDrawableOrientation.Horizontal),
				"Scale" => new ScaleDrawable (child, GravityFlags.Left, 1, 1),
				"Inset" => new InsetDrawable (child, 0),
				"Rotate" => new RotateDrawable { Drawable = child },
				"Layer" => new LayerDrawable ([child]),
				"Container" => new StateListDrawable (),
				_ => throw new ArgumentException (nameof (kind)),
			};
			if (owner is StateListDrawable stateList) {
				stateList.AddState ([], child);
				stateList.SelectDrawable (0);
			}
			using var callback = new DrawableCallback ();
			owner.Callback = callback;
			Action action = () => {};
			switch (owner) {
				case ClipDrawable drawable:
					drawable.ScheduleDrawable (child, action, 0);
					drawable.ScheduleDrawable (child, action, 0);
					drawable.UnscheduleDrawable (child, action);
					break;
				case ScaleDrawable drawable:
					drawable.ScheduleDrawable (child, action, 0);
					drawable.ScheduleDrawable (child, action, 0);
					drawable.UnscheduleDrawable (child, action);
					break;
				case InsetDrawable drawable:
					drawable.ScheduleDrawable (child, action, 0);
					drawable.ScheduleDrawable (child, action, 0);
					drawable.UnscheduleDrawable (child, action);
					break;
				case RotateDrawable drawable:
					drawable.ScheduleDrawable (child, action, 0);
					drawable.ScheduleDrawable (child, action, 0);
					drawable.UnscheduleDrawable (child, action);
					break;
				case LayerDrawable drawable:
					drawable.ScheduleDrawable (child, action, 0);
					drawable.ScheduleDrawable (child, action, 0);
					drawable.UnscheduleDrawable (child, action);
					break;
				case DrawableContainer drawable:
					drawable.ScheduleDrawable (child, action, 0);
					drawable.ScheduleDrawable (child, action, 0);
					drawable.UnscheduleDrawable (child, action);
					break;
			}
			Assert.AreEqual (2, callback.ScheduledCount, "Both posts must reach the native callback.");
			Assert.AreEqual (0, callback.Runnables.Count);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RemovedQueueEntriesAreCollectibleWithLiveAction (bool clearQueue)
		{
			using var thread = new HandlerThread ("ActionCallbackCollection");
			thread.Start ();
			try {
				using var handler = new CaptureHandler (thread.Looper);
				Action action = () => {};
				var references = PostAndRemove (handler, action, clearQueue);
				for (int i = 0; i < 50 && references.Exists (r => r.IsAlive); i++) {
					GC.Collect ();
					GC.WaitForPendingFinalizers ();
					Java.Lang.Runtime.GetRuntime ().Gc ();
					JniEnvironment.Runtime.ValueManager.CollectPeers ();
					Thread.Sleep (100);
				}
				Assert.IsFalse (references.Exists (r => r.IsAlive), "Removed wrappers must not be rooted by the Action lookup.");
				GC.KeepAlive (action);
			} finally {
				thread.Quit ();
				thread.Join ();
			}
		}

		[Test]
		public void FailedPostDisposesItsWrapper ()
		{
			using var thread = new HandlerThread ("RejectedActionCallback");
			thread.Start ();
			using var handler = new CaptureHandler (thread.Looper);
			thread.Quit ();
			thread.Join ();
			Action action = () => Assert.Fail ("A rejected callback must not execute.");
			Assert.IsFalse (handler.Post (action));
			Assert.AreEqual (1, handler.References.Count);
			if (handler.References [0].Target is Java.Lang.IRunnable runnable)
				Assert.AreEqual (IntPtr.Zero, runnable.Handle);
			handler.RemoveCallbacks (action);
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static List<WeakReference> PostAndRemove (CaptureHandler handler, Action action, bool clearQueue)
		{
			for (int i = 0; i < 100; i++)
				Assert.IsTrue (handler.PostDelayed (action, 3_600_000));
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			JniEnvironment.Runtime.ValueManager.CollectPeers ();
			Assert.IsTrue (handler.References.TrueForAll (r => r.IsAlive), "The Java queue must keep pending callbacks alive.");
			if (clearQueue)
				handler.RemoveCallbacksAndMessages (null);
			else
				handler.RemoveCallbacks (action);
			return handler.References;
		}

		static async Task OnHandler (Action<Handler> test)
		{
			using var thread = new HandlerThread ("ActionCallbackTests");
			thread.Start ();
			using var handler = new Handler (thread.Looper);
			var completion = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			try {
				Assert.IsTrue (handler.Post (() => {
					try {
						test (handler);
						Assert.IsTrue (handler.Post (() => completion.TrySetResult ()));
					} catch (Exception e) {
						completion.TrySetException (e);
					}
				}));
				await completion.Task.WaitAsync (TimeSpan.FromSeconds (10));
			} finally {
				handler.RemoveCallbacksAndMessages (null);
				thread.Quit ();
				thread.Join ();
			}
		}

		sealed class ColorDrawableAlias : ColorDrawable {
			public ColorDrawableAlias (IntPtr handle)
				: base (handle, JniHandleOwnership.DoNotTransfer | JniHandleOwnership.DoNotRegister) {}
		}

		sealed class CaptureHandler : Handler {
			public List<WeakReference> References { get; } = new ();

			public CaptureHandler (Looper looper) : base (looper) {}

			public override bool SendMessageAtTime (Message message, long uptimeMillis)
			{
				References.Add (new WeakReference (message.Callback, trackResurrection: true));
				return base.SendMessageAtTime (message, uptimeMillis);
			}
		}

		sealed class CallbackBatch : Java.Lang.Object, Drawable.ICallback {
			public List<Java.Lang.IRunnable> Runnables { get; } = new ();

			public void InvalidateDrawable (Drawable who) {}

			public void ScheduleDrawable (Drawable who, Java.Lang.IRunnable what, long when)
			{
				Runnables.Add (what);
			}

			public void UnscheduleDrawable (Drawable who, Java.Lang.IRunnable what)
			{
				Assert.Fail ("The scaling test must execute its callbacks, not cancel them.");
			}
		}

		sealed class DrawableCallback : Java.Lang.Object, Drawable.ICallback {
			public List<Java.Lang.IRunnable> Runnables { get; } = new ();
			readonly Dictionary<Java.Lang.IRunnable, Drawable> owners = new ();
			public bool IgnoreUnschedule { get; set; }
			public int ScheduledCount { get; private set; }

			public void InvalidateDrawable (Drawable who) {}

			public void ScheduleDrawable (Drawable who, Java.Lang.IRunnable what, long when)
			{
				Runnables.Add (what);
				owners.Add (what, who);
				ScheduledCount++;
			}

			public void UnscheduleDrawable (Drawable who, Java.Lang.IRunnable what)
			{
				if (IgnoreUnschedule)
					return;
				for (int i = Runnables.Count - 1; i >= 0; i--) {
					var runnable = Runnables [i];
					if (JNIEnv.IsSameObject (owners [runnable].Handle, who.Handle) &&
							JNIEnv.IsSameObject (runnable.Handle, what.Handle)) {
						Runnables.RemoveAt (i);
						owners.Remove (runnable);
					}
				}
			}
		}

		sealed class SchedulingView : View {
			public DrawableCallback Callback { get; } = new ();

			public SchedulingView () : base (Application.Context) {}

			public override void ScheduleDrawable (Drawable who, Java.Lang.IRunnable what, long when)
			{
				Callback.ScheduleDrawable (who, what, when);
			}

			public override void UnscheduleDrawable (Drawable who, Java.Lang.IRunnable what)
			{
				Callback.UnscheduleDrawable (who, what);
			}

			protected override void Dispose (bool disposing)
			{
				if (disposing)
					Callback.Dispose ();
				base.Dispose (disposing);
			}
		}
	}
}
