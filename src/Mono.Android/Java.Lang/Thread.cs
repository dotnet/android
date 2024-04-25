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
				if (removable)
					lock (instances)
						instances.AddOrUpdate (handler, this);
			}

			public void Run ()
			{
				if (Handler != null)
					Handler ();
				if (removable)
					lock (instances)
						if (Handler != null)
							instances.Remove (Handler);
				Dispose ();
			}

			static ConditionalWeakTable<Action, RunnableImplementor> instances = new ();

			public static RunnableImplementor Remove (Action handler)
			{
				RunnableImplementor result;
				lock (instances) {
					instances.TryGetValue (handler, out result!);
					instances.Remove (handler);
				}
				return result;
			}
		}

		public Thread (Action runHandler) : this (new RunnableImplementor (runHandler)) {}

		public Thread (Action runHandler, string threadName) : this (new RunnableImplementor (runHandler), threadName) {}

		public Thread (ThreadGroup group, Action runHandler) : this (group, new RunnableImplementor (runHandler)) {}

		public Thread (ThreadGroup group, Action runHandler, string threadName) : this (group, new RunnableImplementor (runHandler), threadName) {}

		public Thread (ThreadGroup group, Action runHandler, string threadName, long stackSize) : this (group, new RunnableImplementor (runHandler), threadName, stackSize) {}
	}
}

