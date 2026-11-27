
using System;

using Android.Runtime;

namespace Android.Animation {

	partial class Animator {

		WeakReference? dispatcher;
		AnimatorEventDispatcher Dispatcher {
			get {
				if (dispatcher?.Target is AnimatorEventDispatcher d)
					return d;
				d = new AnimatorEventDispatcher ();
				dispatcher = new WeakReference (d);
				AddListener (d);
				return d;
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

	[Register ("net/dot/android/animation/AnimatorEventDispatcher")]
	internal class AnimatorEventDispatcher : Java.Lang.Object, Animator.IAnimatorListener {

		public AnimatorEventDispatcher ()
			: base (
					// SAFETY: The complete ASCII literal supplies stable, NUL-terminated JNI class-name bytes.
					unsafe (JNIEnv.AllocObject ("net/dot/android/animation/AnimatorEventDispatcher"u8)),
					JniHandleOwnership.TransferLocalRef)
		{
			unsafe {
				// SAFETY: this peer owns the allocated handle, retained by GC.KeepAlive (this) across FinishCreateInstance;
				// ()V supplies the zero-argument instance-constructor ABI.
				JNIEnv.FinishCreateInstance (Handle, "()V"u8);
				GC.KeepAlive (this);
			}
		}

		public EventHandler? AnimationCancel;
		public EventHandler? AnimationEnd;
		public EventHandler? AnimationRepeat;
		public EventHandler? AnimationStart;

		public void OnAnimationCancel (Animator? animation)
		{
			var h = AnimationCancel;
			if (h != null)
				h (animation, EventArgs.Empty);
		}

		public void OnAnimationEnd (Animator? animation)
		{
			var h = AnimationEnd;
			if (h != null)
				h (animation, EventArgs.Empty);
		}

		public void OnAnimationRepeat (Animator? animation)
		{
			var h = AnimationRepeat;
			if (h != null)
				h (animation, EventArgs.Empty);
		}

		public void OnAnimationStart (Animator? animation)
		{
			var h = AnimationStart;
			if (h != null)
				h (animation, EventArgs.Empty);
		}
	}
}
