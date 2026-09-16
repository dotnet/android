using System;
using Android.Runtime;
using Java.Interop;

namespace Android.Graphics.Drawables {

	public partial class InsetDrawable {

		public void ScheduleDrawable (Android.Graphics.Drawables.Drawable who, Action what, long when)
		{
			ScheduleDrawable (who, new Java.Lang.Thread.RunnableImplementor (what, true), when);
		}

		public void UnscheduleDrawable (Android.Graphics.Drawables.Drawable who, Action what)
		{
			Java.Lang.Thread.RunnableImplementor.Remove (what, this, who, static (runnable, drawable, who) => drawable.UnscheduleDrawable (who, runnable));
		}

		unsafe void UnscheduleDrawable (Android.Graphics.Drawables.Drawable who, JniObjectReference runnable)
		{
			const string id = "unscheduleDrawable.(Landroid/graphics/drawable/Drawable;Ljava/lang/Runnable;)V";
			try {
				JniArgumentValue* args = stackalloc JniArgumentValue [2];
				args [0] = new JniArgumentValue (who.Handle);
				args [1] = new JniArgumentValue (runnable.Handle);
				_members.InstanceMethods.InvokeVirtualVoidMethod (id, this, args);
			} finally {
				GC.KeepAlive (who);
			}
		}
	}
}
