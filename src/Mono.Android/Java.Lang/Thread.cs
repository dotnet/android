using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Android.Runtime;

namespace Java.Lang {

	public partial class Thread {

		[Register ("mono/java/lang/RunnableImplementor")]
		internal sealed class RunnableImplementor : Java.Lang.Object, IRunnable {

			Action? handler;
			readonly bool removable;

			public RunnableImplementor (Action handler) : this (handler, false) {}

			public RunnableImplementor (Action handler, bool removable)
				: base (
						JNIEnv.StartCreateInstance ("mono/java/lang/RunnableImplementor", "()V"),
						JniHandleOwnership.TransferLocalRef)
			{
				JNIEnv.FinishCreateInstance (Handle, "()V");

				this.handler = handler;
				this.removable = removable;
				if (removable) {
					lock (instances) {
						var runnables = instances.GetOrCreateValue (handler);
						runnables.RemoveAll (r => !r.TryGetTarget (out _));
						// Java queues own these peers. A live Action must not keep a cleared queue alive.
						runnables.Add (new WeakReference<RunnableImplementor> (this, trackResurrection: true));
					}
				}
			}

			public void Run ()
			{
				try {
					handler?.Invoke ();
				} finally {
					Dispose ();
				}
			}

			static readonly ConditionalWeakTable<Action, List<WeakReference<RunnableImplementor>>> instances = new ();

			protected override void Dispose (bool disposing)
			{
				lock (instances) {
					if (handler != null && removable && instances.TryGetValue (handler, out var runnables)) {
						runnables.RemoveAll (r => !r.TryGetTarget (out var target) || ReferenceEquals (target, this));
						if (runnables.Count == 0)
							instances.Remove (handler);
					}
					handler = null;
					base.Dispose (disposing);
				}
			}

			public static void Remove (Action handler, Action<RunnableImplementor> remove)
			{
				WeakReference<RunnableImplementor> [] references;
				lock (instances) {
					if (!instances.TryGetValue (handler, out var runnables))
						return;
					runnables.RemoveAll (r => !r.TryGetTarget (out _));
					references = runnables.ToArray ();
				}
				// Native removal filters by queue owner/token and can be a no-op.
				// Let the queue release the peer instead of disposing a possibly queued runnable.
				foreach (var reference in references) {
					if (reference.TryGetTarget (out var runnable) && runnable.Handle != IntPtr.Zero)
						remove (runnable);
				}
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
