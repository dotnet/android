using System;
using Android.Runtime;
using Java.Interop;

namespace Android.Graphics.Drawables {

	public partial class Drawable {

		public void ScheduleSelf (Action what, long when)
		{
			ScheduleSelf (new Java.Lang.Thread.RunnableImplementor (what, true), when);
		}

		public void UnscheduleSelf (Action what)
		{
			Java.Lang.Thread.RunnableImplementor.Remove (what, this, static (runnable, drawable) => drawable.UnscheduleSelf (runnable));
		}

		unsafe void UnscheduleSelf (JniObjectReference runnable)
		{
			const string id = "unscheduleSelf.(Ljava/lang/Runnable;)V";
			JniArgumentValue* args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (runnable.Handle);
			_members.InstanceMethods.InvokeVirtualVoidMethod (id, this, args);
		}
	}
}
