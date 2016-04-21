using System;

using Android.Content;

namespace Android.Views.InputMethods {

	public partial class InputMethodManager {

		public static InputMethodManager FromContext (Context context)
		{
			return context.GetSystemService (Context.InputMethodService) as InputMethodManager;
		}
	}
}


