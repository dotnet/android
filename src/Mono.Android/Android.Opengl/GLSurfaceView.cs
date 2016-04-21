using System;
using Android.Runtime;

namespace Android.Opengl {

	public partial class GLSurfaceView {

		public void QueueEvent (Action r)
		{
			QueueEvent (new Java.Lang.Thread.RunnableImplementor (r));
		}
	}
}

