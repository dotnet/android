namespace Xamarin.Android.Prepare
{
	partial class Context
	{
		static bool isWindows = true;

		void InitOS ()
		{
			OS = new Windows (Context.Instance);

			// Windows console (cmd.exe) is rather... antiquated and doesn't behave well with our output so turn off all
			// the nice stuff until Windows Terminal is in and we can enjoy the full console goodness.
			UseColor = false;
			NoEmoji = true;
			DullMode = true;
		}
	}
}
