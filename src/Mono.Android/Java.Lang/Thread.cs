using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Android.Runtime;
using Java.Interop;

namespace Java.Lang {

	public partial class Thread {

		[Register ("mono/java/lang/RunnableImplementor")]
		internal sealed class RunnableImplementor : Java.Lang.Object, IRunnable {

			public Action Handler;
			bool removable;

			public RunnableImplementor (Action handler) : this (handler, false) {}

			RunnableImplementor (Action handler, bool removable)
				: base (
						// SAFETY: The complete ASCII literal supplies stable, NUL-terminated JNI class-name bytes.
						unsafe (JNIEnv.AllocObject ("mono/java/lang/RunnableImplementor"u8)),
						JniHandleOwnership.TransferLocalRef)
			{
				unsafe {
					// SAFETY: this peer owns the allocated handle; ()V supplies the zero-argument instance-constructor ABI.
					JNIEnv.FinishCreateInstance (Handle, "()V"u8);
				}

				Handler = handler;
				this.removable = removable;
			}

			public void Run ()
			{
				if (Handler != null)
					Handler ();
				// A removable runnable is shared by every pending post of its Action.
				if (!removable)
					Dispose ();
			}

			sealed class Owner (JniObjectReference reference)
			{
				public JniObjectReference Reference = reference;
				public readonly ConditionalWeakTable<Action, RunnableImplementor> Runnables = new ();
				// Runnables of View.ScheduleDrawable (who, Action, long), keyed by the drawable's entry. Only that
				// helper queues them, so View.RemoveCallbacks (Action) can cancel them without touching the
				// drawable's own ScheduleSelf posts. Entries go away once the drawable's entry is swept.
				public readonly ConditionalWeakTable<Action, ConditionalWeakTable<Owner, RunnableImplementor>> Scheduled = new ();
			}

			const int MinimumSweepThreshold = 256;

			static readonly Lock ownersLock = new ();
			static readonly Dictionary<int, List<Owner>> owners = new ();
			static int ownerCount;
			static int sweepThreshold = MinimumSweepThreshold;

			// Reusing one runnable per owner and Action lets the Java remove/unschedule methods cancel
			// every pending post. Android matches runnables, not owners, when Views share a window
			// Handler or Drawables share a View queue, so each owner needs its own runnable.
			// Owners are matched by Java identity, so a replacement wrapper for the same Java object
			// (e.g. after Dispose ()) still cancels posts made through the original wrapper.
			public static RunnableImplementor GetOrCreate (Java.Lang.Object owner, Action handler) =>
				GetOwner (owner).Runnables.GetValue (handler, static h => new RunnableImplementor (h, removable: true));

			public static RunnableImplementor? Find (Java.Lang.Object owner, Action handler) =>
				FindOwner (owner) is { } entry && entry.Runnables.TryGetValue (handler, out var runnable) ? runnable : null;

			// The runnable View.ScheduleDrawable (who, Action, long) uses for (view, who, handler).
			public static RunnableImplementor GetOrCreateScheduled (Java.Lang.Object view, Java.Lang.Object who, Action handler)
			{
				var drawable = GetOwner (who);
				var scheduled = GetOwner (view).Scheduled.GetValue (handler, static _ => new ());
				if (scheduled.TryGetValue (drawable, out var runnable))
					return runnable;
				var created = new RunnableImplementor (handler, removable: true);
				if (scheduled.TryAdd (drawable, created))
					return created;
				created.Dispose ();
				return scheduled.TryGetValue (drawable, out runnable) ? runnable : throw new InvalidOperationException ();
			}

			public static RunnableImplementor? FindScheduled (Java.Lang.Object view, Java.Lang.Object who, Action handler) =>
				FindOwner (view) is { } entry && entry.Scheduled.TryGetValue (handler, out var scheduled) &&
				FindOwner (who) is { } drawable && scheduled.TryGetValue (drawable, out var runnable) ? runnable : null;

			// Concurrent View.ScheduleDrawable calls may or may not be included.
			public static RunnableImplementor[] FindAllScheduled (Java.Lang.Object view, Action handler)
			{
				if (FindOwner (view) is not { } entry || !entry.Scheduled.TryGetValue (handler, out var scheduled))
					return [];
				var runnables = new List<RunnableImplementor> ();
				foreach (var (_, runnable) in scheduled)
					runnables.Add (runnable);
				return runnables.ToArray ();
			}

			static Owner GetOwner (Java.Lang.Object owner) =>
				GetOwner (owner, create: true) ?? throw new ObjectDisposedException (owner.GetType ().FullName);

			static Owner? FindOwner (Java.Lang.Object owner) => GetOwner (owner, create: false);

			static Owner? GetOwner (Java.Lang.Object owner, bool create)
			{
				var reference = owner.PeerReference;
				if (!reference.IsValid)
					return null;

				int hash = owner.JniIdentityHashCode;
				lock (ownersLock) {
					if (owners.TryGetValue (hash, out var bucket)) {
						foreach (var entry in bucket) {
							if (JniEnvironment.Types.IsSameObject (entry.Reference, reference))
								return entry;
						}
					}
					if (!create)
						return null;

					// Drop owners whose Java objects were collected; nothing can cancel their posts anymore.
					if (ownerCount >= sweepThreshold) {
						RemoveCollectedOwners ();
						sweepThreshold = System.Math.Max (MinimumSweepThreshold, ownerCount * 2);
					}
					if (!owners.TryGetValue (hash, out bucket)) {
						bucket = [];
						owners.Add (hash, bucket);
					}
					var owned = new Owner (reference.NewWeakGlobalRef ());
					bucket.Add (owned);
					ownerCount++;
					return owned;
				}
			}

			static void RemoveCollectedOwners ()
			{
				List<int>? empty = null;
				foreach (var (hash, bucket) in owners) {
					ownerCount -= bucket.RemoveAll (static entry => {
						if (!JniEnvironment.Types.IsSameObject (entry.Reference, default))
							return false;
						JniObjectReference.Dispose (ref entry.Reference);
						return true;
					});
					if (bucket.Count == 0)
						(empty ??= []).Add (hash);
				}
				if (empty == null)
					return;
				foreach (var hash in empty)
					owners.Remove (hash);
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
