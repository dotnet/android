using System;

namespace Xamarin.Android.Tools.Plots
{
	[Flags]
	internal enum Status
	{
		OK = 0,
		ShowHelp = 1,
		MissingArgument = 2,
		CsvFileDoesNotExist = 4,
		Error = 8
	}

	internal class Result
	{
		public string Message { get; set; }
		public Status Status { get; set; } = Status.OK;
	}
}
