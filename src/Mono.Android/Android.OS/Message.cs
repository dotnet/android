using System;
using Android.Runtime;

namespace Android.OS {

	public partial class Message {

		public static Message Obtain (Handler h, Action @callback)
		{
			return Obtain (h, new Java.Lang.Thread.RunnableImplementor (@callback));
		}

	}
}

