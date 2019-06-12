using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task splits a property into an item group, considering the following delimiters:
	/// ; - MSBuild default
	/// , - Historically supported by Xamarin.Android
	/// </summary>
	public class SplitProperty : Task
	{
		static readonly char [] Delimiters = { ',', ';' };

		public string Value { get; set; }

		[Output]
		public string [] Output { get; set; }

		public override bool Execute ()
		{
			if (Value != null) {
				Output = Value.Split (Delimiters, StringSplitOptions.RemoveEmptyEntries);
			}
			return true;
		}
	}
}
