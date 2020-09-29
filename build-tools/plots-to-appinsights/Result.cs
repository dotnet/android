namespace Xamarin.Android.Tools.Plots
{
	internal enum Status
	{
		OK = 0,
		ShowHelp = 1,
		MissingArgument = 2,
		CsvFileDoesNotExist = 3,
		Error = 4
	}

	internal class Result
	{
		public string Message { get; set; }
		public Status Status { get; set; } = Status.OK;
	}
}
