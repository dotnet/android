using System;
using System.Collections.Generic;

using Xamarin.Android.Prepare;

namespace Xamarin.Android.Tests
{
	class MSBuildTimingState : AppObject
	{
		public int EmulatorProcessId             { get; set; } = -1;
		public string AdbTarget                  { get; set; } = String.Empty;
		public List<MSBuildTimingResult> Results { get; } = new List<MSBuildTimingResult> ();
	}
}
