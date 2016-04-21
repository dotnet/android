using System;
using Android.Runtime;

namespace Android.App {

	public partial class Instrumentation {

		public void RunOnMainSync (Action runner)
		{
			RunOnMainSync (new Java.Lang.Thread.RunnableImplementor (runner));
		}

		public void WaitForIdle (Action recipient)
		{
			WaitForIdle (new Java.Lang.Thread.RunnableImplementor (recipient));
		}

	}
}

