using System;

namespace Xamarin.Android.Prepare
{
	partial class ThumbTwiddler
	{
		int ConsoleCursorTop => Console.CursorTop;
		int ConsoleCursorLeft => Console.CursorLeft;

		void ConsoleSetCursorPosition (int left, int top)
		{
			Console.SetCursorPosition (left, top);
		}
	}
}
