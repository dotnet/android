using System;
using Android.Runtime;

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
	}
}
