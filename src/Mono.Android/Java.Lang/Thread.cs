using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Android.Runtime;

namespace Java.Lang {

	public partial class Thread {

		[Register ("mono/java/lang/RunnableImplementor")]
		internal sealed class RunnableImplementor : Java.Lang.Object, IRunnable {

			public Action Handler;
			bool removable;

			public RunnableImplementor (Action handler) : this (handler, false) {}

			public RunnableImplementor (Action handler, bool removable)
				: base (
						JNIEnv.StartCreateInstance ("mono/java/lang/RunnableImplementor", "()V"),
						JniHandleOwnership.TransferLocalRef)
			{
				JNIEnv.FinishCreateInstance (Handle, "()V");

				Handler = handler;
				this.removable = removable;
				if (removable) {
					lock (instances) {
						var runnables = instances.GetOrCreateValue (handler);
						Prune (runnables);
						runnables.Add (new WeakReference<RunnableImplementor> (this, trackResurrection: true));
					}
				}
			}

			public void Run ()
			{
				try {
					Handler?.Invoke ();
				} finally {
					Dispose ();
				}
			}

			public new void Dispose ()
			{
				lock (this)
					base.Dispose ();
			}

			protected override void Dispose (bool disposing)
			{
				if (removable && Handler != null) {
					lock (instances) {
						if (instances.TryGetValue (Handler, out var runnables)) {
							Prune (runnables, this);
							if (runnables.Count == 0)
								instances.Remove (Handler);
						}
					}
				}
				base.Dispose (disposing);
			}

			// Java owns queued callbacks. Neither a rooted Action nor native cancellation
			// should keep a runnable alive through this lookup table.
			static readonly ConditionalWeakTable<Action, List<WeakReference<RunnableImplementor>>> instances = new ();

			static void Prune (List<WeakReference<RunnableImplementor>> runnables, RunnableImplementor? completed = null)
			{
				for (int i = runnables.Count - 1; i >= 0; i--) {
					if (!runnables [i].TryGetTarget (out var runnable) ||
							ReferenceEquals (runnable, completed) || runnable.Handle == IntPtr.Zero)
						runnables.RemoveAt (i);
				}
			}

			public static void Remove (Action handler, Action<RunnableImplementor> remove)
			{
				List<RunnableImplementor> pending = new ();
				lock (instances) {
					if (!instances.TryGetValue (handler, out var runnables))
						return;
					Prune (runnables);
					foreach (var reference in runnables) {
						if (reference.TryGetTarget (out var runnable))
							pending.Add (runnable);
					}
					if (runnables.Count == 0)
						instances.Remove (handler);
				}

				foreach (var runnable in pending) {
					lock (runnable) {
						if (runnable.Handle != IntPtr.Zero)
							remove (runnable);
					}
				}
				// Native removal may not match the handler, token or drawable. Keep the
				// weak mapping and let Java reachability determine when disposal is safe.
			}
		}

		/// <summary>
		/// Initializes a new <see cref="Thread"/> that runs the specified <paramref name="runHandler"/> when started.
		/// </summary>
		/// <param name="runHandler">The delegate to execute on the new thread.</param>
		/// <seealso href="https://developer.android.com/reference/java/lang/Thread#Thread(java.lang.Runnable)">Android documentation for <c>java.lang.Thread</c></seealso>
		public Thread (Action runHandler) : this (new RunnableImplementor (runHandler)) {}

		/// <summary>
		/// Initializes a new <see cref="Thread"/> with the specified name that runs the specified <paramref name="runHandler"/> when started.
		/// </summary>
		/// <param name="runHandler">The delegate to execute on the new thread.</param>
		/// <param name="threadName">The name of the new thread.</param>
		/// <seealso href="https://developer.android.com/reference/java/lang/Thread#Thread(java.lang.Runnable,%20java.lang.String)">Android documentation for <c>java.lang.Thread</c></seealso>
		public Thread (Action runHandler, string threadName) : this (new RunnableImplementor (runHandler), threadName) {}

		/// <summary>
		/// Initializes a new <see cref="Thread"/> in the specified thread group that runs the specified <paramref name="runHandler"/> when started.
		/// </summary>
		/// <param name="group">The thread group to which the new thread belongs.</param>
		/// <param name="runHandler">The delegate to execute on the new thread.</param>
		/// <seealso href="https://developer.android.com/reference/java/lang/Thread#Thread(java.lang.ThreadGroup,%20java.lang.Runnable)">Android documentation for <c>java.lang.Thread</c></seealso>
		public Thread (ThreadGroup group, Action runHandler) : this (group, new RunnableImplementor (runHandler)) {}

		/// <summary>
		/// Initializes a new <see cref="Thread"/> in the specified thread group with the specified name that runs the specified <paramref name="runHandler"/> when started.
		/// </summary>
		/// <param name="group">The thread group to which the new thread belongs.</param>
		/// <param name="runHandler">The delegate to execute on the new thread.</param>
		/// <param name="threadName">The name of the new thread.</param>
		/// <seealso href="https://developer.android.com/reference/java/lang/Thread#Thread(java.lang.ThreadGroup,%20java.lang.Runnable,%20java.lang.String)">Android documentation for <c>java.lang.Thread</c></seealso>
		public Thread (ThreadGroup group, Action runHandler, string threadName) : this (group, new RunnableImplementor (runHandler), threadName) {}

		/// <summary>
		/// Initializes a new <see cref="Thread"/> in the specified thread group with the specified name and stack size that runs the specified <paramref name="runHandler"/> when started.
		/// </summary>
		/// <param name="group">The thread group to which the new thread belongs.</param>
		/// <param name="runHandler">The delegate to execute on the new thread.</param>
		/// <param name="threadName">The name of the new thread.</param>
		/// <param name="stackSize">The desired stack size, in bytes, for the new thread, or <c>0</c> to use the default.</param>
		/// <seealso href="https://developer.android.com/reference/java/lang/Thread#Thread(java.lang.ThreadGroup,%20java.lang.Runnable,%20java.lang.String,%20long)">Android documentation for <c>java.lang.Thread</c></seealso>
		public Thread (ThreadGroup group, Action runHandler, string threadName, long stackSize) : this (group, new RunnableImplementor (runHandler), threadName, stackSize) {}
	}
}
