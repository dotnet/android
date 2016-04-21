namespace Android.InputMethodServices
{
	public partial class KeyboardView
	{
		protected virtual KeyboardView.IOnKeyboardActionListener GetOnKeyboardActionListener ()
		{
			return OnKeyboardActionListener;
		}
	}
}

