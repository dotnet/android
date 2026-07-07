using System;
using Java.Interop;
using Android.Runtime;

namespace Android.Animation
{
	public partial class ValueAnimator
	{
		static Delegate? cb_setDuration_SetDuration_J_Landroid_animation_Animator_;
		
		[Register ("setDuration", "(J)Landroid/animation/Animator;", "GetSetDuration_JHandler")]
		public override unsafe Android.Animation.Animator SetDuration (long duration)
		{
			const string __id = "setDuration.(J)Landroid/animation/Animator;";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (duration);
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, __args);
				var __ret = global::Java.Lang.Object.GetObject<Android.Animation.Animator> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				return __ret ?? throw new InvalidOperationException ("Unable to marshal the return value to a managed Android.Animation.Animator instance.");
			} finally {
			}
		}
		
#pragma warning disable 0169
		static Delegate GetSetDuration_JHandler ()
		{
			return cb_setDuration_SetDuration_J_Landroid_animation_Animator_ ??= new _JniMarshal_PPJ_L (n_SetDuration_J);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static IntPtr n_SetDuration_J (IntPtr jnienv, IntPtr native__this, long duration)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				var __this = global::Java.Lang.Object.GetObject<Android.Animation.ValueAnimator> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				if (__this is null)
					throw new InvalidOperationException ("Unable to marshal the native handle to a managed Android.Animation.ValueAnimator instance.");
				return JNIEnv.ToLocalJniHandle (__this.SetDuration (duration));
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169
	}
}
