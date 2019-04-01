using System;
namespace Xamarin.Android.Tasks {
	public struct OutputLine {
		public string Line;
		public bool StdError;

		public OutputLine (string line, bool stdError)
		{
			Line = line;
			StdError = stdError;
		}
	}
}
