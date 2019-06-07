using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class ThumbTwiddler
	{
		int ConsoleCursorTop => SafeConsoleAccess (() => Console.CursorTop);
		int ConsoleCursorLeft => SafeConsoleAccess (() => Console.CursorLeft);

		void ConsoleSetCursorPosition (int left, int top)
		{
			SafeConsoleAccess (() => {
					Console.SetCursorPosition (left, top);
					return 0;
				}
			);
		}

		int SafeConsoleAccess (Func<int> code)
		{
			// Accessing the console may throw an exception of Windows (e.g. when xaprepare runs from within msbuild)
			try {
				return code ();
			} catch (IOException) {
				// Ignore
			}

			return 0;
		}
	}
}
