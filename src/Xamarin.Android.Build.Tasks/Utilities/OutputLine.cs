using System;
namespace Xamarin.Android.Tasks {
	public struct OutputLine {
		public string Line;
		public bool StdError;

		public bool Errored;

		public long JobId;

		public OutputLine (string line, bool stdError, bool errored = false, long jobId = 0)
		{
			Line = line;
			StdError = stdError;
			Errored = errored;
			JobId = jobId;
		}
	}
}
