using System;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class AdbFilterTestResults : Adb
	{
		public override bool Execute ()
		{
			var result = base.Execute ();
			var regex = new Regex (@"Tests run: .*$");

			foreach (var line in Output) {
				var match = regex.Match (line);
				if (match.Success)
					Console.WriteLine (match.Value);
			}

			return result;
		}
	}
}
