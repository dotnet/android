#if ANDROID_11

using System;
using Android.Runtime;

namespace Android.Animation
{
	public partial class AnimatorSet
	{
		private static IntPtr id_setDuration_J;
		[Register ("setDuration", "(J)Landroid/animation/Animator;", "GetSetDuration_JHandler")]
		public override Animator SetDuration (long duration)
		{
			if (id_setDuration_J == IntPtr.Zero)
				id_setDuration_J = JNIEnv.GetMethodID (class_ref, "setDuration", "(J)Landroid/animation/Animator;");
			
			if (base.GetType () == this.ThresholdType) {
				return Java.Lang.Object.GetObject<AnimatorSet> (
						JNIEnv.CallObjectMethod (base.Handle, id_setDuration_J, new JValue (duration)),
						JniHandleOwnership.TransferLocalRef);
			} else {
				return Java.Lang.Object.GetObject<AnimatorSet> (
						JNIEnv.CallNonvirtualObjectMethod (
							base.Handle,
							this.ThresholdClass,
							JNIEnv.GetMethodID (ThresholdClass, "setDuration", "(J)Landroid/animation/Animator;"),
							new JValue (duration)),
						JniHandleOwnership.TransferLocalRef);
			}
		}
		
		private static Delegate cb_setDuration_J;
		
		private static Delegate GetSetDuration_JHandler ()
		{
			if (cb_setDuration_J == null)
				cb_setDuration_J = JNINativeWrapper.CreateDelegate (new Func<IntPtr, IntPtr, long, IntPtr> (n_SetDuration_J));
			return cb_setDuration_J;
		}
		
		private static IntPtr n_SetDuration_J (IntPtr jnienv, IntPtr native__this, long duration)
		{
			AnimatorSet @object = Java.Lang.Object.GetObject<AnimatorSet> (native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToJniHandle (@object.SetDuration (duration));
		}
	}
}

#endif
