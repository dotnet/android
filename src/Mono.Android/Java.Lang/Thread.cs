using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Android.Runtime;
using Java.Interop;

namespace Java.Lang {

	public partial class Thread {

		[Register ("mono/java/lang/RunnableImplementor")]
		internal sealed class RunnableImplementor : Java.Lang.Object, IRunnable {

			Action? handler;
			JniObjectReference owner;
			JniObjectReference context;
			JniObjectReference token;
			bool posted;
			LinkedListNode<WeakReference<RunnableImplementor>>? registration;

			public RunnableImplementor (Action handler) : this (handler, null) {}

			public RunnableImplementor (Action handler, Java.Lang.Object? owner, Java.Lang.Object? context = null, Java.Lang.Object? token = null)
				: base (
						JNIEnv.StartCreateInstance ("mono/java/lang/RunnableImplementor", "()V"),
						JniHandleOwnership.TransferLocalRef)
			{
				JNIEnv.FinishCreateInstance (Handle, "()V");

				this.handler = handler;
				if (owner != null) {
					this.owner = owner.PeerReference.NewWeakGlobalRef ();
					if (context != null)
						this.context = context.PeerReference.NewWeakGlobalRef ();
					if (token != null)
						this.token = token.PeerReference.NewWeakGlobalRef ();
					lock (instances) {
						var runnables = instances.GetOrCreateValue (handler);
						// Java queues own these peers. A live Action must not keep a cleared queue alive.
						registration = runnables.AddLast (new WeakReference<RunnableImplementor> (this, trackResurrection: true));
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

			static readonly ConditionalWeakTable<Action, LinkedList<WeakReference<RunnableImplementor>>> instances = new ();

			public void SetPosted ()
			{
				lock (instances) {
					posted = true;
				}
			}

			void Unregister ()
			{
				if (registration?.List is { } runnables) {
					runnables.Remove (registration);
					if (runnables.Count == 0 && handler != null &&
							instances.TryGetValue (handler, out var current) && ReferenceEquals (current, runnables))
						instances.Remove (handler);
				}
				registration = null;
				JniObjectReference.Dispose (ref owner);
				JniObjectReference.Dispose (ref context);
				JniObjectReference.Dispose (ref token);
			}

			protected override void Dispose (bool disposing)
			{
				lock (instances) {
					Unregister ();
					handler = null;
					base.Dispose (disposing);
				}
			}

			static bool IsSameObject (JniObjectReference first, Java.Lang.Object second)
			{
				return first.IsValid && JNIEnv.IsSameObject (first.Handle, second.Handle);
			}

			public static void Remove (Action handler, Java.Lang.Object owner, Action<RunnableImplementor> remove, Java.Lang.Object? context = null, Java.Lang.Object? token = null, bool retire = false)
			{
				List<(RunnableImplementor Runnable, bool Retire)> matches;
				lock (instances) {
					if (!instances.TryGetValue (handler, out var runnables))
						return;
					matches = new ();
					for (var node = runnables.First; node != null;) {
						var next = node.Next;
						if (!node.Value.TryGetTarget (out var runnable))
							runnables.Remove (node);
						else if ((context != null
								? IsSameObject (runnable.context.IsValid ? runnable.context : runnable.owner, context)
								: IsSameObject (runnable.owner, owner)) &&
								(token == null || IsSameObject (runnable.token, token)))
							matches.Add ((runnable, retire && runnable.posted));
						node = next;
					}
					if (runnables.Count == 0)
						instances.Remove (handler);
				}
				// Drawable helpers share an originating drawable even across forwarding entry points.
				// Native removal still filters the actual queue, without disposing possibly owned peers.
				foreach (var (runnable, canRetire) in matches) {
					if (runnable.Handle != IntPtr.Zero) {
						remove (runnable);
						if (canRetire) {
							lock (instances) {
								runnable.Unregister ();
							}
						}
					}
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
