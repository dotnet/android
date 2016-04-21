using System;
using System.Collections.Generic;
using Android.Content;
using Android.Runtime;

namespace Android.App {

	public partial class ProgressDialog {

		public static ProgressDialog Show (Context context, Java.Lang.ICharSequence title, Java.Lang.ICharSequence message, bool indeterminate, bool cancelable, EventHandler cancelHandler)
		{
			return Show (context, title, message, indeterminate, cancelable, new IDialogInterfaceOnCancelListenerImplementor () { Handler = cancelHandler });
		}

		public static ProgressDialog Show (Context context, string title, string message, bool indeterminate, bool cancelable, EventHandler cancelHandler)
		{
			return Show (context, title, message, indeterminate, cancelable, new IDialogInterfaceOnCancelListenerImplementor () { Handler = cancelHandler });
		}
	}
}
