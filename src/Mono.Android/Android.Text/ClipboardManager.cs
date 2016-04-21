using System;

using Android.Content;

namespace Android.Text {

	public partial class ClipboardManager {

		public static ClipboardManager FromContext (Context context)
		{
			return context.GetSystemService (Context.ClipboardService) as ClipboardManager;
		}
	}
}


