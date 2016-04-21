#if ANDROID_11

using System;

using Android.Runtime;

namespace Android.Animation {

	partial class Animator {

		WeakReference dispatcher;
		AnimatorEventDispatcher Dispatcher {
			get {
				if (dispatcher == null || !dispatcher.IsAlive) {
					dispatcher = new WeakReference (new AnimatorEventDispatcher ());
					AddListener ((AnimatorEventDispatcher) dispatcher.Target);
				}
				return (AnimatorEventDispatcher) dispatcher.Target;
			}
		}

		public event EventHandler AnimationCancel {
			add {
				Dispatcher.AnimationCancel += value;
			}
			remove {
				Dispatcher.AnimationCancel -= value;
			}
		}

		public event EventHandler AnimationEnd {
			add {
				Dispatcher.AnimationEnd += value;
			}
			remove {
				Dispatcher.AnimationEnd -= value;
			}
		}

		public event EventHandler AnimationRepeat {
			add {
				Dispatcher.AnimationRepeat += value;
			}
			remove {
				Dispatcher.AnimationRepeat -= value;
			}
		}

		public event EventHandler AnimationStart {
			add {
				Dispatcher.AnimationStart += value;
			}
			remove {
				Dispatcher.AnimationStart -= value;
			}
		}
	}

	[Register ("mono/android/animation/AnimatorEventDispatcher")]
	internal class AnimatorEventDispatcher : Java.Lang.Object, Animator.IAnimatorListener {

		public AnimatorEventDispatcher ()
			: base (
					JNIEnv.StartCreateInstance ("mono/android/animation/AnimatorEventDispatcher", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			JNIEnv.FinishCreateInstance (Handle, "()V");
		}

		public EventHandler AnimationCancel;
		public EventHandler AnimationEnd;
		public EventHandler AnimationRepeat;
		public EventHandler AnimationStart;

		public void OnAnimationCancel (Animator animation)
		{
			var h = AnimationCancel;
			if (h != null)
				h (animation, EventArgs.Empty);
		}

		public void OnAnimationEnd (Animator animation)
		{
			var h = AnimationEnd;
			if (h != null)
				h (animation, EventArgs.Empty);
		}

		public void OnAnimationRepeat (Animator animation)
		{
			var h = AnimationRepeat;
			if (h != null)
				h (animation, EventArgs.Empty);
		}

		public void OnAnimationStart (Animator animation)
		{
			var h = AnimationStart;
			if (h != null)
				h (animation, EventArgs.Empty);
		}
	}
}

#endif
