using System;

namespace Xamarin.Android.Tools.Plots
{
	[Flags]
	internal enum Status
	{
		OK                  = 0,
		ShowHelp            = (1 << 0),
		MissingArgument     = (1 << 1),
		CsvFileDoesNotExist = (1 << 2),
		Error               = (1 << 3),
	}

	internal class Result
	{
		public string Message { get; set; }
		public Status Status { get; set; } = Status.OK;
	}
}
